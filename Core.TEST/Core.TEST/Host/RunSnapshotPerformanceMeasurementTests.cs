using System.Diagnostics;
using BeltRunner.Core.Execution;
using BeltRunner.Core.Execution.Interaction;
using BeltRunner.Core.Execution.Outcome;
using BeltRunner.Core.Host;
using BeltRunner.Core.Phase;
using BeltRunner.Core.Plan;
using BeltRunner.Core.TEST.Testing;
using BeltRunner.Core.Units;
using BeltRunnerHost = BeltRunner.Core.Host.Host;

namespace BeltRunner.Core.TEST.Host;

/// <summary>
/// Measures how snapshot publication cost scales as a single phase grows in unit count.
/// </summary>
/// <remarks>
/// <para>Purpose: Provide a manual, human-readable measurement for the current cost of unit progress updates in large phases.</para>
/// <para>Why this matters: The run snapshot path still recreates every unit snapshot inside a dirty phase, so a large phase can become the dominant telemetry hot path.</para>
/// <para>Expected result: The test emits elapsed-time and allocation observations for several unit-count scenarios so a human can judge how sharply update cost scales.</para>
/// </remarks>
[TestFixture]
[Category("Performance")]
[Explicit("Manual measurement only. This test is intended to print scaling observations, not to act as a stable CI benchmark.")]
[NonParallelizable]
[TestOf(typeof(HostOptions))]
[TestOf(typeof(BeltRunnerHost))]
[TestOf(typeof(Run))]
public sealed class RunSnapshotPerformanceMeasurementTests {
    /// <summary>
    /// Measures the per-update cost of progress reporting for phases with increasing unit counts.
    /// </summary>
    /// <remarks>
    /// <para>Purpose: Quantify how much work one progress burst performs when a dirty phase contains more units.</para>
    /// <para>Why this matters: This measurement helps distinguish a harmless constant-cost update path from one that scales with phase size.</para>
    /// <para>Expected result: The test completes successfully and records elapsed time and allocated bytes for each configured unit-count scenario.</para>
    /// </remarks>
    [Test]
    public async Task Host_StartAsync_WithLargePhaseTelemetryBurst_EmitsSnapshotUpdateScalingMeasurements() {
        int[] unitCounts = [1, 128, 1024, 4096];
        const int measuredUpdateCount = 64;

        SnapshotUpdateMeasurement[] measurements = new SnapshotUpdateMeasurement[unitCounts.Length];

        for( int i = 0; i < unitCounts.Length; i++ ) {
            measurements[i] = await MeasureScenarioAsync(unitCounts[i], measuredUpdateCount);
            TestNarrative.Observe(FormatMeasurement(measurements[i]));
        }

        Assert.Multiple(() => {
            Assert.That(measurements.Select(x => x.UnitCount).ToArray(), Is.EqualTo(unitCounts));
            Assert.That(measurements.All(x => x.Outcome.Kind == RunOutcomeKind.Succeeded), Is.True);
            Assert.That(measurements.All(x => x.Elapsed > TimeSpan.Zero), Is.True);
            Assert.That(measurements.All(x => x.AllocatedBytes >= 0), Is.True);
        });
    }

    private static async Task<SnapshotUpdateMeasurement> MeasureScenarioAsync(int unitCount, int measuredUpdateCount) {
        TaskCompletionSource<SnapshotUpdateMeasurement> measurementTcs = new(TaskCreationOptions.RunContinuationsAsynchronously);

        using BeltRunnerHost host = new(new HostOptions {
            InteractionBrokerFactory = static () => new InMemoryInteractionBroker()
        });

        SequentialPlan plan = new SequentialPlanBuilder()
            .Add(new SnapshotMeasurementPhaseFactory("phase/a", unitCount, measuredUpdateCount, measurementTcs), $"Measured Phase ({unitCount} units)")
            .Build();

        using IRun run = await host.StartAsync(plan, CancellationToken.None);
        RunOutcome outcome = await run.Completion.WaitAsync(TestTimeouts.Default);
        SnapshotUpdateMeasurement measurement = await measurementTcs.Task.WaitAsync(TestTimeouts.Default);
        return measurement with { Outcome = outcome };
    }

