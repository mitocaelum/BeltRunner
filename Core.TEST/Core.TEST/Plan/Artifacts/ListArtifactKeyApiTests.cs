using BeltRunner.Core.Phase;
using BeltRunner.Core.Plan.Artifacts;
using BeltRunner.Core.TEST.Testing;

namespace BeltRunner.Core.TEST.Plan.Artifacts;

/// <summary>
/// Verifies core API properties of <see cref="ListArtifactKey{TItem}"/>.
/// </summary>
/// <remarks>
/// <para>Purpose: Protect the base type semantics of list artifact keys.</para>
/// <para>Why this matters: The list artifact abstraction only works if its value type stays aligned with read-only lists.</para>
/// <para>Expected result: The key exposes <c>IReadOnlyList&lt;TItem&gt;</c> as its runtime value type.</para>
/// </remarks>
[TestFixture]
[TestOf(typeof(ListArtifactKey<>))]
public sealed class ListArtifactKeyTests {
    /// <summary>
    /// Verifies that the runtime value type is a read-only list of the item type.
    /// </summary>
    /// <remarks>
    /// <para>Purpose: Define the runtime type contract for list artifact keys.</para>
    /// <para>Why this matters: Producers and consumers depend on a stable list container type when exchanging artifacts.</para>
    /// <para>Expected result: The key reports <c>IReadOnlyList&lt;int&gt;</c> for an integer list artifact.</para>
    /// </remarks>
    [Test]
    public void ValueType_IsReadOnlyListOfItem() {
        ArtifactName name = ArtifactName.Create("incoming");
        ListArtifactKey<int> key = new(name);
        TestNarrative.Observe($"valueType={key.ValueType.Name}");

        Assert.That(key.ValueType, Is.EqualTo(typeof(IReadOnlyList<int>)));
    }
}

/// <summary>
/// Verifies that <see cref="ArtifactName"/> works cleanly with list artifact key construction.
/// </summary>
/// <remarks>
/// <para>Purpose: Protect the compatibility between logical artifact names and list-key creation.</para>
/// <para>Why this matters: Naming helpers are often used together, and friction here would make the list-key API awkward to adopt.</para>
/// <para>Expected result: A created artifact name can be passed directly into a list key and preserved as the logical key name.</para>
/// </remarks>
[TestFixture]
[TestOf(typeof(ArtifactName))]
public sealed class ArtifactNameTests {
    /// <summary>
    /// Verifies that an artifact name created through the factory can be used to construct a list artifact key.
    /// </summary>
    /// <remarks>
    /// <para>Purpose: Confirm the happy path integration between <see cref="ArtifactName.Create(string)"/> and <see cref="ListArtifactKey{TItem}"/>.</para>
    /// <para>Why this matters: Logical naming should remain straightforward when list artifacts are introduced.</para>
    /// <para>Expected result: The constructed key keeps the provided logical artifact name unchanged.</para>
    /// </remarks>
    [Test]
    public void Create_Works_ForListArtifactKeyConstruction() {
        ListArtifactKey<int> key = new(ArtifactName.Create("incomingOrders"));
        TestNarrative.Observe($"keyName={key.Name}");

        Assert.That(key.Name, Is.EqualTo("incomingOrders"));
    }
}

