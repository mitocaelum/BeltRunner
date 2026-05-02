using BeltRunner.Core.Execution;
using BeltRunner.Core.Execution.Event;
using BeltRunner.Core.Execution.Interaction;
using BeltRunner.Core.Host;
using BeltRunner.Core.Phase;
using BeltRunner.Core.Plan;
using BeltRunner.Core.Plan.Artifacts;
using BeltRunner.Core.TEST.Testing;
using BeltRunner.Core.Units;
using BeltRunnerHost = BeltRunner.Core.Host.Host;

namespace BeltRunner.Core.TEST.Host;

/// <summary>
/// Verifies launch lifecycle behavior for the host entry point.
/// </summary>
/// <remarks>
/// <para>Purpose: Protect how the host initializes a run, invokes lifecycle hooks, and passes runtime context into phase execution.</para>
/// <para>Why this matters: Launch sequencing is integration-heavy, and a small ordering bug can leave extensions or seeded artifacts invisible at exactly the wrong time.</para>
/// <para>Expected result: The host exposes initialized run state before execution begins, fails early when the launch hook fails, and preserves the expected phase key in execution context.</para>
/// </remarks>
[TestFixture]
[TestOf(typeof(BeltRunnerHost))]
public sealed class RunLaunchLifecycleTests {
    /// <summary>
    /// Verifies that the pre-execution lifecycle hook can inspect an initialized run before the first event is published.
    /// </summary>
    /// <remarks>
    /// <para>Purpose: Define what state is available to <c>BeforeExecutionStartAsync</c>.</para>
    /// <para>Why this matters: Extensions often need access to seeded artifacts and snapshot structure before execution mutates the run.</para>
    /// <para>Expected result: The hook sees an initialized run with seeded artifacts and phase snapshots, while the event stream has not yet published <see cref="RunStartedEvent"/>.</para>
    /// </remarks>
    [Test]
    public async Task Host_StartAsync_BeforeExecutionStartAsync_CanObserveInitializedRunBeforeFirstEvent() {
        using BeltRunnerHost host = new(new HostOptions {
            InteractionBrokerFactory = static () => new InMemoryInteractionBroker()
        });

        ArtifactKey<string> seedKey = ArtifactSeeds.Key<string>("seed");
        SequentialPlan plan = CreatePlan(() => { }, seedKey);

        ObservableRecorder<RunEvent>? eventRecorder = null;
        ObservableRecorder<IRunSnapshot>? snapshotRecorder = null;
        int eventLogCountAtHook = -1;
        RunStatus statusAtHook = default;
        int phaseCountAtHook = -1;
        string? seededValueAtHook = null;

        RunLaunchOptions options = new() {
            LifecycleCallbacks = new RunLifecycleCallbacks {
                BeforeExecutionStartAsync = run => {
                    eventLogCountAtHook = run.EventLog.Count;
                    statusAtHook = run.Status;
                    phaseCountAtHook = run.Snapshot.Phases.Count;
                    seededValueAtHook = run.Artifacts.GetRequired(seedKey);
                    eventRecorder = new ObservableRecorder<RunEvent>(run.EventStream);
                    snapshotRecorder = new ObservableRecorder<IRunSnapshot>(run.SnapshotStream);
                    return default;
                }
            }
        };

        IRun run = await host.StartAsync(
            plan,
            [new ProducedArtifact<string>(seedKey, "seed-value")],
            options,
            CancellationToken.None);

        try {
            await run.Completion.WaitAsync(TestTimeouts.Default);

            Assert.That(eventRecorder, Is.Not.Null);
            Assert.That(snapshotRecorder, Is.Not.Null);
            TestNarrative.ObserveMany(
                $"eventLogCountAtHook={eventLogCountAtHook}",
                $"statusAtHook={statusAtHook}",
                $"phaseCountAtHook={phaseCountAtHook}",
                $"seededValueAtHook={seededValueAtHook}",
                $"firstEventType={eventRecorder!.Items[0].GetType().Name}",
                $"firstSnapshotStatus={snapshotRecorder!.Items[0].Status}");

            Assert.Multiple(() => {
                Assert.That(eventLogCountAtHook, Is.EqualTo(0));
                Assert.That(statusAtHook, Is.EqualTo(RunStatus.Created));
                Assert.That(phaseCountAtHook, Is.EqualTo(1));
                Assert.That(seededValueAtHook, Is.EqualTo("seed-value"));
                Assert.That(eventRecorder!.Items, Is.Not.Empty);
                Assert.That(eventRecorder.Items[0], Is.TypeOf<RunStartedEvent>());
                Assert.That(snapshotRecorder!.Items, Is.Not.Empty);
                Assert.That(snapshotRecorder.Items[0].Status, Is.EqualTo(RunStatus.Created));
                Assert.That(snapshotRecorder.Items[0].Phases.Count, Is.EqualTo(1));
            });
        } finally {
            eventRecorder?.Dispose();
            snapshotRecorder?.Dispose();
            run.Dispose();
        }
    }