    private static string FormatMeasurement(SnapshotUpdateMeasurement measurement) {
        double elapsedPerUpdateMilliseconds = measurement.Elapsed.TotalMilliseconds / measurement.MeasuredUpdateCount;
        double allocatedPerUpdateBytes = (double)measurement.AllocatedBytes / measurement.MeasuredUpdateCount;

        return
            $"units={measurement.UnitCount}, updates={measurement.MeasuredUpdateCount}, " +
            $"elapsed={measurement.Elapsed.TotalMilliseconds:0.###} ms, " +
            $"avg={elapsedPerUpdateMilliseconds:0.###} ms/update, " +
            $"allocated={measurement.AllocatedBytes:N0} bytes, " +
            $"avg={allocatedPerUpdateBytes:0.###} bytes/update, " +
            $"outcome={measurement.Outcome.Kind}";
    }

    private readonly record struct SnapshotUpdateMeasurement(
        int UnitCount,
        int MeasuredUpdateCount,
        TimeSpan Elapsed,
        long AllocatedBytes,
        RunOutcome Outcome);

    private sealed class SnapshotMeasurementPhaseFactory : PhaseFactoryBase {
        private readonly int unitCount;
        private readonly int measuredUpdateCount;
        private readonly TaskCompletionSource<SnapshotUpdateMeasurement> measurementTcs;

        public SnapshotMeasurementPhaseFactory(
            string key,
            int unitCount,
            int measuredUpdateCount,
            TaskCompletionSource<SnapshotUpdateMeasurement> measurementTcs) : base(key) {

            this.unitCount = unitCount;
            this.measuredUpdateCount = measuredUpdateCount;
            this.measurementTcs = measurementTcs ?? throw new ArgumentNullException(nameof(measurementTcs));
        }

        public override IPhase Create() {
            return new SnapshotMeasurementPhase(this.unitCount, this.measuredUpdateCount, this.measurementTcs);
        }
    }

    private sealed class SnapshotMeasurementPhase : IPhase {
        private readonly int measuredUpdateCount;
        private readonly TaskCompletionSource<SnapshotUpdateMeasurement> measurementTcs;
        private readonly MeasurementUnit[] units;

        public SnapshotMeasurementPhase(
            int unitCount,
            int measuredUpdateCount,
            TaskCompletionSource<SnapshotUpdateMeasurement> measurementTcs) {

            if( unitCount <= 0 ) {
                throw new ArgumentOutOfRangeException(nameof(unitCount));
            }

            if( measuredUpdateCount <= 0 ) {
                throw new ArgumentOutOfRangeException(nameof(measuredUpdateCount));
            }

            this.measuredUpdateCount = measuredUpdateCount;
            this.measurementTcs = measurementTcs ?? throw new ArgumentNullException(nameof(measurementTcs));
            this.units = Enumerable.Range(0, unitCount)
                .Select(index => new MeasurementUnit($"Measured Unit {index}"))
                .ToArray();

            this.Units.AddRangeAndLock(this.units);
        }

        public string Name => "Snapshot Measurement";

        public UnitSet Units { get; } = new();

        public Task<IPhaseOutcome> ExecuteAsync(IPhaseContext context, CancellationToken ct = default) {
            MeasurementUnit targetUnit = this.units[0];
            context.Telemetry.StartUnit(targetUnit);
            context.Telemetry.ReportUnitProgress(targetUnit, 0.01);

            GC.Collect();
            GC.WaitForPendingFinalizers();
            GC.Collect();

            long allocatedBytesBefore = GC.GetAllocatedBytesForCurrentThread();
            Stopwatch stopwatch = Stopwatch.StartNew();

            for( int i = 1; i <= this.measuredUpdateCount; i++ ) {
                double ratio = Math.Min(0.99, (double)i / (this.measuredUpdateCount + 1));
                context.Telemetry.ReportUnitProgress(targetUnit, ratio);
            }

            stopwatch.Stop();
            long allocatedBytes = GC.GetAllocatedBytesForCurrentThread() - allocatedBytesBefore;
            this.measurementTcs.TrySetResult(new SnapshotUpdateMeasurement(
                this.units.Length,
                this.measuredUpdateCount,
                stopwatch.Elapsed,
                allocatedBytes,
                RunOutcome.Succeeded()));

            context.Telemetry.CompleteUnit(targetUnit);
            return Task.FromResult<IPhaseOutcome>(new PhaseOutcome());
        }
    }

    private sealed class MeasurementUnit : Unit<string> {
        public MeasurementUnit(string name) : base(name, name) {
        }
    }
}