/// <summary>
/// Verifies artifact store behavior when list artifact keys are used for storage and retrieval.
/// </summary>
/// <remarks>
/// <para>Purpose: Protect list-key interoperability with the shared artifact store.</para>
/// <para>Why this matters: The store is where key identity meets runtime values, so list keys must behave exactly like other keys at retrieval time.</para>
/// <para>Expected result: Values stored through list keys can be retrieved directly and through compatible key signatures.</para>
/// </remarks>
[TestFixture]
[TestOf(typeof(ArtifactStore))]
public sealed class ArtifactStoreListKeyTests {
    /// <summary>
    /// Verifies that the artifact store can set and get a value through a list artifact key.
    /// </summary>
    /// <remarks>
    /// <para>Purpose: Confirm the basic storage path for list artifacts.</para>
    /// <para>Why this matters: A dedicated key type is only useful if the store accepts it without special handling by the caller.</para>
    /// <para>Expected result: The stored list is found successfully and returned as the same object instance.</para>
    /// </remarks>
    [Test]
    public void SetAndGet_Works_WithListArtifactKey() {
        ArtifactStore store = new();
        ListArtifactKey<int> key = new(ArtifactName.Create("numbers"));
        IReadOnlyList<int> expected = new List<int> { 10, 20, 30 };

        store.Set(key, expected);
        bool found = store.TryGet(key, out IReadOnlyList<int> actual);
        TestNarrative.ObserveMany(
            $"found={found}",
            $"sameInstance={ReferenceEquals(expected, actual)}",
            $"actualValues={string.Join(", ", actual)}");

        Assert.Multiple(() => {
            Assert.That(found, Is.True);
            Assert.That(actual, Is.SameAs(expected));
        });
    }

    /// <summary>
    /// Verifies that the artifact store matches list and regular keys by logical name and value-type signature.
    /// </summary>
    /// <remarks>
    /// <para>Purpose: Define interoperability between compatible key implementations.</para>
    /// <para>Why this matters: Callers may use different key construction paths and still expect the same stored value when the logical contract matches.</para>
    /// <para>Expected result: A value stored with a list key can be retrieved through a regular key with the same name and read-only list value type.</para>
    /// </remarks>
    [Test]
    public void Store_UsesNameAndValueTypeSignature_AcrossKeyImplementations() {
        ArtifactStore store = new();
        ListArtifactKey<int> listKey = new(ArtifactName.Create("numbers"));
        ArtifactKey<IReadOnlyList<int>> regularKey = ArtifactSeeds.Key<IReadOnlyList<int>>("numbers");
        IReadOnlyList<int> expected = new List<int> { 2, 4, 6 };

        store.Set(listKey, expected);
        bool found = store.TryGet(regularKey, out IReadOnlyList<int> actual);
        TestNarrative.ObserveMany(
            $"found={found}",
            $"sameInstance={ReferenceEquals(expected, actual)}",
            $"actualValues={string.Join(", ", actual)}");

        Assert.Multiple(() => {
            Assert.That(found, Is.True);
            Assert.That(actual, Is.SameAs(expected));
        });
    }
}

/// <summary>
/// Verifies that <see cref="PhaseOutcome"/> accepts list artifact keys as produced outputs.
/// </summary>
/// <remarks>
/// <para>Purpose: Protect list artifact production within phase outcome composition.</para>
/// <para>Why this matters: Phase outputs are the handoff point between phases, so list-key support must work there without custom wrappers.</para>
/// <para>Expected result: Producing with a list key records the key metadata and value exactly as provided.</para>
/// </remarks>
[TestFixture]
[TestOf(typeof(PhaseOutcome))]
public sealed class PhaseOutcomeListKeyTests {
    /// <summary>
    /// Verifies that producing an artifact with a list key records the expected key and value.
    /// </summary>
    /// <remarks>
    /// <para>Purpose: Confirm the list-key overload path for produced artifacts.</para>
    /// <para>Why this matters: Output contracts should remain uniform regardless of whether a scalar or list artifact is produced.</para>
    /// <para>Expected result: The outcome records one produced artifact with the original key name, list value type, and value instance.</para>
    /// </remarks>
    [Test]
    public void Produce_AcceptsListArtifactKey() {
        ListArtifactKey<int> key = new(ArtifactName.Create("numbers"));
        IReadOnlyList<int> values = new List<int> { 1, 2, 3 };

        PhaseOutcome outcome = new PhaseOutcome().Produce(key, values);
        TestNarrative.ObserveMany(
            $"producedCount={outcome.Produced.Count}",
            $"keyName={outcome.Produced[0].Key.Name}",
            $"valueType={outcome.Produced[0].Key.ValueType.Name}",
            $"sameValueInstance={ReferenceEquals(values, outcome.Produced[0].Value)}");

        Assert.Multiple(() => {
            Assert.That(outcome.Produced, Has.Count.EqualTo(1));
            Assert.That(outcome.Produced[0].Key.Name, Is.EqualTo(key.Name));
            Assert.That(outcome.Produced[0].Key.ValueType, Is.EqualTo(typeof(IReadOnlyList<int>)));
            Assert.That(outcome.Produced[0].Value, Is.SameAs(values));
        });
    }
}

