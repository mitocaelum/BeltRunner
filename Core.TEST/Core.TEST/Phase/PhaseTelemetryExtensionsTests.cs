using BeltRunner.Core.Execution;
using BeltRunner.Core.Phase;
using BeltRunner.Core.TEST.Testing;
using BeltRunner.Core.Units;

namespace BeltRunner.Core.TEST.Phase;

/// <summary>
/// Verifies the convenience telemetry APIs defined by <see cref="PhaseTelemetryExtensions"/>.
/// </summary>
/// <remarks>
/// <para>Purpose: Protect the extension methods that translate unit-centric calls into telemetry contract operations.</para>
/// <para>Why this matters: These helpers are intended to make phase code simpler, but they must preserve the exact identifiers and severities required by the runtime.</para>
/// <para>Expected result: Unit status, progress, and diagnostic helpers forward the expected values to the telemetry sink.</para>
/// </remarks>
[TestFixture]
[TestOf(typeof(PhaseTelemetryExtensions))]
public sealed class PhaseTelemetryExtensionsTests {
    /// <summary>
    /// Verifies that the unit lifecycle helpers use the runtime unit identifier.
    /// </summary>
    /// <remarks>
    /// <para>Purpose: Confirm that unit-based helper methods route through the same identifier used by core telemetry APIs.</para>
    /// <para>Why this matters: A mismatched identifier would corrupt progress tracking and unit status reporting.</para>
    /// <para>Expected result: Starting, reporting progress for, and completing a unit emit the expected unit identifier and ratios.</para>
    /// </remarks>
    [Test]
    public void StartUnit_ReportUnitProgress_AndCompleteUnit_UseUnitIdentifier() {
        RecordingTelemetry telemetry = new();
        TestUnit unit = new("Telemetry Unit");

        telemetry.StartUnit(unit);
        telemetry.ReportUnitProgress(unit, 0.25);
        telemetry.CompleteUnit(unit);
        TestNarrative.ObserveMany(
            $"statusUpdates={string.Join(", ", telemetry.StatusUpdates.Select(x => $"{x.UnitId}:{x.Status}"))}",
            $"progressUpdates={string.Join(", ", telemetry.ProgressUpdates.Select(x => $"{x.UnitId}:{x.Ratio:0.####}"))}");

        Assert.Multiple(() => {
            Assert.That(telemetry.StatusUpdates, Is.EqualTo(new[] {
                (unit.Id, UnitStatus.Running),
                (unit.Id, UnitStatus.Succeeded)
            }));
            Assert.That(telemetry.ProgressUpdates, Is.EqualTo(new[] {
                (unit.Id, 0.25),
                (unit.Id, 1.0)
            }));
        });
    }

    /// <summary>
    /// Verifies that skip and fail helpers publish the expected terminal statuses.
    /// </summary>
    /// <remarks>
    /// <para>Purpose: Protect the mapping from convenience helpers to terminal unit states.</para>
    /// <para>Why this matters: Incorrect status forwarding would make dashboards and downstream logic report the wrong outcome.</para>
    /// <para>Expected result: Skipping a unit publishes <see cref="UnitStatus.Skipped"/>, and failing a unit publishes <see cref="UnitStatus.Failed"/>.</para>
    /// </remarks>
    [Test]
    public void SkipUnit_AndFailUnit_WriteExpectedStatuses() {
        RecordingTelemetry telemetry = new();
        Guid unitId = Guid.NewGuid();

        telemetry.SkipUnit(unitId);
        telemetry.FailUnit(unitId);
        TestNarrative.Observe($"statusUpdates={string.Join(", ", telemetry.StatusUpdates.Select(x => $"{x.UnitId}:{x.Status}"))}");

        Assert.That(telemetry.StatusUpdates, Is.EqualTo(new[] {
            (unitId, UnitStatus.Skipped),
            (unitId, UnitStatus.Failed)
        }));
    }

    /// <summary>
    /// Verifies that diagnostic helpers publish the expected severity, message, exception, and unit association.
    /// </summary>
    /// <remarks>
    /// <para>Purpose: Define the forwarding contract for informational, warning, and error diagnostics.</para>
    /// <para>Why this matters: Diagnostics lose value quickly if severity or associated unit context is dropped.</para>
    /// <para>Expected result: Each helper writes one diagnostic entry with the expected severity, text, exception, and unit identifier.</para>
    /// </remarks>
    [Test]
    public void Info_Warn_AndError_PublishExpectedDiagnostics() {
        RecordingTelemetry telemetry = new();
        Guid unitId = Guid.NewGuid();
        InvalidOperationException warningException = new("warn");
        InvalidOperationException errorException = new("error");

        telemetry.Info("info", unitId);
        telemetry.Warn("warn", warningException, unitId);
        telemetry.Error("error", errorException, unitId);

        Assert.That(telemetry.Diagnostics, Has.Count.EqualTo(3));
        TestNarrative.ObserveMany(
            $"diagnostic0={telemetry.Diagnostics[0].Severity}:{telemetry.Diagnostics[0].Message}",
            $"diagnostic1={telemetry.Diagnostics[1].Severity}:{telemetry.Diagnostics[1].Message}:{telemetry.Diagnostics[1].Exception?.Message}",
            $"diagnostic2={telemetry.Diagnostics[2].Severity}:{telemetry.Diagnostics[2].Message}:{telemetry.Diagnostics[2].Exception?.Message}");

        Assert.Multiple(() => {
            Assert.That(
                telemetry.Diagnostics[0],
                Is.EqualTo(((DiagnosticSeverity Severity, string Message, Exception? Exception, Guid? UnitId))(DiagnosticSeverity.Information, "info", null, unitId)));
            Assert.That(
                telemetry.Diagnostics[1],
                Is.EqualTo(((DiagnosticSeverity Severity, string Message, Exception? Exception, Guid? UnitId))(DiagnosticSeverity.Warning, "warn", warningException, unitId)));
            Assert.That(
                telemetry.Diagnostics[2],
                Is.EqualTo(((DiagnosticSeverity Severity, string Message, Exception? Exception, Guid? UnitId))(DiagnosticSeverity.Error, "error", errorException, unitId)));
        });
    }

    private sealed class RecordingTelemetry : IPhaseTelemetry {
        public List<(Guid UnitId, double Ratio)> ProgressUpdates { get; } = new();

        public List<(Guid UnitId, UnitStatus Status)> StatusUpdates { get; } = new();

        public List<(DiagnosticSeverity Severity, string Message, Exception? Exception, Guid? UnitId)> Diagnostics { get; } = new();

        public int? TotalUnits { get; private set; }

        public IPhaseProgressTracker BeginPhaseProgressTracking(int totalUnits) {
            return new PhaseProgressTracker(totalUnits, SetTotalUnits, _ => { }, SetUnitProgress, SetUnitStatus);
        }

        public void SetTotalUnits(int? totalUnits) {
            this.TotalUnits = totalUnits;
        }

        public void SetUnitProgress(Guid unitId, double ratio) {
            this.ProgressUpdates.Add((unitId, ratio));
        }

        public void SetUnitStatus(Guid unitId, UnitStatus status) {
            this.StatusUpdates.Add((unitId, status));
        }

        public void PublishDiagnostic(DiagnosticSeverity severity, string message, Exception? exception = null, Guid? unitId = null) {
            this.Diagnostics.Add((severity, message, exception, unitId));
        }
    }

    private sealed class TestUnit : Unit<string> {
        public TestUnit(string name) : base(name, name) {
        }
    }
}
