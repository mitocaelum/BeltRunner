using BeltRunner.Core.Execution;
using BeltRunner.Core.Execution.Event;
using BeltRunner.Core.Execution.Interaction;
using BeltRunner.Core.Execution.Outcome;
using BeltRunner.Core.Host;
using BeltRunner.Core.Phase;
using BeltRunner.Core.Plan;
using BeltRunner.Core.Plan.Artifacts;
using BeltRunner.Core.TEST.Testing;
using BeltRunnerHost = BeltRunner.Core.Host.Host;

namespace BeltRunner.Core.TEST.Host;

/// <summary>
/// Verifies the supplemental completion callback exposed through <see cref="RunLifecycleCallbacks"/>.
/// </summary>
/// <remarks>
/// <para>Purpose: Protect the contract for <c>OnCompletedAsync</c> across terminal outcomes, callback timing, and failure handling.</para>
/// <para>Why this matters: Consumers need a reliable run-scoped hook that complements <see cref="IRun.Completion"/> without changing the public completion model.</para>
/// <para>Expected result: The callback runs once per run after completion is settled, preserves the public outcome when it fails, and remains usable even when callers do not await <see cref="IRun.Completion"/> themselves.</para>
/// </remarks>
[TestFixture]
[TestOf(typeof(RunLifecycleCallbacks))]
public sealed class RunCompletionLifecycleCallbackTests {
    /// <summary>
    /// Verifies that the supplemental completion callback runs exactly once for every terminal run outcome.
    /// </summary>
    /// <remarks>
    /// <para>Purpose: Define the one-shot invocation contract for <c>OnCompletedAsync</c> across success, partial success, failure, fault, and cancellation.</para>
    /// <para>Why this matters: Integrations that emit metrics or detach external resources need a stable callback count regardless of how the run ended.</para>
    /// <para>Expected result: Each terminal scenario invokes the callback once, and the callback observes an already-completed <see cref="IRun.Completion"/> with the final outcome kind.</para>
    /// </remarks>
    [TestCase(RunOutcomeKind.Succeeded)]
    [TestCase(RunOutcomeKind.PartiallySucceeded)]
    [TestCase(RunOutcomeKind.Failed)]
    [TestCase(RunOutcomeKind.Faulted)]
    [TestCase(RunOutcomeKind.Cancelled)]
    public async Task Host_StartAsync_OnCompletedAsync_InvokesOnce_ForEachTerminalOutcome(RunOutcomeKind expectedKind) {
        using BeltRunnerHost host = CreateHost();
        TaskCompletionSource<object?> callbackSettled = new(TaskCreationOptions.RunContinuationsAsynchronously);
        int callbackCount = 0;
        bool completionCompletedAtCallback = false;
        RunOutcomeKind observedKindAtCallback = default;

        RunLaunchOptions options = new() {
            LifecycleCallbacks = new RunLifecycleCallbacks {
                OnCompletedAsync = async run => {
                    Interlocked.Increment(ref callbackCount);
                    completionCompletedAtCallback = run.Completion.IsCompleted;
                    observedKindAtCallback = (await run.Completion.ConfigureAwait(false)).Kind;
                    callbackSettled.TrySetResult(null);
                }
            }
        };

        using IRun run = await host.StartAsync(CreatePlanForOutcome(expectedKind), options, CancellationToken.None);

        RunOutcome outcome = await run.Completion.WaitAsync(TestTimeouts.Default);
        await callbackSettled.Task.WaitAsync(TestTimeouts.Default);
        TestNarrative.ObserveMany(
            $"expectedKind={expectedKind}",
            $"outcomeKind={outcome.Kind}",
            $"callbackCount={callbackCount}",
            $"completionCompletedAtCallback={completionCompletedAtCallback}",
            $"observedKindAtCallback={observedKindAtCallback}");

        Assert.Multiple(() => {
            Assert.That(outcome.Kind, Is.EqualTo(expectedKind));
            Assert.That(callbackCount, Is.EqualTo(1));
            Assert.That(completionCompletedAtCallback, Is.True);
            Assert.That(observedKindAtCallback, Is.EqualTo(expectedKind));
        });
    }