/// <summary>
/// Verifies that <see cref="ArtifactSeeds"/> can create produced artifacts from list artifact keys.
/// </summary>
/// <remarks>
/// <para>Purpose: Protect list-key support in seed artifact creation.</para>
/// <para>Why this matters: Initial artifact seeding should work consistently for both scalar and list-based startup data.</para>
/// <para>Expected result: Seeded produced artifacts preserve the list key metadata and the supplied value instance.</para>
/// </remarks>
[TestFixture]
[TestOf(typeof(ArtifactSeeds))]
public sealed class ArtifactSeedsListKeyTests {
    /// <summary>
    /// Verifies that a list artifact key can be used directly when seeding an artifact.
    /// </summary>
    /// <remarks>
    /// <para>Purpose: Confirm the explicit list-key seeding path.</para>
    /// <para>Why this matters: Startup data often arrives in batches, and list artifacts should be first-class seeds.</para>
    /// <para>Expected result: The seeded artifact keeps the original key metadata and the original value object.</para>
    /// </remarks>
    [Test]
    public void Seed_AcceptsListArtifactKey() {
        ListArtifactKey<int> key = new(ArtifactName.Create("numbers"));
        IReadOnlyList<int> values = new List<int> { 5, 8, 13 };

        IProducedArtifact seeded = ArtifactSeeds.Seed(key, values);
        TestNarrative.ObserveMany(
            $"keyName={seeded.Key.Name}",
            $"valueType={seeded.Key.ValueType.Name}",
            $"sameValueInstance={ReferenceEquals(values, seeded.Value)}");

        Assert.Multiple(() => {
            Assert.That(seeded.Key.Name, Is.EqualTo(key.Name));
            Assert.That(seeded.Key.ValueType, Is.EqualTo(typeof(IReadOnlyList<int>)));
            Assert.That(seeded.Value, Is.SameAs(values));
        });
    }

    /// <summary>
    /// Verifies that list artifact seeding works cleanly when only a logical name is used to create the key.
    /// </summary>
    /// <remarks>
    /// <para>Purpose: Protect the common path where callers build a list key from a logical artifact name and immediately seed it.</para>
    /// <para>Why this matters: The ergonomic path should not hide any metadata mismatch or unexpected key rewriting.</para>
    /// <para>Expected result: The seeded artifact keeps the logical name, exposes the read-only list value type, and stores the original values object.</para>
    /// </remarks>
    [Test]
    public void Seed_Works_WithLogicalNameOnly() {
        ListArtifactKey<int> incomingOrders = new(ArtifactName.Create("incomingOrders"));
        IReadOnlyList<int> initialOrders = new List<int> { 101, 102 };

        IProducedArtifact seeded = ArtifactSeeds.Seed(incomingOrders, initialOrders);
        TestNarrative.ObserveMany(
            $"keyName={seeded.Key.Name}",
            $"valueType={seeded.Key.ValueType.Name}",
            $"sameValueInstance={ReferenceEquals(initialOrders, seeded.Value)}");

        Assert.Multiple(() => {
            Assert.That(seeded.Key.Name, Is.EqualTo("incomingOrders"));
            Assert.That(seeded.Key.ValueType, Is.EqualTo(typeof(IReadOnlyList<int>)));
            Assert.That(seeded.Value, Is.SameAs(initialOrders));
        });
    }
}

