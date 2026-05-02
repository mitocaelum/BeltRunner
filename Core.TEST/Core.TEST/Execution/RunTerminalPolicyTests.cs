using BeltRunner.Core.Execution;
using BeltRunner.Core.Execution.Event;
using BeltRunner.Core.Execution.Interaction;
using BeltRunner.Core.Execution.Outcome;
using BeltRunner.Core.Host;
using BeltRunner.Core.TEST.Testing;

namespace BeltRunner.Core.TEST.Execution;

/// <summary>
/// Verifies terminal behavior for <see cref="Run"/> when disposal is initiated by the owner.
/// </summary>
/// <remarks>
/// <para>Purpose: Protect the shutdown contract for event streams, snapshot streams, and completion state.</para>
/// <para>Why this matters: Disposal often happens in cleanup paths, and those paths must settle the run consistently without leaking faults.</para>
/// <para>Expected result: Disposing a run produces a stable cancelled outcome, emits one terminal notification per stream, and tolerates callback failures.</para>
/// </remarks>
[TestFixture]
[TestOf(typeof(Run))]
public sealed class RunTerminalPolicyTests {
    /// <summary>
    /// Verifies that disposing an unsettled run completes it with a cancelled outcome.
    /// </summary>
    /// <remarks>
    /// <para>Purpose: Define how a run settles when ownership ends before normal completion.</para>
    /// <para>Why this matters: Cleanup logic needs a deterministic outcome instead of a hanging task or an unobserved fault.</para>
    /// <para>Expected result: The completion task finishes successfully with a <see cref="RunOutcome"/> whose kind is <see cref="RunOutcomeKind.Cancelled"/>, and cancellation state is visible on the run.</para>
    /// </remarks>
    [Test]
    public async Task Dispose_BeforeSettlement_CompletesAsCancelledOutcome() {
        Run run = new(new InMemoryInteractionBroker());

        Assert.That(run.Completion.IsCompleted, Is.False);

        run.Dispose();

        RunOutcome outcome = await run.Completion.WaitAsync(TestTimeouts.Default);
        TestNarrative.ObserveMany(
            $"outcomeKind={outcome.Kind}",
            $"isCancellationRequested={run.IsCancellationRequested}",
            $"cancelReason={run.CancelReason}",
            $"outcomeReason={outcome.CancellationReason}");

        Assert.Multiple(() => {
            Assert.That(outcome.Kind, Is.EqualTo(RunOutcomeKind.Cancelled));
            Assert.That(run.Completion.IsFaulted, Is.False);
            Assert.That(run.IsCancellationRequested, Is.True);
        });

        Assert.That(outcome.CancellationReason, Does.Contain("disposed").IgnoreCase);
        Assert.That(run.CancelReason, Does.Contain("disposed").IgnoreCase);
    }

    /// <summary>
    /// Verifies that a newly created run does not expose a cancellation reason before cancellation is requested.
    /// </summary>
    /// <remarks>
    /// <para>Purpose: Define the default cancellation surface for a newly created run.</para>
    /// <para>Why this matters: Callers should be able to distinguish between an unset reason and an explicit cancellation reason.</para>
    /// <para>Expected result: <see cref="Run.CancelReason"/> is <see langword="null"/> before cancellation is requested.</para>
    /// </remarks>
    [Test]
    public void CancelReason_BeforeCancellation_IsNull() {
        using Run run = new(new InMemoryInteractionBroker());
        TestNarrative.Observe($"cancelReason={(run.CancelReason is null ? "null" : run.CancelReason)}");

        Assert.That(run.CancelReason, Is.Null);
    }

    /// <summary>
    /// Verifies that requesting cancellation without a reason keeps <see cref="Run.CancelReason"/> unset.
    /// </summary>
    /// <remarks>
    /// <para>Purpose: Protect the distinction between cancellation and cancellation-with-message.</para>
    /// <para>Why this matters: Some callers depend on <see langword="null"/> meaning that no human-readable reason was supplied.</para>
    /// <para>Expected result: Cancellation is requested and <see cref="Run.CancelReason"/> remains <see langword="null"/>.</para>
    /// </remarks>
    [Test]
    public void RequestCancellation_WithoutReason_LeavesCancelReasonNull() {
        using Run run = new(new InMemoryInteractionBroker());

        run.RequestCancellation();
        TestNarrative.ObserveMany(
            $"isCancellationRequested={run.IsCancellationRequested}",
            $"cancelReason={(run.CancelReason is null ? "null" : run.CancelReason)}");

        Assert.Multiple(() => {
            Assert.That(run.IsCancellationRequested, Is.True);
            Assert.That(run.CancelReason, Is.Null);
        });
    }