    /// <summary>
    /// Verifies that the supplemental completion callback can inspect terminal run state after settlement.
    /// </summary>
    /// <remarks>
    /// <para>Purpose: Define which retained run surfaces remain available inside <c>OnCompletedAsync</c>.</para>
    /// <para>Why this matters: Completion hooks are useful only if they can still read the snapshot, artifacts, and retained diagnostics that describe the finished run.</para>
    /// <para>Expected result: The callback can read <see cref="IRun.Completion"/>, <see cref="IRun.Snapshot"/>, <see cref="IRun.Artifacts"/>, <see cref="IRun.DiagnosticLog"/>, and <see cref="IRun.EventLog"/> without racing disposal.</para>
    /// </remarks>
    [Test]
    public async Task Host_StartAsync_OnCompletedAsync_CanReadTerminalRunState() {
        using BeltRunnerHost host = CreateHost();
        ArtifactKey<string> producedKey = ArtifactSeeds.Key<string>("result");
        TaskCompletionSource<object?> callbackSettled = new(TaskCreationOptions.RunContinuationsAsynchronously);

        bool completionCompletedAtCallback = false;
        RunStatus snapshotStatusAtCallback = default;
        int phaseCountAtCallback = -1;
        string? artifactValueAtCallback = null;
        string? diagnosticMessageAtCallback = null;
        int eventLogCountAtCallback = -1;
        string? lastEventTypeAtCallback = null;
        RunOutcomeKind observedKindAtCallback = default;

        RunLaunchOptions options = new() {
            LifecycleCallbacks = new RunLifecycleCallbacks {
                OnCompletedAsync = async run => {
                    completionCompletedAtCallback = run.Completion.IsCompleted;
                    observedKindAtCallback = (await run.Completion.ConfigureAwait(false)).Kind;
                    snapshotStatusAtCallback = run.Snapshot.Status;
                    phaseCountAtCallback = run.Snapshot.Phases.Count;
                    artifactValueAtCallback = run.Artifacts.GetRequired(producedKey);
                    diagnosticMessageAtCallback = run.DiagnosticLog.Count > 0 ? run.DiagnosticLog[0].Message : null;
                    eventLogCountAtCallback = run.EventLog.Count;
                    lastEventTypeAtCallback = eventLogCountAtCallback > 0
                        ? run.EventLog[eventLogCountAtCallback - 1].GetType().Name
                        : null;
                    callbackSettled.TrySetResult(null);
                }
            }
        };

        using IRun run = await host.StartAsync(CreateSuccessPlanWithArtifactAndDiagnostic(producedKey), options, CancellationToken.None);

        RunOutcome outcome = await run.Completion.WaitAsync(TestTimeouts.Default);
        await callbackSettled.Task.WaitAsync(TestTimeouts.Default);
        TestNarrative.ObserveMany(
            $"outcomeKind={outcome.Kind}",
            $"completionCompletedAtCallback={completionCompletedAtCallback}",
            $"snapshotStatusAtCallback={snapshotStatusAtCallback}",
            $"phaseCountAtCallback={phaseCountAtCallback}",
            $"artifactValueAtCallback={artifactValueAtCallback}",
            $"diagnosticMessageAtCallback={diagnosticMessageAtCallback}",
            $"eventLogCountAtCallback={eventLogCountAtCallback}",
            $"lastEventTypeAtCallback={lastEventTypeAtCallback}");

        Assert.Multiple(() => {
            Assert.That(outcome.Kind, Is.EqualTo(RunOutcomeKind.Succeeded));
            Assert.That(observedKindAtCallback, Is.EqualTo(RunOutcomeKind.Succeeded));
            Assert.That(completionCompletedAtCallback, Is.True);
            Assert.That(snapshotStatusAtCallback, Is.EqualTo(RunStatus.Completed));
            Assert.That(phaseCountAtCallback, Is.EqualTo(1));
            Assert.That(artifactValueAtCallback, Is.EqualTo("artifact-value"));
            Assert.That(diagnosticMessageAtCallback, Is.EqualTo("phase-info"));
            Assert.That(eventLogCountAtCallback, Is.GreaterThan(0));
            Assert.That(lastEventTypeAtCallback, Is.EqualTo(nameof(RunCompletedEvent)));
        });
    }

