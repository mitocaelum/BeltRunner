using System;
using BeltRunner.Core.Units;

namespace BeltRunner.Core.Phase;

/// <summary>
/// Represents a high-level helper that tracks phase progress by completed unit count.
/// </summary>
/// <remarks>
/// <para>
/// This contract is intended for phases that only need to report how many units have completed out of the total.
/// </para>
/// <para>
/// Lower-level telemetry methods such as <see cref="IPhaseTelemetry.SetUnitProgress"/> and
/// <see cref="IPhaseTelemetry.SetUnitStatus"/> remain available for direct runtime control.
/// </para>
/// <para>
/// The tracker is disposable so that phase code can scope its lifetime explicitly, but disposal alone does not mark
/// outstanding units as completed.
/// </para>
/// </remarks>
public interface IPhaseProgressTracker : IDisposable {
    /// <summary>
    /// Reports the best-known number of completed units for the current phase.
    /// </summary>
    /// <param name="completedUnits">
    /// The number of completed units observed so far.
    /// The value must be zero or greater.
    /// </param>
    /// <remarks>
    /// This method is useful when the phase already maintains an aggregate completed count separately from individual unit scopes.
    /// </remarks>
    void ReportCompleted(int completedUnits);

    /// <summary>
    /// Begins tracking a single unit-processing scope.
    /// </summary>
    /// <param name="unitId">The tracked unit identifier.</param>
    /// <returns>
    /// A unit-processing scope that marks the unit as running immediately and can later complete it successfully.
    /// </returns>
    /// <remarks>
    /// Disposing the returned scope without calling <see cref="ITrackedUnitScope.Complete"/> leaves aggregate completion unchanged.
    /// </remarks>
    ITrackedUnitScope BeginUnit(Guid unitId);

    /// <summary>
    /// Begins tracking a single unit-processing scope.
    /// </summary>
    /// <param name="unit">The tracked unit.</param>
    /// <returns>
    /// A unit-processing scope that marks the unit as running immediately and can later complete it successfully.
    /// </returns>
    /// <remarks>
    /// This overload is equivalent to passing <see cref="IUnit.Id"/> to <see cref="BeginUnit(Guid)"/>.
    /// </remarks>
    ITrackedUnitScope BeginUnit(IUnit unit);
}
