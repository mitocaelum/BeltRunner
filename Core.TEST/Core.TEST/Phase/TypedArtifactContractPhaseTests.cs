using BeltRunner.Core.Execution;
using BeltRunner.Core.Execution.Interaction;
using BeltRunner.Core.Execution.Outcome;
using BeltRunner.Core.Host;
using BeltRunner.Core.Phase;
using BeltRunner.Core.Plan;
using BeltRunner.Core.Plan.Artifacts;
using BeltRunner.Core.TEST.Testing;
using BeltRunner.Core.Units;
using BeltRunnerHost = BeltRunner.Core.Host.Host;

namespace BeltRunner.Core.TEST.Phase;

/// <summary>
/// Verifies the typed artifact-contract authoring path for phases.
/// </summary>
/// <remarks>
/// <para>Purpose: Protect the new generic factory, phase, and context APIs that remove redundant artifact-key constructor injection.</para>
/// <para>Why this matters: The typed factory path is now the recommended phase-authoring surface, so both the bridge and the fallback behavior must stay reliable.</para>
/// <para>Expected result: Generic phases receive their factory metadata at runtime, non-generic phases still execute, and mismatched contexts fail fast.</para>
/// </remarks>
[TestFixture]
[TestOf(typeof(PhaseFactoryBase<>))]
[TestOf(typeof(PhaseBase<>))]
[TestOf(typeof(IPhaseContext<>))]
[TestOf(typeof(IPhaseFactory<>))]
public sealed class TypedArtifactContractPhaseTests {
    /// <summary>
    /// Verifies that a generic phase receives the typed factory metadata at runtime.
    /// </summary>
    [Test]
    public async Task Host_StartAsync_PassesTypedFactoryMetadata_ToGenericPhase() {
        using BeltRunnerHost host = new(new HostOptions {
            InteractionBrokerFactory = static () => new InMemoryInteractionBroker()
        });

        TypedContractPhaseFactory factory = new();
        SequentialPlan plan = new SequentialPlanBuilder()
            .Add(factory, "Typed Contract")
            .Build();

        using IRun run = await host.StartAsync(
            plan,
            [new ProducedArtifact<string>(factory.InputKey, "seed-value")],
            CancellationToken.None);

        RunOutcome outcome = await run.Completion.WaitAsync(TestTimeouts.Default);
        string output = run.Artifacts.GetRequired(factory.OutputKey);

        TestNarrative.ObserveMany(
            $"phaseSawFactory={factory.PhaseObservedFactory}",
            $"output={output}",
            $"inputName={factory.InputKey.Name}",
            $"outputName={factory.OutputKey.Name}");

        Assert.Multiple(() => {
            Assert.That(factory.PhaseObservedFactory, Is.True);
            Assert.That(output, Is.EqualTo("seed-value-processed"));
            Assert.That(outcome.IsSuccessful, Is.True);
        });
    }

    /// <summary>
    /// Verifies that the legacy non-generic authoring path continues to execute after typed-contract support is added.
    /// </summary>
    [Test]
    public async Task Host_StartAsync_StillExecutes_LegacyPhaseFactoryPath() {
        using BeltRunnerHost host = new(new HostOptions {
            InteractionBrokerFactory = static () => new InMemoryInteractionBroker()
        });

        bool executed = false;
        SequentialPlan plan = new SequentialPlanBuilder()
            .Add(new LegacyPhaseFactory(() => executed = true), "Legacy")
            .Build();

        using IRun run = await host.StartAsync(plan, CancellationToken.None);
        RunOutcome outcome = await run.Completion.WaitAsync(TestTimeouts.Default);

        TestNarrative.Observe($"executed={executed}");

        Assert.Multiple(() => {
            Assert.That(executed, Is.True);
            Assert.That(outcome.IsSuccessful, Is.True);
        });
    }

