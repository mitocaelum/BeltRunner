using BeltRunner.Core.Execution;
using BeltRunner.Core.Execution.Event;
using BeltRunner.Core.Execution.Interaction;
using BeltRunner.Core.Host;
using BeltRunner.Core.Phase;
using BeltRunner.Core.Plan;
using BeltRunner.Core.TEST.Testing;
using BeltRunner.Core.Units;
using BeltRunnerHost = BeltRunner.Core.Host.Host;

namespace BeltRunner.Core.TEST.Host;

/// <summary>
/// Verifies high-volume execution scenarios that protect against avoidable in-memory performance regressions.
/// </summary>
/// <remarks>
/// <para>Purpose: Keep the host stable when telemetry, event history, or diagnostic volume increases sharply.</para>
/// <para>Why this matters: Performance regressions often first appear as runaway publication counts or retained history that grows beyond configured bounds.</para>
/// <para>Expected result: Snapshot coalescing suppresses bursty telemetry publication, and configured retention limits cap replayable event and diagnostic history under heavier loads.</para>
/// </remarks>
[TestFixture]
[Category("Performance")]
[TestOf(typeof(HostOptions))]
[TestOf(typeof(BeltRunnerHost))]
[TestOf(typeof(Run))]
public sealed class HostPerformanceRegressionTests {
    /// <summary>
    /// Verifies that snapshot coalescing suppresses publication growth during a dense telemetry burst.
    /// </summary>
    /// <remarks>
    /// <para>Purpose: Protect the coalescing option from degrading into near one-publication-per-update behavior.</para>
    /// <para>Why this matters: Telemetry-heavy phases can otherwise overwhelm observers with snapshot churn and unnecessary allocations.</para>
    /// <para>Expected result: A coalesced run completes with the same final state as an uncoalesced run while emitting dramatically fewer snapshots.</para>
    /// </remarks>
    [Test]
    public async Task Host_StartAsync_WithSnapshotPublishCoalescingInterval_DuringTelemetryBurst_PublishesFarFewerSnapshots() {
        const int progressUpdateCount = 256;

        SnapshotBurstMetrics uncoalesced = await ExecuteTelemetryBurstRunAsync(progressUpdateCount, TimeSpan.Zero);
        SnapshotBurstMetrics coalesced = await ExecuteTelemetryBurstRunAsync(progressUpdateCount, TimeSpan.FromMilliseconds(40));

        IRunSnapshot finalSnapshot = coalesced.FinalSnapshot;
        IPhaseSnapshot finalPhase = finalSnapshot.Phases[0];
        IUnitSnapshot finalUnit = finalPhase.Units[0];
        TestNarrative.ObserveMany(
            $"uncoalescedSnapshotCount={uncoalesced.SnapshotCount}",
            $"coalescedSnapshotCount={coalesced.SnapshotCount}",
            $"finalRunStatus={finalSnapshot.Status}",
            $"finalPhaseStatus={finalPhase.Status}",
            $"finalUnitStatus={finalUnit.Status}",
            $"finalUnitRatio={finalUnit.Ratio:0.####}");

        Assert.Multiple(() => {
            Assert.That(uncoalesced.SnapshotCount, Is.GreaterThan(progressUpdateCount));
            Assert.That(coalesced.SnapshotCount, Is.LessThan(uncoalesced.SnapshotCount / 20));
            Assert.That(coalesced.SnapshotCount, Is.LessThanOrEqualTo(10));
            Assert.That(finalSnapshot.Status, Is.EqualTo(RunStatus.Completed));
            Assert.That(finalPhase.Status, Is.EqualTo(PhaseStatus.Completed));
            Assert.That(finalUnit.Status, Is.EqualTo(UnitStatus.Succeeded));
            Assert.That(finalUnit.Ratio, Is.EqualTo(1.0).Within(0.0001));
        });
    }

