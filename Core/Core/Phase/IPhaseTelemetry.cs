using System;
using BeltRunner.Core.Execution;
using BeltRunner.Core.Units;

namespace BeltRunner.Core.Phase;

/// <summary>
/// Provides the structured write-side telemetry contract for a phase.
/// </summary>
/// <remarks>
/// <para>
/// This interface exposes low-level runtime control over phase progress, unit progress, unit status, and diagnostics.
/// </para>
/// <para>
/// When the phase only needs aggregate completed-unit reporting, prefer the higher-level helper returned by
/// <see cref="BeginPhaseProgressTracking(int)"/> instead of manually combining low-level calls.
/// </para>
/// </remarks>
public interface IPhaseTelemetry {
    /// <summary>
    /// Sets the best-known total number of units expected for this phase.
    /// </summary>
    /// <param name="totalUnits">
    /// The current best-known total, or <see langword="null"/> when no estimate is available.
    /// Runtime snapshots treat this value as a floor that cannot be smaller than the number of units
    /// already observed in <see cref="IPhase.Units"/>.
    /// </param>
    void SetTotalUnits(int? totalUnits);

    /// <summary>
    /// Begins a high-level progress tracker for completed-unit reporting within the current phase.
    /// </summary>
    /// <param name="totalUnits">
    /// The total number of units expected for this phase.
    /// This value must be greater than zero.
    /// </param>
    /// <returns>
    /// A tracker that can report aggregate completion progress and create per-unit processing scopes.
    /// </returns>
    /// <remarks>
    /// This helper complements the lower-level telemetry methods.
    /// It calls <see cref="SetTotalUnits(int?)"/> internally so callers do not need to set the total separately.
    /// </remarks>
    IPhaseProgressTracker BeginPhaseProgressTracking(int totalUnits);

    /// <summary>
    /// Updates the current progress ratio of a tracked unit.
    /// </summary>
    /// <param name="unitId">The unit identifier.</param>
    /// <param name="ratio">The current ratio in the range <c>[0, 1]</c>.</param>
    /// <remarks>
    /// Use this method when the phase has meaningful fractional unit progress.
    /// For aggregate completed-unit reporting, prefer <see cref="BeginPhaseProgressTracking(int)"/>.
    /// </remarks>
    void SetUnitProgress(Guid unitId, double ratio);

    /// <summary>
    /// Updates the current status of a tracked unit.
    /// </summary>
    /// <param name="unitId">The unit identifier.</param>
    /// <param name="status">The new unit status.</param>
    /// <remarks>
    /// This is the low-level status control API.
    /// High-level helpers may call it internally, but phases can still use it directly when they need explicit state management.
    /// </remarks>
    void SetUnitStatus(Guid unitId, UnitStatus status);

    /// <summary>
    /// Publishes a structured diagnostic entry for this phase.
    /// </summary>
    /// <param name="severity">The diagnostic severity.</param>
    /// <param name="message">The diagnostic message.</param>
    /// <param name="exception">
    /// The related exception, if any. Public diagnostic surfaces expose only sanitized fault information derived
    /// from this exception. Raw exception details can still be written to NLog for trusted diagnostics when the
    /// application configures NLog to capture BeltRunner loggers.
    /// </param>
    /// <param name="unitId">The related unit identifier, if any.</param>
    /// <remarks>
    /// Published diagnostics become part of the run's public diagnostic surfaces after sanitization rules have been applied.
    /// </remarks>
    void PublishDiagnostic(DiagnosticSeverity severity, string message, Exception? exception = null, Guid? unitId = null);
}