    /// <summary>
    /// Verifies that callback failures do not change the already-settled public run outcome.
    /// </summary>
    /// <remarks>
    /// <para>Purpose: Protect the failure isolation contract for <c>OnCompletedAsync</c>.</para>
    /// <para>Why this matters: Supplemental callbacks must not be able to turn a completed run into a public failure just because post-processing failed.</para>
    /// <para>Expected result: The callback may throw, but <see cref="IRun.Completion"/> still completes successfully with the original outcome kind and the host observes the run as completed.</para>
    /// </remarks>
    [Test]
    public async Task Host_StartAsync_WhenOnCompletedAsyncThrows_PreservesOutcomeAndPublicCompletion() {
        using BeltRunnerHost host = CreateHost();
        TaskCompletionSource<object?> callbackEntered = new(TaskCreationOptions.RunContinuationsAsynchronously);
        TaskCompletionSource<object?> callbackSettled = new(TaskCreationOptions.RunContinuationsAsynchronously);

        RunLaunchOptions options = new() {
            LifecycleCallbacks = new RunLifecycleCallbacks {
                OnCompletedAsync = async _ => {
                    callbackEntered.TrySetResult(null);

                    try {
                        await Task.Yield();
                        throw new InvalidOperationException("on-completed failed");
                    } finally {
                        callbackSettled.TrySetResult(null);
                    }
                }
            }
        };

        using IRun run = await host.StartAsync(CreatePlanForOutcome(RunOutcomeKind.Succeeded), options, CancellationToken.None);

        RunOutcome outcome = await run.Completion.WaitAsync(TestTimeouts.Default);
        await callbackEntered.Task.WaitAsync(TestTimeouts.Default);
        await callbackSettled.Task.WaitAsync(TestTimeouts.Default);
        TestNarrative.ObserveMany(
            $"outcomeKind={outcome.Kind}",
            $"completionFaulted={run.Completion.IsFaulted}",
            $"hostState={host.State}");

        Assert.Multiple(() => {
            Assert.That(outcome.Kind, Is.EqualTo(RunOutcomeKind.Succeeded));
            Assert.That(run.Completion.IsFaulted, Is.False);
            Assert.That(host.State, Is.EqualTo(HostState.Completed));
        });
    }

    /// <summary>
    /// Verifies that the supplemental completion callback still runs when callers never await <see cref="IRun.Completion"/>.
    /// </summary>
    /// <remarks>
    /// <para>Purpose: Define the callback trigger independently from caller behavior.</para>
    /// <para>Why this matters: Some callers may rely entirely on callbacks or event subscriptions and never await the completion task themselves.</para>
    /// <para>Expected result: The callback still runs and observes the final outcome even if the test waits only on callback-owned synchronization.</para>
    /// </remarks>
    [Test]
    public async Task Host_StartAsync_OnCompletedAsync_RunsWithoutAwaitingCompletion() {
        using BeltRunnerHost host = CreateHost();
        TaskCompletionSource<RunOutcomeKind> callbackObservedKind = new(TaskCreationOptions.RunContinuationsAsynchronously);

        RunLaunchOptions options = new() {
            LifecycleCallbacks = new RunLifecycleCallbacks {
                OnCompletedAsync = async run => {
                    RunOutcome outcome = await run.Completion.ConfigureAwait(false);
                    callbackObservedKind.TrySetResult(outcome.Kind);
                }
            }
        };

        using IRun run = await host.StartAsync(CreatePlanForOutcome(RunOutcomeKind.Succeeded), options, CancellationToken.None);

        RunOutcomeKind observedKind = await callbackObservedKind.Task.WaitAsync(TestTimeouts.Default);
        TestNarrative.ObserveMany(
            $"observedKind={observedKind}",
            $"completionIsCompleted={run.Completion.IsCompleted}");

        Assert.Multiple(() => {
            Assert.That(observedKind, Is.EqualTo(RunOutcomeKind.Succeeded));
            Assert.That(run.Completion.IsCompleted, Is.True);
        });
    }

