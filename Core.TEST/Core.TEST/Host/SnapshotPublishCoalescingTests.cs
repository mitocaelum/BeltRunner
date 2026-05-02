using BeltRunner.Core.Execution;
using BeltRunner.Core.Execution.Interaction;
using BeltRunner.Core.Host;
using BeltRunner.Core.Phase;
using BeltRunner.Core.Plan;
using BeltRunner.Core.TEST.Testing;
using BeltRunner.Core.Units;
using BeltRunnerHost = BeltRunner.Core.Host.Host;

namespace BeltRunner.Core.TEST.Host;

/// <summary>
/// Verifies host-configured snapshot publish coalescing behavior.
/// </summary>
/// <remarks>
/// <para>Purpose: Protect the host option that throttles high-frequency snapshot publications.</para>
/// <para>Why this matters: Telemetry-heavy phases can otherwise force snapshot rebuilds and publications at an unsustainable rate.</para>
/// <para>Expected result: The option defaults to zero, zero disables coalescing, and a positive interval coalesces rapid telemetry updates into a single publication window.</para>
/// </remarks>
[TestFixture]
[TestOf(typeof(HostOptions))]
[TestOf(typeof(BeltRunnerHost))]
public sealed class SnapshotPublishCoalescingTests {
    /// <summary>
    /// Verifies that the host option defaults to zero and leaves zero as the disabled-coalescing value.
    /// </summary>
    [Test]
    public void HostOptions_SnapshotPublishCoalescingInterval_DefaultsToZero_AndZeroDisablesCoalescing() {
        HostOptions options = new();
        TestNarrative.Observe($"snapshotPublishCoalescingInterval={options.SnapshotPublishCoalescingInterval}");

        Assert.Multiple(() => {
            Assert.That(options.SnapshotPublishCoalescingInterval, Is.EqualTo(TimeSpan.Zero));
            Assert.DoesNotThrow(() => options.SnapshotPublishCoalescingInterval = TimeSpan.Zero);
            Assert.That(options.SnapshotPublishCoalescingInterval, Is.EqualTo(TimeSpan.Zero));
        });
    }

    /// <summary>
    /// Verifies that negative coalescing intervals are rejected.
    /// </summary>
    [Test]
    public void HostOptions_SnapshotPublishCoalescingInterval_WithNegativeValue_Throws() {
        HostOptions options = new();

        ArgumentOutOfRangeException ex = Assert.Throws<ArgumentOutOfRangeException>(() => options.SnapshotPublishCoalescingInterval = TimeSpan.FromMilliseconds(-1))!;
        TestNarrative.Observe($"setter rejected negative interval with paramName={ex.ParamName}");

        Assert.That(ex.ParamName, Is.EqualTo("value"));
    }