    /// <summary>
    /// Verifies that a tight event-log retention limit still caps replayable history during a larger multi-phase run.
    /// </summary>
    /// <remarks>
    /// <para>Purpose: Protect event replay cost when plans contain many short-lived phases.</para>
    /// <para>Why this matters: A retention regression can quietly turn a long but simple run into an ever-growing in-memory history.</para>
    /// <para>Expected result: Only the newest retained events remain in both the event log and late-subscriber replay stream.</para>
    /// </remarks>
    [Test]
    public async Task Host_StartAsync_WithRunEventRetentionLimit_DuringLargePlan_RetainsOnlyNewestReplayableEvents() {
        const int phaseCount = 20;
        const int retentionLimit = 9;

        using BeltRunnerHost host = new(new HostOptions {
            InteractionBrokerFactory = static () => new InMemoryInteractionBroker(),
            RunEventLogMaxRetainedCount = retentionLimit
        });

        using IRun run = await host.StartAsync(CreateCompletedPlan(phaseCount), CancellationToken.None);
        await run.Completion.WaitAsync(TestTimeouts.Default);

        using ObservableRecorder<RunEvent> recorder = new(run.EventStream);
        TerminalSignal terminal = await recorder.WaitForTerminalAsync(TestTimeouts.Default);

        string[] retainedEvents = run.EventLog.Select(DescribeRunEvent).ToArray();
        string[] replayedEvents = recorder.Items.Select(DescribeRunEvent).ToArray();
        TestNarrative.ObserveMany(
            $"terminal={terminal}",
            $"retainedEvents={string.Join(", ", retainedEvents)}",
            $"replayedEvents={string.Join(", ", replayedEvents)}");

        Assert.Multiple(() => {
            Assert.That(terminal, Is.EqualTo(TerminalSignal.Completed));
            Assert.That(retainedEvents, Is.EqualTo(new[] {
                "PhaseStarted:16",
                "PhaseCompleted:16",
                "PhaseStarted:17",
                "PhaseCompleted:17",
                "PhaseStarted:18",
                "PhaseCompleted:18",
                "PhaseStarted:19",
                "PhaseCompleted:19",
                "RunCompleted"
            }));
            Assert.That(replayedEvents, Is.EqualTo(retainedEvents));
        });
    }

    /// <summary>
    /// Verifies that diagnostic retention remains bounded when a phase emits a large warning burst.
    /// </summary>
    /// <remarks>
    /// <para>Purpose: Protect diagnostic replay and retained memory usage under diagnostic-heavy execution.</para>
    /// <para>Why this matters: Warning storms are a common source of accidental retention growth even when execution still succeeds.</para>
    /// <para>Expected result: The run retains and replays only the newest configured diagnostics after a larger diagnostic burst.</para>
    /// </remarks>
    [Test]
    public async Task Host_StartAsync_WithDiagnosticRetentionLimit_DuringDiagnosticBurst_RetainsOnlyNewestReplayableDiagnostics() {
        const int diagnosticCount = 200;
        const int retentionLimit = 16;

        using BeltRunnerHost host = new(new HostOptions {
            InteractionBrokerFactory = static () => new InMemoryInteractionBroker(),
            RunDiagnosticsMaxRetainedCount = retentionLimit
        });

        using IRun run = await host.StartAsync(CreateDiagnosticBurstPlan(diagnosticCount), CancellationToken.None);
        await run.Completion.WaitAsync(TestTimeouts.Default);

        using ObservableRecorder<IDiagnosticEntry> recorder = new(run.DiagnosticStream);
        TerminalSignal terminal = await recorder.WaitForTerminalAsync(TestTimeouts.Default);

        string[] retainedMessages = run.DiagnosticLog.Select(x => x.Message).ToArray();
        string[] replayedMessages = recorder.Items.Select(x => x.Message).ToArray();
        string[] expectedMessages = Enumerable.Range(diagnosticCount - retentionLimit, retentionLimit)
            .Select(index => $"diag-{index}")
            .ToArray();
        TestNarrative.ObserveMany(
            $"terminal={terminal}",
            $"retainedDiagnostics={string.Join(", ", retainedMessages)}",
            $"replayedDiagnostics={string.Join(", ", replayedMessages)}");

        Assert.Multiple(() => {
            Assert.That(terminal, Is.EqualTo(TerminalSignal.Completed));
            Assert.That(retainedMessages, Is.EqualTo(expectedMessages));
            Assert.That(replayedMessages, Is.EqualTo(expectedMessages));
        });
    }

    private static async Task<SnapshotBurstMetrics> ExecuteTelemetryBurstRunAsync(int progressUpdateCount, TimeSpan coalescingInterval) {
        ObservableRecorder<IRunSnapshot>? snapshotRecorder = null;

        using BeltRunnerHost host = new(new HostOptions {
            InteractionBrokerFactory = static () => new InMemoryInteractionBroker(),
            SnapshotPublishCoalescingInterval = coalescingInterval
        });

        RunLaunchOptions options = new() {
            LifecycleCallbacks = new RunLifecycleCallbacks {
                BeforeExecutionStartAsync = run => {
                    snapshotRecorder = new ObservableRecorder<IRunSnapshot>(run.SnapshotStream);
                    return default;
                }
            }
        };

        using IRun run = await host.StartAsync(CreateTelemetryBurstPlan(progressUpdateCount), options, CancellationToken.None);

        try {
            await run.Completion.WaitAsync(TestTimeouts.Default);

            Assert.That(snapshotRecorder, Is.Not.Null);
            await WaitUntilAsync(() => snapshotRecorder!.Items.Count > 0 && snapshotRecorder.Items[^1].Status == RunStatus.Completed);

            return new SnapshotBurstMetrics(snapshotRecorder.Items.Count, snapshotRecorder.Items[^1]);
        } finally {
            snapshotRecorder?.Dispose();
        }
    }

    private static SequentialPlan CreateTelemetryBurstPlan(int progressUpdateCount) {
        return new SequentialPlanBuilder()
            .Add(new TelemetryBurstPhaseFactory("phase/a", progressUpdateCount), "Phase A")
            .Build();
    }

