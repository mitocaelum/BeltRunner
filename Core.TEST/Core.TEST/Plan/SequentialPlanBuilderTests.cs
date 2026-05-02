using BeltRunner.Core.Phase;
using BeltRunner.Core.Plan;

namespace BeltRunner.Core.TEST.Plan;

/// <summary>
/// Verifies fluent sequential-plan composition behavior.
/// </summary>
[TestFixture]
[TestOf(typeof(SequentialPlanBuilder))]
public sealed class SequentialPlanBuilderTests {
    /// <summary>
    /// Verifies that the builder preserves add order and optional node naming.
    /// </summary>
    [Test]
    public void Build_WithFactoriesAndCustomNames_PreservesSequenceAndNames() {
        SequentialPlan plan = new SequentialPlanBuilder()
            .Add(new BuilderPhaseFactory("phase/a"), "First")
            .Add(() => new BuilderPhaseFactory("phase/b"))
            .Build();

        Assert.Multiple(() => {
            Assert.That(plan.Steps, Has.Count.EqualTo(2));
            Assert.That(plan.Steps[0].Factory.Key.Value, Is.EqualTo("phase/a"));
            Assert.That(plan.Steps[0].Name, Is.EqualTo("First"));
            Assert.That(plan.Steps[1].Factory.Key.Value, Is.EqualTo("phase/b"));
            Assert.That(plan.Steps[1].Name, Is.EqualTo("phase/b"));
        });
    }

    private sealed class BuilderPhaseFactory : PhaseFactoryBase {
        public BuilderPhaseFactory(string key) : base(key) {
        }

        public override IPhase Create() {
            return new BuilderPhase();
        }
    }

    private sealed class BuilderPhase : IPhase {
        public string Name => "Builder";

        public UnitSet Units { get; } = new();

        public Task<IPhaseOutcome> ExecuteAsync(IPhaseContext context, CancellationToken ct = default) {
            return Task.FromResult<IPhaseOutcome>(new PhaseOutcome());
        }
    }
}