    /// <summary>
    /// Verifies that an explicitly empty cancellation reason is preserved distinctly from an unset reason.
    /// </summary>
    /// <remarks>
    /// <para>Purpose: Preserve the semantic difference between an empty supplied reason and no supplied reason.</para>
    /// <para>Why this matters: Consumers may round-trip or display an explicit empty string differently from a missing value.</para>
    /// <para>Expected result: Cancellation is requested and <see cref="Run.CancelReason"/> equals <see cref="string.Empty"/>.</para>
    /// </remarks>
    [Test]
    public void RequestCancellation_WithExplicitEmptyReason_PreservesEmptyString() {
        using Run run = new(new InMemoryInteractionBroker());

        run.RequestCancellation(string.Empty);
        TestNarrative.ObserveMany(
            $"isCancellationRequested={run.IsCancellationRequested}",
            $"cancelReasonLength={run.CancelReason?.Length ?? -1}");

        Assert.Multiple(() => {
            Assert.That(run.IsCancellationRequested, Is.True);
            Assert.That(run.CancelReason, Is.EqualTo(string.Empty));
        });
    }

    /// <summary>
    /// Verifies that control characters are removed from cancellation reasons exposed on the run.
    /// </summary>
    /// <remarks>
    /// <para>Purpose: Protect the public cancellation reason surface from unsafe control characters.</para>
    /// <para>Why this matters: Cancellation reasons can flow into logs and user-visible diagnostics.</para>
    /// <para>Expected result: The stored cancellation reason keeps the visible text while removing carriage returns, newlines, tabs, and escape characters.</para>
    /// </remarks>
    [Test]
    public void RequestCancellation_WithControlCharacters_SanitizesReason() {
        using Run run = new(new InMemoryInteractionBroker());

        run.RequestCancellation("line1\r\nline2\t\u001bline3");

        Assert.That(run.CancelReason, Is.Not.Null);
        TestNarrative.Observe($"sanitized cancelReason={run.CancelReason}");
        Assert.Multiple(() => {
            Assert.That(run.CancelReason, Does.Contain("line1"));
            Assert.That(run.CancelReason, Does.Contain("line2"));
            Assert.That(run.CancelReason, Does.Contain("line3"));
            Assert.That(run.CancelReason, Does.Not.Contain("\r"));
            Assert.That(run.CancelReason, Does.Not.Contain("\n"));
            Assert.That(run.CancelReason, Does.Not.Contain("\t"));
            Assert.That(run.CancelReason!.IndexOf('\u001b'), Is.EqualTo(-1));
        });
    }

    /// <summary>
    /// Verifies that the event stream completes once without reporting an error during disposal.
    /// </summary>
    /// <remarks>
    /// <para>Purpose: Protect the terminal semantics of <see cref="Run.EventStream"/> during shutdown.</para>
    /// <para>Why this matters: Observers must be able to treat disposal as a clean end-of-stream condition.</para>
    /// <para>Expected result: The recorder observes a single completed terminal signal, no error, and no duplicate terminal notifications.</para>
    /// </remarks>
    [Test]
    public async Task Events_Stream_Completes_OnDispose_WithoutError() {
        Run run = new(new InMemoryInteractionBroker());
        using ObservableRecorder<RunEvent> recorder = new(run.EventStream);

        run.Dispose();

        TerminalSignal terminal = await recorder.WaitForTerminalAsync(TestTimeouts.Default);
        TestNarrative.ObserveMany(
            $"terminal={terminal}",
            $"isCompleted={recorder.IsCompleted}",
            $"error={(recorder.Error is null ? "null" : recorder.Error.GetType().Name)}",
            $"terminalCount={recorder.TerminalCount}");

        Assert.Multiple(() => {
            Assert.That(terminal, Is.EqualTo(TerminalSignal.Completed));
            Assert.That(recorder.IsCompleted, Is.True);
            Assert.That(recorder.Error, Is.Null);
            Assert.That(recorder.TerminalCount, Is.EqualTo(1));
        });
    }