    private static SequentialPlan CreateCompletedPlan(int phaseCount) {
        SequentialPlanBuilder builder = new();
        foreach( int index in Enumerable.Range(0, phaseCount) ) {
            builder.Add(new CompletedPhaseFactory($"phase/{index}"), $"Phase {index}");
        }

        return builder.Build();
    }

    private static SequentialPlan CreateDiagnosticBurstPlan(int diagnosticCount) {
        return new SequentialPlanBuilder()
            .Add(new DiagnosticBurstPhaseFactory("phase/a", diagnosticCount), "Phase A")
            .Build();
    }

    private static string DescribeRunEvent(RunEvent ev) {
        return ev switch {
            PhaseStartedEvent phaseStarted => $"PhaseStarted:{phaseStarted.PhaseIndex}",
            PhaseCompletedEvent phaseCompleted => $"PhaseCompleted:{phaseCompleted.PhaseIndex}",
            RunCompletedEvent => "RunCompleted",
            RunStartedEvent => "RunStarted",
            RunCancelledEvent => "RunCancelled",
            RunFaultedEvent => "RunFaulted",
            _ => ev.GetType().Name
        };
    }

    private static async Task WaitUntilAsync(Func<bool> condition) {
        DateTimeOffset deadline = DateTimeOffset.UtcNow.Add(TestTimeouts.Default);

        while( DateTimeOffset.UtcNow < deadline ) {
            if( condition() ) {
                return;
            }

            await Task.Delay(20);
        }

        Assert.Fail("Timed out while waiting for the expected state.");
    }

    private readonly record struct SnapshotBurstMetrics(int SnapshotCount, IRunSnapshot FinalSnapshot);

    private sealed class TelemetryBurstPhaseFactory : PhaseFactoryBase {
        private readonly int progressUpdateCount;

        public TelemetryBurstPhaseFactory(string key, int progressUpdateCount) : base(key) {
            this.progressUpdateCount = progressUpdateCount;
        }

        public override IPhase Create() {
            return new TelemetryBurstPhase(this.progressUpdateCount);
        }
    }

    private sealed class TelemetryBurstPhase : IPhase {
        private readonly int progressUpdateCount;
        private readonly BurstUnit unit = new("Burst Unit");

        public TelemetryBurstPhase(int progressUpdateCount) {
            this.progressUpdateCount = progressUpdateCount;
            this.Units.AddAndLock(this.unit);
        }

        public string Name => "Telemetry Burst";

        public UnitSet Units { get; } = new();

        public Task<IPhaseOutcome> ExecuteAsync(IPhaseContext context, CancellationToken ct = default) {
            context.Telemetry.StartUnit(this.unit);

            for( int i = 1; i <= this.progressUpdateCount; i++ ) {
                context.Telemetry.ReportUnitProgress(this.unit, (double)i / this.progressUpdateCount);
            }

            context.Telemetry.CompleteUnit(this.unit);
            return Task.FromResult<IPhaseOutcome>(new PhaseOutcome());
        }
    }

    private sealed class CompletedPhaseFactory : PhaseFactoryBase {
        public CompletedPhaseFactory(string key) : base(key) {
        }

        public override IPhase Create() {
            return new CompletedPhase();
        }
    }

    private sealed class CompletedPhase : IPhase {
        public string Name => "Completed";

        public UnitSet Units { get; } = new();

        public Task<IPhaseOutcome> ExecuteAsync(IPhaseContext context, CancellationToken ct = default) {
            return Task.FromResult<IPhaseOutcome>(new PhaseOutcome());
        }
    }

    private sealed class DiagnosticBurstPhaseFactory : PhaseFactoryBase {
        private readonly int diagnosticCount;

        public DiagnosticBurstPhaseFactory(string key, int diagnosticCount) : base(key) {
            this.diagnosticCount = diagnosticCount;
        }

        public override IPhase Create() {
            return new DiagnosticBurstPhase(this.diagnosticCount);
        }
    }

    private sealed class DiagnosticBurstPhase : IPhase {
        private readonly int diagnosticCount;
        private readonly BurstUnit unit = new("Diagnostic Unit");

        public DiagnosticBurstPhase(int diagnosticCount) {
            this.diagnosticCount = diagnosticCount;
            this.Units.AddAndLock(this.unit);
        }

        public string Name => "Diagnostics";

        public UnitSet Units { get; } = new();

        public Task<IPhaseOutcome> ExecuteAsync(IPhaseContext context, CancellationToken ct = default) {
            for( int i = 0; i < this.diagnosticCount; i++ ) {
                context.Telemetry.Warn($"diag-{i}", unitId: this.unit.Id);
            }

            return Task.FromResult<IPhaseOutcome>(new PhaseOutcome());
        }
    }

    private sealed class BurstUnit : Unit<string> {
        public BurstUnit(string name) : base(name, name) {
        }
    }
}
