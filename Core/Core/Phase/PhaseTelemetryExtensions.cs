using System;
using BeltRunner.Core.Execution;
using BeltRunner.Core.Units;

namespace BeltRunner.Core.Phase;

/// <summary>
/// Provides convenience methods for common telemetry operations.
/// </summary>
public static class PhaseTelemetryExtensions {
    /// <summary>
    /// Marks the specified unit as running.
    /// </summary>
    /// <param name="telemetry">
    /// The telemetry channel.
    /// </param>
    /// <param name="unit">
    /// The unit to mark as running.
    /// </param>
    public static void StartUnit(this IPhaseTelemetry telemetry, IUnit unit) {
        if( telemetry is null ) throw new ArgumentNullException(nameof(telemetry));
        if( unit is null ) throw new ArgumentNullException(nameof(unit));

        telemetry.SetUnitStatus(unit.Id, UnitStatus.Running);
    }

    /// <summary>
    /// Marks the specified unit identifier as running.
    /// </summary>
    /// <param name="telemetry">
    /// The telemetry channel.
    /// </param>
    /// <param name="unitId">
    /// The unit identifier.
    /// </param>
    public static void StartUnit(this IPhaseTelemetry telemetry, Guid unitId) {
        if( telemetry is null ) throw new ArgumentNullException(nameof(telemetry));

        telemetry.SetUnitStatus(unitId, UnitStatus.Running);
    }

    /// <summary>
    /// Updates the progress ratio of a tracked unit.
    /// </summary>
    public static void ReportUnitProgress(this IPhaseTelemetry telemetry, Guid unitId, double ratio) {
        if( telemetry is null ) throw new ArgumentNullException(nameof(telemetry));

        telemetry.SetUnitProgress(unitId, ratio);
    }

    /// <summary>
    /// Updates the progress ratio of a tracked unit.
    /// </summary>
    public static void ReportUnitProgress(this IPhaseTelemetry telemetry, IUnit unit, double ratio) {
        if( telemetry is null ) throw new ArgumentNullException(nameof(telemetry));
        if( unit is null ) throw new ArgumentNullException(nameof(unit));

        telemetry.SetUnitProgress(unit.Id, ratio);
    }

    /// <summary>
    /// Marks a unit as completed successfully.
    /// </summary>
    public static void CompleteUnit(this IPhaseTelemetry telemetry, Guid unitId) {
        if( telemetry is null ) throw new ArgumentNullException(nameof(telemetry));

        telemetry.SetUnitProgress(unitId, 1.0);
        telemetry.SetUnitStatus(unitId, UnitStatus.Succeeded);
    }

    /// <summary>
    /// Marks a unit as completed successfully.
    /// </summary>
    public static void CompleteUnit(this IPhaseTelemetry telemetry, IUnit unit) {
        if( telemetry is null ) throw new ArgumentNullException(nameof(telemetry));
        if( unit is null ) throw new ArgumentNullException(nameof(unit));

        telemetry.CompleteUnit(unit.Id);
    }

    /// <summary>
    /// Marks a unit as skipped.
    /// </summary>
    public static void SkipUnit(this IPhaseTelemetry telemetry, Guid unitId) {
        if( telemetry is null ) throw new ArgumentNullException(nameof(telemetry));

        telemetry.SetUnitStatus(unitId, UnitStatus.Skipped);
    }

    /// <summary>
    /// Marks a unit as failed.
    /// </summary>
    public static void FailUnit(this IPhaseTelemetry telemetry, Guid unitId) {
        if( telemetry is null ) throw new ArgumentNullException(nameof(telemetry));

        telemetry.SetUnitStatus(unitId, UnitStatus.Failed);
    }

    /// <summary>
    /// Publishes an information diagnostic.
    /// </summary>
    public static void Info(this IPhaseTelemetry telemetry, string message, Guid? unitId = null) {
        if( telemetry is null ) throw new ArgumentNullException(nameof(telemetry));

        telemetry.PublishDiagnostic(DiagnosticSeverity.Information, message, null, unitId);
    }

    /// <summary>
    /// Publishes a warning diagnostic.
    /// </summary>
    public static void Warn(this IPhaseTelemetry telemetry, string message, Exception? exception = null, Guid? unitId = null) {
        if( telemetry is null ) throw new ArgumentNullException(nameof(telemetry));

        telemetry.PublishDiagnostic(DiagnosticSeverity.Warning, message, exception, unitId);
    }

    /// <summary>
    /// Publishes an error diagnostic.
    /// </summary>
    public static void Error(this IPhaseTelemetry telemetry, string message, Exception? exception = null, Guid? unitId = null) {
        if( telemetry is null ) throw new ArgumentNullException(nameof(telemetry));

        telemetry.PublishDiagnostic(DiagnosticSeverity.Error, message, exception, unitId);
    }
}