    /// <summary>
    /// Verifies that the snapshot stream completes once without reporting an error during disposal.
    /// </summary>
    /// <remarks>
    /// <para>Purpose: Ensure that snapshot subscribers receive a clean terminal signal when the run is disposed.</para>
    /// <para>Why this matters: Tooling that renders snapshots should not have to special-case disposal as a stream fault.</para>
    /// <para>Expected result: The snapshot recorder completes successfully, reports no error, and retains emitted items for inspection.</para>
    /// </remarks>
    [Test]
    public async Task Snapshots_Stream_Completes_OnDispose_WithoutError() {
        Run run = new(new InMemoryInteractionBroker());
        using ObservableRecorder<IRunSnapshot> recorder = new(run.SnapshotStream);

        run.Dispose();

        TerminalSignal terminal = await recorder.WaitForTerminalAsync(TestTimeouts.Default);
        TestNarrative.ObserveMany(
            $"terminal={terminal}",
            $"isCompleted={recorder.IsCompleted}",
            $"error={(recorder.Error is null ? "null" : recorder.Error.GetType().Name)}",
            $"terminalCount={recorder.TerminalCount}",
            $"snapshotCount={recorder.Items.Count}");

        Assert.Multiple(() => {
            Assert.That(terminal, Is.EqualTo(TerminalSignal.Completed));
            Assert.That(recorder.IsCompleted, Is.True);
            Assert.That(recorder.Error, Is.Null);
            Assert.That(recorder.TerminalCount, Is.EqualTo(1));
            Assert.That(recorder.Items, Is.Not.Empty);
        });
    }

    /// <summary>
    /// Verifies that repeated disposal still produces only one terminal notification and one cancelled completion outcome.
    /// </summary>
    /// <remarks>
    /// <para>Purpose: Define disposal idempotency for terminal signaling.</para>
    /// <para>Why this matters: Cleanup code may call <see cref="IDisposable.Dispose"/> more than once, and the run should remain stable.</para>
    /// <para>Expected result: The stream completes exactly once and the run still settles as a cancelled outcome without faulting completion.</para>
    /// </remarks>
    [Test]
    public async Task Dispose_CalledTwice_EmitsSingleTerminalSignal_AndCancelledCompletion() {
        Run run = new(new InMemoryInteractionBroker());
        using ObservableRecorder<RunEvent> recorder = new(run.EventStream);

        run.Dispose();
        run.Dispose();

        TerminalSignal terminal = await recorder.WaitForTerminalAsync(TestTimeouts.Default);
        RunOutcome outcome = await run.Completion.WaitAsync(TestTimeouts.Default);
        TestNarrative.ObserveMany(
            $"terminal={terminal}",
            $"terminalCount={recorder.TerminalCount}",
            $"outcomeKind={outcome.Kind}",
            $"completionFaulted={run.Completion.IsFaulted}");

        Assert.Multiple(() => {
            Assert.That(terminal, Is.EqualTo(TerminalSignal.Completed));
            Assert.That(recorder.Error, Is.Null);
            Assert.That(recorder.TerminalCount, Is.EqualTo(1));
            Assert.That(run.Completion.IsFaulted, Is.False);
            Assert.That(outcome.Kind, Is.EqualTo(RunOutcomeKind.Cancelled));
        });
    }

    /// <summary>
    /// Verifies that disposal runs the supplemental completion callback before the disposal callback and only once.
    /// </summary>
    /// <remarks>
    /// <para>Purpose: Protect the ordering contract between <c>OnCompletedAsync</c> and <c>BeforeRunDisposeAsync</c> when disposal settles the run.</para>
    /// <para>Why this matters: Cleanup integrations may need terminal data first and teardown cleanup second, even when disposal itself causes cancellation.</para>
    /// <para>Expected result: Disposal settles completion, invokes <c>OnCompletedAsync</c> once, then invokes <c>BeforeRunDisposeAsync</c> once.</para>
    /// </remarks>
    [Test]
    public async Task Dispose_Invokes_OnCompletedAsync_Before_BeforeRunDisposeAsync_Once() {
        List<string> callOrder = new();
        object callOrderGate = new();
        bool completionCompletedAtOnCompleted = false;
        bool completionCompletedAtBeforeDispose = false;

        RunLifecycleCallbacks callbacks = new() {
            OnCompletedAsync = run => {
                lock( callOrderGate ) {
                    callOrder.Add("OnCompletedAsync");
                }

                completionCompletedAtOnCompleted = run.Completion.IsCompleted;
                return default;
            },
            BeforeRunDisposeAsync = run => {
                lock( callOrderGate ) {
                    callOrder.Add("BeforeRunDisposeAsync");
                }

                completionCompletedAtBeforeDispose = run.Completion.IsCompleted;
                return default;
            }
        };

        Run run = new(new InMemoryInteractionBroker(), new RunConfiguration {
            LifecycleCallbacks = callbacks
        });

        run.Dispose();
        RunOutcome outcome = await run.Completion.WaitAsync(TestTimeouts.Default);
        TestNarrative.ObserveMany(
            $"callOrder={string.Join(" -> ", callOrder)}",
            $"completionCompletedAtOnCompleted={completionCompletedAtOnCompleted}",
            $"completionCompletedAtBeforeDispose={completionCompletedAtBeforeDispose}",
            $"outcomeKind={outcome.Kind}");

        Assert.Multiple(() => {
            Assert.That(callOrder, Has.Count.EqualTo(2));
            Assert.That(callOrder, Is.EqualTo(new[] { "OnCompletedAsync", "BeforeRunDisposeAsync" }));
            Assert.That(completionCompletedAtOnCompleted, Is.True);
            Assert.That(completionCompletedAtBeforeDispose, Is.True);
            Assert.That(outcome.Kind, Is.EqualTo(RunOutcomeKind.Cancelled));
        });
    }