    /// <summary>
    /// Verifies that a failing pre-execution lifecycle hook aborts startup before any phase executes.
    /// </summary>
    /// <remarks>
    /// <para>Purpose: Protect the failure policy for <c>BeforeExecutionStartAsync</c>.</para>
    /// <para>Why this matters: Startup hooks should be able to block execution when required preconditions are not met.</para>
    /// <para>Expected result: The exception is surfaced to the caller, no phase runs, and the host returns to the idle state.</para>
    /// </remarks>
    [Test]
    public async Task Host_StartAsync_WhenBeforeExecutionStartAsyncThrows_FailsBeforeExecutionAndReturnsToIdle() {
        int executionCount = 0;
        using BeltRunnerHost host = new(new HostOptions {
            InteractionBrokerFactory = static () => new InMemoryInteractionBroker()
        });
        SequentialPlan plan = CreatePlan(() => Interlocked.Increment(ref executionCount));

        RunLaunchOptions options = new() {
            LifecycleCallbacks = new RunLifecycleCallbacks {
                BeforeExecutionStartAsync = _ => new ValueTask(Task.FromException(new InvalidOperationException("before-start failed")))
            }
        };

        InvalidOperationException ex = Assert.ThrowsAsync<InvalidOperationException>(async () => await host.StartAsync(plan, options));

        await Task.Delay(100);
        TestNarrative.ObserveMany(
            $"exceptionMessage={ex.Message}",
            $"executionCount={Volatile.Read(ref executionCount)}",
            $"hostIsRunning={host.IsRunning}",
            $"hostState={host.State}");

        Assert.Multiple(() => {
            Assert.That(ex.Message, Is.EqualTo("before-start failed"));
            Assert.That(Volatile.Read(ref executionCount), Is.EqualTo(0));
            Assert.That(host.IsRunning, Is.False);
            Assert.That(host.State, Is.EqualTo(HostState.Idle));
        });
    }

    /// <summary>
    /// Verifies that the phase context receives the factory key used to build the plan.
    /// </summary>
    /// <remarks>
    /// <para>Purpose: Ensure that launch-time plan metadata survives into phase execution.</para>
    /// <para>Why this matters: Diagnostics, telemetry, and artifact scoping rely on the correct phase key at runtime.</para>
    /// <para>Expected result: The executing phase observes the same key that was assigned by the phase factory in the plan.</para>
    /// </remarks>
    [Test]
    public async Task Host_StartAsync_PassesFactoryKey_ThroughPhaseContext() {
        using BeltRunnerHost host = new(new HostOptions {
            InteractionBrokerFactory = static () => new InMemoryInteractionBroker()
        });

        PhaseKey expectedKey = new("phase/a");
        PhaseKey? observedKey = null;
        SequentialPlan plan = CreatePlan(() => { }, onExecuteWithContext: context => observedKey = context.Key);

        using IRun run = await host.StartAsync(plan, CancellationToken.None);
        await run.Completion.WaitAsync(TestTimeouts.Default);
        TestNarrative.Observe($"observedKey={observedKey}");

        Assert.That(observedKey, Is.EqualTo(expectedKey));
    }

    private static SequentialPlan CreatePlan(Action onExecute, ArtifactKey<string>? requiredSeedKey = null, Action<IPhaseContext>? onExecuteWithContext = null) {
        CallbackPhaseFactory factory = new("phase/a", onExecute, requiredSeedKey, onExecuteWithContext);
        return new SequentialPlanBuilder()
            .Add(factory, "Phase A")
            .Build();
    }

    private sealed class CallbackPhaseFactory : PhaseFactoryBase {
        private readonly Action onExecute;
        private readonly Action<IPhaseContext>? onExecuteWithContext;

        public CallbackPhaseFactory(string key, Action onExecute, ArtifactKey<string>? requiredSeedKey = null, Action<IPhaseContext>? onExecuteWithContext = null) : base(key) {
            this.onExecute = onExecute ?? throw new ArgumentNullException(nameof(onExecute));
            this.onExecuteWithContext = onExecuteWithContext;

            if( requiredSeedKey is not null ) {
                Consume(requiredSeedKey);
            }
        }

        public override IPhase Create() {
            return new CallbackPhase(this.onExecute, this.onExecuteWithContext);
        }
    }

    private sealed class CallbackPhase : IPhase {
        private readonly Action onExecute;
        private readonly Action<IPhaseContext>? onExecuteWithContext;

        public CallbackPhase(Action onExecute, Action<IPhaseContext>? onExecuteWithContext = null) {
            this.onExecute = onExecute ?? throw new ArgumentNullException(nameof(onExecute));
            this.onExecuteWithContext = onExecuteWithContext;
        }

        public string Name => "Callback";

        public UnitSet Units { get; } = new();

        public Task<IPhaseOutcome> ExecuteAsync(IPhaseContext context, CancellationToken ct = default) {
            this.onExecute();
            this.onExecuteWithContext?.Invoke(context);
            return Task.FromResult<IPhaseOutcome>(new PhaseOutcome());
        }
    }

    private sealed class TestUnit : IUnit {
        public Guid Id { get; } = Guid.NewGuid();
        public string Name { get; } = "Test Unit";
        public UnitStatus Status => UnitStatus.Pending;
        public PhaseKey? CurrentPhaseKey => null;
        public System.Collections.Generic.IReadOnlyCollection<UnitTag> Tags { get; } = Array.Empty<UnitTag>();
    }
}
