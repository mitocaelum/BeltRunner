using BeltRunner.Core.Phase;
using BeltRunner.Core.Plan.Artifacts;
using BeltRunner.Core.TEST.Testing;
using BeltRunner.Core.Units;

namespace BeltRunner.Core.TEST.Phase;

/// <summary>
/// Verifies the list-oriented artifact helper APIs exposed by <see cref="PhaseFactoryBase"/>.
/// </summary>
/// <remarks>
/// <para>Purpose: Protect the typed helper methods that create consumed, produced, and shared list artifact keys.</para>
/// <para>Why this matters: The helpers are convenience APIs, but they still define naming and typing conventions that downstream phases depend on.</para>
/// <para>Expected result: Each helper creates the expected logical name and list value type, and shared keys can be created without additional prefixes.</para>
/// </remarks>
[TestFixture]
[TestOf(typeof(PhaseFactoryBase))]
public sealed class PhaseFactoryBaseListHelpersTests {
    /// <summary>
    /// Verifies that list consume and produce helpers declare the expected keys and registrations.
    /// </summary>
    /// <remarks>
    /// <para>Purpose: Confirm that helper-generated keys preserve both logical names and list value typing.</para>
    /// <para>Why this matters: Incorrect registrations would break artifact wiring between phases while still looking superficially valid.</para>
    /// <para>Expected result: The factory exposes the expected key names, list value types, and consume or produce registrations.</para>
    /// </remarks>
    [Test]
    public void ConsumeList_AndProduceList_DeclareExpectedKeys() {
        ListHelperFactory factory = new();
        TestNarrative.ObserveMany(
            $"consumedName={factory.Consumed.Name}",
            $"producedName={factory.Produced.Name}",
            $"sharedName={factory.Shared.Name}",
            $"consumedValueType={factory.Consumed.ValueType.Name}",
            $"produceRegistrationCount={factory.Produces.Count}",
            $"consumeRegistrationCount={factory.Consumes.Count}");

        Assert.Multiple(() => {
            Assert.That(factory.Consumed.Name, Is.EqualTo("incoming"));
            Assert.That(factory.Produced.Name, Is.EqualTo("outgoing"));
            Assert.That(factory.Shared.Name, Is.EqualTo("shared"));

            Assert.That(factory.Consumed.ValueType, Is.EqualTo(typeof(IReadOnlyList<int>)));
            Assert.That(factory.Produces, Has.Count.EqualTo(1));
            Assert.That(factory.Consumes, Has.Count.EqualTo(1));
            Assert.That(factory.Consumes[0], Is.SameAs(factory.Consumed));
            Assert.That(factory.Produces[0], Is.SameAs(factory.Produced));
        });
    }

    /// <summary>
    /// Verifies that shared list keys are created from the logical name only.
    /// </summary>
    /// <remarks>
    /// <para>Purpose: Define the naming contract for shared list artifacts.</para>
    /// <para>Why this matters: Shared keys should remain stable and predictable across producers and consumers.</para>
    /// <para>Expected result: The created key keeps the requested logical name and uses a read-only list value type.</para>
    /// </remarks>
    [Test]
    public void SharedList_CreatesKey_FromLogicalNameOnly() {
        NoPrefixListFactory factory = new();

        ListArtifactKey<int> key = factory.CreateSharedListKey();
        TestNarrative.ObserveMany(
            $"sharedKeyName={key.Name}",
            $"sharedValueType={key.ValueType.Name}");

        Assert.Multiple(() => {
            Assert.That(key.Name, Is.EqualTo("incoming"));
            Assert.That(key.ValueType, Is.EqualTo(typeof(IReadOnlyList<int>)));
        });
    }

    private sealed class ListHelperFactory : PhaseFactoryBase {
        public ListHelperFactory() : base("list-helper") {
            Consumed = ConsumeList<int>("incoming");
            Produced = ProduceList<int>("outgoing");
            Shared = SharedList<int>("shared");
        }

        public ListArtifactKey<int> Consumed { get; }
        public ListArtifactKey<int> Produced { get; }
        public ListArtifactKey<int> Shared { get; }

        public override IPhase Create() {
            return new ListHelperPhase();
        }
    }

    private sealed class NoPrefixListFactory : PhaseFactoryBase {
        public NoPrefixListFactory() : base("list-helper-no-prefix") {
        }

        public ListArtifactKey<int> CreateSharedListKey() {
            return SharedList<int>("incoming");
        }

        public override IPhase Create() {
            return new ListHelperPhase();
        }
    }

    private sealed class ListHelperPhase : IPhase {
        public string Name => "List Helper";
        public UnitSet Units { get; } = new();

        public Task<IPhaseOutcome> ExecuteAsync(IPhaseContext context, CancellationToken ct = default) {
            return Task.FromResult<IPhaseOutcome>(new PhaseOutcome());
        }
    }

    private sealed class TestUnit : IUnit {
        public Guid Id { get; } = Guid.NewGuid();
        public string Name { get; } = "Test Unit";
        public UnitStatus Status => UnitStatus.Pending;
        public PhaseKey? CurrentPhaseKey => null;
        public IReadOnlyCollection<UnitTag> Tags { get; } = Array.Empty<UnitTag>();
    }
}
