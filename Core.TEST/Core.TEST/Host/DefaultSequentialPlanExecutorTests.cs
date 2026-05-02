using BeltRunner.Core.Execution;
using BeltRunner.Core.Execution.Interaction;
using BeltRunner.Core.Execution.Outcome;
using BeltRunner.Core.Host;
using BeltRunner.Core.Phase;
using BeltRunner.Core.Plan;
using BeltRunner.Core.TEST.Testing;

namespace BeltRunner.Core.TEST.Host;

/// <summary>
/// Verifies outcome mapping behavior in <see cref="DefaultSequentialPlanExecutor"/>.
/// </summary>
/// <remarks>
/// <para>Purpose: Protect how phase outcomes are translated into run outcomes and downstream execution decisions.</para>
/// <para>Why this matters: The sequential executor is the control point that decides whether the plan continues, halts, or changes the final run status.</para>
/// <para>Expected result: Partial success, failure, and cancellation reported by a phase are converted into the correct run outcome and execution flow.</para>
/// </remarks>
[TestFixture]
[TestOf(typeof(DefaultSequentialPlanExecutor))]
public sealed class DefaultSequentialPlanExecutorTests {
    /// <summary>
    /// Verifies that a partially succeeded phase completes the run as partially succeeded while still allowing downstream execution.
    /// </summary>
    /// <remarks>
    /// <para>Purpose: Define how partial success propagates through sequential execution.</para>
    /// <para>Why this matters: Partial success is informative but not terminal, so the executor must preserve both the outcome and continued execution.</para>
    /// <para>Expected result: The run finishes with a <see cref="RunOutcome"/> whose kind is <see cref="RunOutcomeKind.PartiallySucceeded"/>, the downstream phase runs once, and the final run status is completed.</para>
    /// </remarks>
    [Test]
    public async Task ExecuteAsync_WhenPhaseReportsPartiallySucceeded_CompletesRunAsPartiallySucceededOutcome() {
        DefaultSequentialPlanExecutor executor = new();
        int secondPhaseExecutionCount = 0;
        SequentialPlan plan = CreatePlan(
            new OutcomePhaseFactory("phase/a", () => new PhaseOutcome().WithResult(PhaseResult.PartiallySucceeded)),
            new OutcomePhaseFactory("phase/b", () => new PhaseOutcome(), () => secondPhaseExecutionCount++));

        using Run run = await ExecuteAsync(executor, plan);
        RunOutcome outcome = await run.Completion.WaitAsync(TestTimeouts.Default);
        TestNarrative.ObserveMany(
            $"outcomeKind={outcome.Kind}",
            $"secondPhaseExecutionCount={secondPhaseExecutionCount}",
            $"runStatus={run.Status}");

        Assert.Multiple(() => {
            Assert.That(outcome.Kind, Is.EqualTo(RunOutcomeKind.PartiallySucceeded));
            Assert.That(secondPhaseExecutionCount, Is.EqualTo(1));
            Assert.That(outcome.Summary, Does.Contain("firstPhaseKey=\"phase/a\""));
            Assert.That(run.Status, Is.EqualTo(RunStatus.Completed));
        });
    }

    /// <summary>
    /// Verifies that a failed phase configured to halt stops downstream execution.
    /// </summary>
    /// <remarks>
    /// <para>Purpose: Protect the executor halt rule for terminal phase failures.</para>
    /// <para>Why this matters: Running later phases after a halting failure can compound damage and hide the original error boundary.</para>
    /// <para>Expected result: The run completes with a <see cref="RunOutcome"/> whose kind is <see cref="RunOutcomeKind.Failed"/>, the downstream phase does not execute, and the failure summary points at the first failing phase.</para>
    /// </remarks>
    [Test]
    public async Task ExecuteAsync_WhenPhaseFailsAndHalts_DoesNotRunDownstreamPhase() {
        DefaultSequentialPlanExecutor executor = new();
        int secondPhaseExecutionCount = 0;
        SequentialPlan plan = CreatePlan(
            new OutcomePhaseFactory("phase/a", () => new PhaseOutcome().FailedAndHalt()),
            new OutcomePhaseFactory("phase/b", () => new PhaseOutcome(), () => secondPhaseExecutionCount++));

        using Run run = await ExecuteAsync(executor, plan);
        RunOutcome outcome = await run.Completion.WaitAsync(TestTimeouts.Default);
        TestNarrative.ObserveMany(
            $"outcomeKind={outcome.Kind}",
            $"secondPhaseExecutionCount={secondPhaseExecutionCount}",
            $"runStatus={run.Status}");

        Assert.Multiple(() => {
            Assert.That(outcome.Kind, Is.EqualTo(RunOutcomeKind.Failed));
            Assert.That(secondPhaseExecutionCount, Is.EqualTo(0));
            Assert.That(outcome.Summary, Does.Contain("firstPhaseKey=\"phase/a\""));
            Assert.That(run.Status, Is.EqualTo(RunStatus.Completed));
        });
    }