    /// <summary>
    /// Verifies that disposal continues even when the disposal callback throws.
    /// </summary>
    /// <remarks>
    /// <para>Purpose: Define the fault-tolerance policy for disposal callbacks.</para>
    /// <para>Why this matters: Cleanup hooks must not be able to strand the run in an incomplete state.</para>
    /// <para>Expected result: Disposal does not throw to the caller, terminal signals still complete normally, and the run still settles as cancelled.</para>
    /// </remarks>
    [Test]
    public async Task Dispose_WhenBeforeRunDisposeAsyncThrows_SwallowsAndContinues() {
        RunLifecycleCallbacks callbacks = new() {
            BeforeRunDisposeAsync = _ => new ValueTask(Task.FromException(new InvalidOperationException("dispose failed")))
        };

        Run run = new(new InMemoryInteractionBroker(), new RunConfiguration {
            LifecycleCallbacks = callbacks
        });
        using ObservableRecorder<RunEvent> recorder = new(run.EventStream);

        Assert.DoesNotThrow(() => run.Dispose());

        TerminalSignal terminal = await recorder.WaitForTerminalAsync(TestTimeouts.Default);
        RunOutcome outcome = await run.Completion.WaitAsync(TestTimeouts.Default);
        TestNarrative.ObserveMany(
            $"terminal={terminal}",
            $"outcomeKind={outcome.Kind}",
            $"error={(recorder.Error is null ? "null" : recorder.Error.GetType().Name)}");

        Assert.Multiple(() => {
            Assert.That(terminal, Is.EqualTo(TerminalSignal.Completed));
            Assert.That(outcome.Kind, Is.EqualTo(RunOutcomeKind.Cancelled));
            Assert.That(recorder.Error, Is.Null);
        });
    }

    /// <summary>
    /// Verifies that a completion and disposal race still invokes the supplemental completion callback at most once.
    /// </summary>
    /// <remarks>
    /// <para>Purpose: Protect the callback idempotency contract when terminal completion and disposal compete.</para>
    /// <para>Why this matters: Real owners can dispose a run at nearly the same moment that execution settles, and duplicate completion callbacks would be a subtle integration bug.</para>
    /// <para>Expected result: Across repeated races, the callback is invoked exactly once per run and the completion task remains publicly settled.</para>
    /// </remarks>
    [Test]
    public async Task Completion_And_Dispose_Race_Invokes_OnCompletedAsync_AtMostOnce() {
        for( int iteration = 0; iteration < 25; iteration++ ) {
            TaskCompletionSource<object?> releaseCallback = new(TaskCreationOptions.RunContinuationsAsynchronously);
            int callbackCount = 0;

            Run run = new(new InMemoryInteractionBroker(), new RunConfiguration {
                LifecycleCallbacks = new RunLifecycleCallbacks {
                    OnCompletedAsync = async _ => {
                        Interlocked.Increment(ref callbackCount);
                        await releaseCallback.Task.ConfigureAwait(false);
                    }
                }
            });

            Task completeTask = Task.Run(() => run.TryComplete(RunOutcome.Succeeded()));
            Task disposeTask = Task.Run(() => run.Dispose());

            await Task.Delay(10);
            releaseCallback.TrySetResult(null);

            await Task.WhenAll(completeTask, disposeTask).WaitAsync(TestTimeouts.Default);
            RunOutcome outcome = await run.Completion.WaitAsync(TestTimeouts.Default);
            TestNarrative.ObserveMany(
                $"iteration={iteration}",
                $"callbackCount={callbackCount}",
                $"outcomeKind={outcome.Kind}");

            Assert.That(callbackCount, Is.EqualTo(1), $"iteration={iteration} outcomeKind={outcome.Kind}");
        }
    }
}