    /// <summary>
    /// Verifies that the generic phase bridge fails fast when the runtime context does not carry the expected typed factory metadata.
    /// </summary>
    [Test]
    public void IPhase_ExecuteAsync_Throws_WhenTypedFactoryContextIsMissing() {
        TypedContractPhase phase = new(() => { });
        ArtifactStore artifacts = new();
        using InMemoryInteractionBroker broker = new();
        IPhaseTelemetry telemetry = new NoOpPhaseTelemetry();
        IPhaseContext context = new PhaseContext(new PhaseKey("typed"), CancellationToken.None, artifacts, broker, telemetry);

        InvalidOperationException ex = Assert.ThrowsAsync<InvalidOperationException>(async () =>
            await ((IPhase)phase).ExecuteAsync(context, CancellationToken.None));

        TestNarrative.Observe($"exceptionMessage={ex!.Message}");
        Assert.That(ex.Message, Does.Contain("non-matching typed context"));
    }

    private sealed class TypedContractPhaseFactory : PhaseFactoryBase<TypedContractPhaseFactory> {
        public TypedContractPhaseFactory() : base("typed-contract") {
            InputKey = Consume<string>("input");
            OutputKey = Produce<string>("output");
        }

        public override IPhase Create() {
            return new TypedContractPhase(() => PhaseObservedFactory = true);
        }

        public ArtifactKey<string> InputKey { get; }
        public ArtifactKey<string> OutputKey { get; }
        public bool PhaseObservedFactory { get; private set; }
    }

    private sealed class TypedContractPhase : PhaseBase<TypedContractPhaseFactory> {
        private readonly Action onObservedFactory;

        public TypedContractPhase(Action onObservedFactory) {
            this.onObservedFactory = onObservedFactory;
        }

        public override string Name => "Typed Contract";

        public override UnitSet Units { get; } = new();

        public override Task<IPhaseOutcome> ExecuteAsync(IPhaseContext<TypedContractPhaseFactory> context, CancellationToken ct = default) {
            this.onObservedFactory();
            string input = context.Artifacts.GetRequired(context.Factory.InputKey);

            IPhaseOutcome outcome = new PhaseOutcome()
                .Produce(context.Factory.OutputKey, $"{input}-processed");

            return Task.FromResult(outcome);
        }
    }

    private sealed class LegacyPhaseFactory : PhaseFactoryBase {
        private readonly Action onExecute;

        public LegacyPhaseFactory(Action onExecute) : base("legacy") {
            this.onExecute = onExecute;
        }

        public override IPhase Create() {
            return new LegacyPhase(this.onExecute);
        }
    }

    private sealed class LegacyPhase : IPhase {
        private readonly Action onExecute;

        public LegacyPhase(Action onExecute) {
            this.onExecute = onExecute;
        }

        public string Name => "Legacy";

        public UnitSet Units { get; } = new();

        public Task<IPhaseOutcome> ExecuteAsync(IPhaseContext context, CancellationToken ct = default) {
            this.onExecute();
            return Task.FromResult<IPhaseOutcome>(new PhaseOutcome());
        }
    }

    private sealed class NoOpPhaseTelemetry : IPhaseTelemetry {
        public IPhaseProgressTracker BeginPhaseProgressTracking(int totalUnits) {
            return new NoOpPhaseProgressTracker();
        }

        public void PublishDiagnostic(BeltRunner.Core.Execution.DiagnosticSeverity severity, string message, Exception? exception = null, Guid? unitId = null) {
        }

        public void SetTotalUnits(int? totalUnits) {
        }

        public void SetUnitProgress(Guid unitId, double ratio) {
        }

        public void SetUnitStatus(Guid unitId, UnitStatus status) {
        }
    }

    private sealed class NoOpPhaseProgressTracker : IPhaseProgressTracker {
        public ITrackedUnitScope BeginUnit(Guid unitId) {
            return new NoOpTrackedUnitScope();
        }

        public ITrackedUnitScope BeginUnit(IUnit unit) {
            return new NoOpTrackedUnitScope();
        }

        public void Dispose() {
        }

        public void ReportCompleted(int completedUnits) {
        }
    }

    private sealed class NoOpTrackedUnitScope : ITrackedUnitScope {
        public void Complete() {
        }

        public void Dispose() {
        }
    }
}