    /// <summary>
    /// Verifies that a cancelled phase completes the run as cancelled and stops downstream execution.
    /// </summary>
    /// <remarks>
    /// <para>Purpose: Define cancellation propagation within sequential execution.</para>
    /// <para>Why this matters: Cancellation should stop further work while still producing a stable and inspectable outcome.</para>
    /// <para>Expected result: The run finishes with a <see cref="RunOutcome"/> whose kind is <see cref="RunOutcomeKind.Cancelled"/>, the downstream phase does not execute, and the run status becomes cancelled.</para>
    /// </remarks>
    [Test]
    public async Task ExecuteAsync_WhenPhaseReportsCancelled_CompletesRunAsCancelledOutcome() {
        DefaultSequentialPlanExecutor executor = new();
        int secondPhaseExecutionCount = 0;
        SequentialPlan plan = CreatePlan(
            new OutcomePhaseFactory("phase/a", () => new PhaseOutcome().WithResult(PhaseResult.Cancelled)),
            new OutcomePhaseFactory("phase/b", () => new PhaseOutcome(), () => secondPhaseExecutionCount++));

        using Run run = await ExecuteAsync(executor, plan);
        RunOutcome outcome = await run.Completion.WaitAsync(TestTimeouts.Default);
        TestNarrative.ObserveMany(
            $"outcomeKind={outcome.Kind}",
            $"secondPhaseExecutionCount={secondPhaseExecutionCount}",
            $"cancelReason={(run.CancelReason is null ? "null" : run.CancelReason)}",
            $"runStatus={run.Status}");

        Assert.Multiple(() => {
            Assert.That(outcome.Kind, Is.EqualTo(RunOutcomeKind.Cancelled));
            Assert.That(secondPhaseExecutionCount, Is.EqualTo(0));
            Assert.That(outcome.CancellationReason, Is.Null);
            Assert.That(run.CancelReason, Is.Null);
            Assert.That(run.Status, Is.EqualTo(RunStatus.Cancelled));
        });
    }

    private static SequentialPlan CreatePlan(params OutcomePhaseFactory[] factories) {
        SequentialPlanBuilder builder = new();
        for( int i = 0; i < factories.Length; i++ ) {
            builder.Add(factories[i], $"Phase {i + 1}");
        }

        return builder.Build();
    }

    private static async Task<Run> ExecuteAsync(DefaultSequentialPlanExecutor executor, SequentialPlan plan) {
        Run run = new(new InMemoryInteractionBroker());

        executor.Preflight(plan, Array.Empty<BeltRunner.Core.Plan.Artifacts.IProducedArtifact>());
        run.InitializeRuntimeState(plan.Steps);
        run.ActivateExecution();
        await executor.ExecuteAsync(plan, run, CancellationToken.None);

        return run;
    }

    private sealed class OutcomePhaseFactory : PhaseFactoryBase {
        private readonly Func<IPhaseOutcome> createOutcome;
        private readonly Action? onExecute;

        public OutcomePhaseFactory(string key, Func<IPhaseOutcome> createOutcome, Action? onExecute = null) : base(key) {
            this.createOutcome = createOutcome ?? throw new ArgumentNullException(nameof(createOutcome));
            this.onExecute = onExecute;
        }

        public override IPhase Create() {
            return new OutcomePhase(this.createOutcome, this.onExecute);
        }
    }

    private sealed class OutcomePhase : IPhase {
        private readonly Func<IPhaseOutcome> createOutcome;
        private readonly Action? onExecute;

        public OutcomePhase(Func<IPhaseOutcome> createOutcome, Action? onExecute) {
            this.createOutcome = createOutcome ?? throw new ArgumentNullException(nameof(createOutcome));
            this.onExecute = onExecute;
        }

        public string Name => "Outcome";

        public UnitSet Units { get; } = new();

        public Task<IPhaseOutcome> ExecuteAsync(IPhaseContext context, CancellationToken ct = default) {
            this.onExecute?.Invoke();
            return Task.FromResult(this.createOutcome());
        }
    }
}