    /// <summary>
    /// Verifies that the host coalesces high-frequency telemetry snapshot publications when configured.
    /// </summary>
    [Test]
    public async Task Host_StartAsync_WithSnapshotPublishCoalescingInterval_CoalescesTelemetrySnapshotPublications() {
        ObservableRecorder<IRunSnapshot>? snapshotRecorder = null;
        TaskCompletionSource<bool> telemetryApplied = new(TaskCreationOptions.RunContinuationsAsynchronously);
        TaskCompletionSource<bool> releasePhase = new(TaskCreationOptions.RunContinuationsAsynchronously);

        using BeltRunnerHost host = new(new HostOptions {
            InteractionBrokerFactory = static () => new InMemoryInteractionBroker(),
            SnapshotPublishCoalescingInterval = TimeSpan.FromMilliseconds(120)
        });

        RunLaunchOptions options = new() {
            LifecycleCallbacks = new RunLifecycleCallbacks {
                BeforeExecutionStartAsync = run => {
                    snapshotRecorder = new ObservableRecorder<IRunSnapshot>(run.SnapshotStream);
                    return default;
                }
            }
        };

        using IRun run = await host.StartAsync(
            CreatePlan(telemetryApplied, releasePhase),
            options,
            CancellationToken.None);

        try {
            await telemetryApplied.Task.WaitAsync(TestTimeouts.Default);

            Assert.That(snapshotRecorder, Is.Not.Null);

            int snapshotCountBeforeCoalescedPublish = snapshotRecorder!.Items.Count;

            await Task.Delay(30);
            Assert.That(snapshotRecorder.Items.Count, Is.EqualTo(snapshotCountBeforeCoalescedPublish));

            await WaitUntilAsync(() => snapshotRecorder.Items.Count == snapshotCountBeforeCoalescedPublish + 1);

            IRunSnapshot latestSnapshot = snapshotRecorder.Items[^1];
            IPhaseSnapshot phaseSnapshot = latestSnapshot.Phases[0];
            IUnitSnapshot unitSnapshot = phaseSnapshot.Units[0];
            TestNarrative.ObserveMany(
                $"snapshotCountBeforeWindow={snapshotCountBeforeCoalescedPublish}",
                $"snapshotCountAfterWindow={snapshotRecorder.Items.Count}",
                $"latestSnapshotStatus={latestSnapshot.Status}",
                $"phaseStatus={phaseSnapshot.Status}",
                $"unitStatus={unitSnapshot.Status}",
                $"unitRatio={unitSnapshot.Ratio:0.####}");

            Assert.Multiple(() => {
                Assert.That(latestSnapshot.Status, Is.EqualTo(RunStatus.Running));
                Assert.That(phaseSnapshot.Status, Is.EqualTo(PhaseStatus.Running));
                Assert.That(unitSnapshot.Status, Is.EqualTo(UnitStatus.Running));
                Assert.That(unitSnapshot.Ratio, Is.EqualTo(0.9).Within(0.0001));
            });

            releasePhase.TrySetResult(true);
            await run.Completion.WaitAsync(TestTimeouts.Default);
        } finally {
            releasePhase.TrySetResult(true);
            snapshotRecorder?.Dispose();
        }
    }

    private static SequentialPlan CreatePlan(TaskCompletionSource<bool> telemetryApplied, TaskCompletionSource<bool> releasePhase) {
        CoalescingPhaseFactory factory = new("phase/a", telemetryApplied, releasePhase);
        return new SequentialPlanBuilder()
            .Add(factory, "Phase A")
            .Build();
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

    private sealed class CoalescingPhaseFactory : PhaseFactoryBase {
        private readonly TaskCompletionSource<bool> telemetryApplied;
        private readonly TaskCompletionSource<bool> releasePhase;

        public CoalescingPhaseFactory(string key, TaskCompletionSource<bool> telemetryApplied, TaskCompletionSource<bool> releasePhase) : base(key) {
            this.telemetryApplied = telemetryApplied ?? throw new ArgumentNullException(nameof(telemetryApplied));
            this.releasePhase = releasePhase ?? throw new ArgumentNullException(nameof(releasePhase));
        }

        public override IPhase Create() {
            return new CoalescingPhase(this.telemetryApplied, this.releasePhase);
        }
    }

    private sealed class CoalescingPhase : IPhase {
        private readonly TaskCompletionSource<bool> telemetryApplied;
        private readonly TaskCompletionSource<bool> releasePhase;

        public CoalescingPhase(TaskCompletionSource<bool> telemetryApplied, TaskCompletionSource<bool> releasePhase) {
            this.telemetryApplied = telemetryApplied ?? throw new ArgumentNullException(nameof(telemetryApplied));
            this.releasePhase = releasePhase ?? throw new ArgumentNullException(nameof(releasePhase));
        }

        public string Name => "Coalescing";

        public UnitSet Units { get; } = new();

        public async Task<IPhaseOutcome> ExecuteAsync(IPhaseContext context, CancellationToken ct = default) {
            SnapshotUnit unit = new("Telemetry Unit");

            this.Units.AddAndLock(unit);
            context.Telemetry.StartUnit(unit);
            context.Telemetry.ReportUnitProgress(unit, 0.2);
            context.Telemetry.ReportUnitProgress(unit, 0.9);
            this.telemetryApplied.TrySetResult(true);

            await this.releasePhase.Task.WaitAsync(TestTimeouts.Default);

            context.Telemetry.CompleteUnit(unit);
            return new PhaseOutcome();
        }
    }

    private sealed class SnapshotUnit : Unit<string> {
        public SnapshotUnit(string name) : base(name, name) {
        }
    }
}
