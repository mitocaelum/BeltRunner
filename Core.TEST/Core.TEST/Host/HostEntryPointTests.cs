using BeltRunner.Core.Execution;
using BeltRunner.Core.Execution.Interaction;
using BeltRunner.Core.Execution.Outcome;
using BeltRunner.Core.Host;
using BeltRunner.Core.Phase;
using BeltRunner.Core.Plan.Artifacts;

namespace BeltRunner.Core.TEST.Host;

/// <summary>
/// Verifies the convenience entry points for host construction and run startup.
/// </summary>
[TestFixture]
[TestOf(typeof(HostBuilder))]
[TestOf(typeof(HostStartExtensions))]
public sealed class HostEntryPointTests {
    /// <summary>
    /// Verifies that the sequential-start convenience overload builds the plan, seeds initial artifacts, and completes successfully.
    /// </summary>
    [Test]
    public async Task StartSequentialAsync_WithArtifacts_CompletesAndSeedsArtifacts() {
        ArtifactKey<int> inputKey = ArtifactSeeds.Key<int>("inputValue");
        ArtifactKey<int> outputKey = ArtifactSeeds.Key<int>("outputValue");

        using IHost host = new HostBuilder()
            .UseInteractionBrokerFactory(static () => new InMemoryInteractionBroker())
            .Build();

        using IRun run = await host.StartSequentialAsync(
            plan => plan.Add(new TransformPhaseFactory(inputKey, outputKey)),
            artifacts => artifacts.Add(inputKey, 41));

        RunOutcome outcome = await run.Completion.WaitAsync(TestTimeouts.Default);

        Assert.Multiple(() => {
            Assert.That(outcome.Kind, Is.EqualTo(RunOutcomeKind.Succeeded));
            Assert.That(run.Artifacts.GetRequired(outputKey), Is.EqualTo(42));
        });
    }

    /// <summary>
    /// Verifies that the host builder applies fault-policy configuration to created hosts.
    /// </summary>
    [Test]
    public async Task HostBuilder_WhenFaultOnFailedOutcomeIsFalse_CompletesHostWithoutFaulting() {
        using IHost host = new HostBuilder()
            .UseInteractionBrokerFactory(static () => new InMemoryInteractionBroker())
            .FaultOnFailedOutcome(false)
            .Build();

        using IRun run = await host.StartSequentialAsync(plan => plan.Add(new FailedPhaseFactory()));
        RunOutcome outcome = await run.Completion.WaitAsync(TestTimeouts.Default);

        await WaitUntilAsync(() => host.State == HostState.Completed || host.State == HostState.Faulted);

        Assert.Multiple(() => {
            Assert.That(outcome.Kind, Is.EqualTo(RunOutcomeKind.Failed));
            Assert.That(host.State, Is.EqualTo(HostState.Completed));
        });
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

    private sealed class TransformPhaseFactory : PhaseFactoryBase {
        private readonly ArtifactKey<int> inputKey;
        private readonly ArtifactKey<int> outputKey;

        public TransformPhaseFactory(ArtifactKey<int> inputKey, ArtifactKey<int> outputKey) : base("phase/transform") {
            this.inputKey = Consume(inputKey);
            this.outputKey = Produce(outputKey);
        }

        public override IPhase Create() {
            return new TransformPhase(this.inputKey, this.outputKey);
        }
    }

    private sealed class TransformPhase : IPhase {
        private readonly ArtifactKey<int> inputKey;
        private readonly ArtifactKey<int> outputKey;

        public TransformPhase(ArtifactKey<int> inputKey, ArtifactKey<int> outputKey) {
            this.inputKey = inputKey;
            this.outputKey = outputKey;
        }

        public string Name => "Transform";

        public UnitSet Units { get; } = new();

        public Task<IPhaseOutcome> ExecuteAsync(IPhaseContext context, CancellationToken ct = default) {
            int value = context.Artifacts.GetRequired(this.inputKey);
            return Task.FromResult<IPhaseOutcome>(new PhaseOutcome().Produce(this.outputKey, value + 1));
        }
    }

    private sealed class FailedPhaseFactory : PhaseFactoryBase {
        public FailedPhaseFactory() : base("phase/failed") {
        }

        public override IPhase Create() {
            return new FailedPhase();
        }
    }

    private sealed class FailedPhase : IPhase {
        public string Name => "Failed";

        public UnitSet Units { get; } = new();

        public Task<IPhaseOutcome> ExecuteAsync(IPhaseContext context, CancellationToken ct = default) {
            return Task.FromResult<IPhaseOutcome>(new PhaseOutcome().FailedAndHalt());
        }
    }
}