    private static BeltRunnerHost CreateHost() {
        return new BeltRunnerHost(new HostOptions {
            InteractionBrokerFactory = static () => new InMemoryInteractionBroker()
        });
    }

    private static SequentialPlan CreatePlanForOutcome(RunOutcomeKind outcomeKind) {
        return outcomeKind switch {
            RunOutcomeKind.Succeeded => CreatePlan(static (_, _) => Task.FromResult<IPhaseOutcome>(new PhaseOutcome())),
            RunOutcomeKind.PartiallySucceeded => CreatePlan(static (_, _) => Task.FromResult<IPhaseOutcome>(new PhaseOutcome(PhaseResult.PartiallySucceeded))),
            RunOutcomeKind.Failed => CreatePlan(static (_, _) => Task.FromResult<IPhaseOutcome>(new PhaseOutcome().FailedAndHalt())),
            RunOutcomeKind.Faulted => CreatePlan(static (_, _) => throw new InvalidOperationException("phase fault")),
            RunOutcomeKind.Cancelled => CreatePlan(static (_, _) => Task.FromResult<IPhaseOutcome>(new PhaseOutcome(PhaseResult.Cancelled))),
            _ => throw new ArgumentOutOfRangeException(nameof(outcomeKind))
        };
    }

    private static SequentialPlan CreateSuccessPlanWithArtifactAndDiagnostic(ArtifactKey<string> producedKey) {
        return CreatePlan((context, _) => {
            context.Telemetry.Info("phase-info");
            IPhaseOutcome outcome = new PhaseOutcome().Produce(producedKey, "artifact-value");
            return Task.FromResult(outcome);
        }, producedKey);
    }

    private static SequentialPlan CreatePlan(Func<IPhaseContext, CancellationToken, Task<IPhaseOutcome>> executeAsync, ArtifactKey<string>? producedKey = null) {
        CallbackPhaseFactory factory = new("phase/a", executeAsync, producedKey);
        return new SequentialPlanBuilder()
            .Add(factory, "Phase A")
            .Build();
    }

    private sealed class CallbackPhaseFactory : PhaseFactoryBase {
        private readonly Func<IPhaseContext, CancellationToken, Task<IPhaseOutcome>> executeAsync;

        public CallbackPhaseFactory(string key, Func<IPhaseContext, CancellationToken, Task<IPhaseOutcome>> executeAsync, ArtifactKey<string>? producedKey = null) : base(key) {
            this.executeAsync = executeAsync ?? throw new ArgumentNullException(nameof(executeAsync));

            if( producedKey is not null ) {
                Produce(producedKey);
            }
        }

        public override IPhase Create() {
            return new CallbackPhase(this.executeAsync);
        }
    }

    private sealed class CallbackPhase : IPhase {
        private readonly Func<IPhaseContext, CancellationToken, Task<IPhaseOutcome>> executeAsync;

        public CallbackPhase(Func<IPhaseContext, CancellationToken, Task<IPhaseOutcome>> executeAsync) {
            this.executeAsync = executeAsync ?? throw new ArgumentNullException(nameof(executeAsync));
        }

        public string Name { get; } = "Callback";

        public UnitSet Units { get; } = new();

        public Task<IPhaseOutcome> ExecuteAsync(IPhaseContext context, CancellationToken ct = default) {
            return this.executeAsync(context, ct);
        }
    }
}
