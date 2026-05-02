using BeltRunner.Core.Plan.Artifacts;
using BeltRunner.Core.TEST.Testing;

namespace BeltRunner.Core.TEST.Plan.Artifacts;

/// <summary>
/// Verifies typed and list-based key registration behavior in <see cref="ArtifactKeyCatalog"/>.
/// </summary>
/// <remarks>
/// <para>Purpose: Protect the catalog rules that govern key identity, type reuse, and list-key registration.</para>
/// <para>Why this matters: Artifact lookup only remains safe if the catalog rejects conflicting registrations and reuses compatible keys consistently.</para>
/// <para>Expected result: Compatible registrations return the same key instance, and incompatible registrations fail with clear exceptions.</para>
/// </remarks>
[TestFixture]
[TestOf(typeof(ArtifactKeyCatalog))]
public sealed class ArtifactKeyCatalogListTests {
    /// <summary>
    /// Verifies that resolving the same logical name and value type returns the same key instance.
    /// </summary>
    /// <remarks>
    /// <para>Purpose: Define key identity reuse for compatible catalog lookups.</para>
    /// <para>Why this matters: Stable key identity simplifies downstream comparisons and avoids duplicate registrations for the same contract.</para>
    /// <para>Expected result: Two compatible <c>Get</c> calls return the exact same artifact key instance.</para>
    /// </remarks>
    [Test]
    public void Get_SameNameAndSameType_ReturnsSameInstance() {
        ArtifactKeyCatalog catalog = new();
        ArtifactName name = ArtifactName.Create("incoming");

        ArtifactKey<int> key1 = catalog.Get<int>(name);
        ArtifactKey<int> key2 = catalog.Get<int>(name);
        TestNarrative.ObserveMany(
            $"key1HashCode={key1.GetHashCode()}",
            $"key2HashCode={key2.GetHashCode()}",
            $"sameInstance={ReferenceEquals(key1, key2)}");

        Assert.That(key2, Is.SameAs(key1));
    }

    /// <summary>
    /// Verifies that <c>TryGet</c> resolves a previously registered compatible key.
    /// </summary>
    /// <remarks>
    /// <para>Purpose: Protect the non-throwing retrieval path for already registered keys.</para>
    /// <para>Why this matters: Callers often need to probe for an existing key without changing catalog state.</para>
    /// <para>Expected result: The lookup succeeds and returns the same key instance that was originally created.</para>
    /// </remarks>
    [Test]
    public void TryGet_SameNameAndSameType_ReturnsTrueAndSameInstance() {
        ArtifactKeyCatalog catalog = new();
        ArtifactName name = ArtifactName.Create("incoming");
        ArtifactKey<int> created = catalog.Get<int>(name);

        bool found = catalog.TryGet<int>(name, out ArtifactKey<int> resolved);
        TestNarrative.ObserveMany(
            $"found={found}",
            $"sameInstance={ReferenceEquals(created, resolved)}");

        Assert.Multiple(() => {
            Assert.That(found, Is.True);
            Assert.That(resolved, Is.SameAs(created));
        });
    }

    /// <summary>
    /// Verifies that registering the same logical name with a different value type fails.
    /// </summary>
    /// <remarks>
    /// <para>Purpose: Define the catalog protection against type collisions.</para>
    /// <para>Why this matters: Reusing a logical artifact name for multiple value types would break type safety at runtime.</para>
    /// <para>Expected result: A conflicting lookup throws an <see cref="InvalidOperationException"/> that describes the value-type mismatch.</para>
    /// </remarks>
    [Test]
    public void Get_Throws_WhenNameAlreadyRegisteredWithDifferentValueType() {
        ArtifactKeyCatalog catalog = new();
        ArtifactName name = ArtifactName.Create("incoming");

        catalog.Get<int>(name);

        InvalidOperationException ex = Assert.Throws<InvalidOperationException>(() => catalog.Get<string>(name))!;
        TestNarrative.Observe($"exceptionMessage={ex.Message}");
        Assert.That(ex.Message, Does.Contain("different value type"));
    }

    /// <summary>
    /// Verifies that <c>GetList</c> creates a list artifact key with a read-only list value type.
    /// </summary>
    /// <remarks>
    /// <para>Purpose: Protect the list-key specialization provided by the catalog.</para>
    /// <para>Why this matters: List artifacts rely on a predictable container type, not just a reused scalar key shape.</para>
    /// <para>Expected result: The created key keeps the logical name and exposes <c>IReadOnlyList&lt;T&gt;</c> as its value type.</para>
    /// </remarks>
    [Test]
    public void GetList_CreatesListArtifactKey() {
        ArtifactKeyCatalog catalog = new();
        ArtifactName name = ArtifactName.Create("incoming");

        ListArtifactKey<int> key = catalog.GetList<int>(name);
        TestNarrative.ObserveMany(
            $"keyName={key.Name}",
            $"valueType={key.ValueType.Name}");

        Assert.Multiple(() => {
            Assert.That(key.Name, Is.EqualTo("incoming"));
            Assert.That(key.ValueType, Is.EqualTo(typeof(IReadOnlyList<int>)));
        });
    }

    /// <summary>
    /// Verifies that a registered list key can be retrieved by list lookup without losing identity.
    /// </summary>
    /// <remarks>
    /// <para>Purpose: Confirm list-key reuse for explicit registrations.</para>
    /// <para>Why this matters: Callers should be able to register a list key once and retrieve the same object later.</para>
    /// <para>Expected result: <c>TryGetList</c> succeeds and returns the exact registered list key instance.</para>
    /// </remarks>
    [Test]
    public void RegisterList_ThenTryGetList_ReturnsSameInstance() {
        ArtifactKeyCatalog catalog = new();
        ArtifactName name = ArtifactName.Create("incoming");
        ListArtifactKey<int> key = new(name);

        catalog.Register(key);
        bool found = catalog.TryGetList<int>(name, out ListArtifactKey<int> stored);
        TestNarrative.ObserveMany(
            $"found={found}",
            $"sameInstance={ReferenceEquals(key, stored)}");

        Assert.Multiple(() => {
            Assert.That(found, Is.True);
            Assert.That(stored, Is.SameAs(key));
        });
    }

    /// <summary>
    /// Verifies that requesting a list key for a name already registered with a different value type fails.
    /// </summary>
    /// <remarks>
    /// <para>Purpose: Protect list-key creation from value-type collisions.</para>
    /// <para>Why this matters: The list helper should reject incompatible existing registrations just as strictly as scalar key lookup does.</para>
    /// <para>Expected result: The catalog throws an <see cref="InvalidOperationException"/> that identifies the conflicting value type.</para>
    /// </remarks>
    [Test]
    public void GetList_Throws_WhenNameAlreadyRegisteredWithDifferentValueType() {
        ArtifactKeyCatalog catalog = new();
        ArtifactName name = ArtifactName.Create("incoming");

        catalog.Register(new ArtifactKey<int>(name));

        InvalidOperationException ex = Assert.Throws<InvalidOperationException>(() => catalog.GetList<int>(name))!;
        TestNarrative.Observe($"exceptionMessage={ex.Message}");
        Assert.That(ex.Message, Does.Contain("different value type"));
    }

    /// <summary>
    /// Verifies that requesting a list key for a name already registered in the regular typed-key family fails.
    /// </summary>
    /// <remarks>
    /// <para>Purpose: Define the separation between list keys and regular typed keys.</para>
    /// <para>Why this matters: Keys with identical value types but different key families still represent different contracts.</para>
    /// <para>Expected result: The catalog throws an <see cref="InvalidOperationException"/> describing the typed-key family conflict.</para>
    /// </remarks>
    [Test]
    public void GetList_Throws_WhenNameAlreadyRegisteredWithDifferentKeyFamily() {
        ArtifactKeyCatalog catalog = new();
        ArtifactName name = ArtifactName.Create("incoming");

        catalog.Get<IReadOnlyList<int>>(name);

        InvalidOperationException ex = Assert.Throws<InvalidOperationException>(() => catalog.GetList<int>(name))!;
        TestNarrative.Observe($"exceptionMessage={ex.Message}");
        Assert.That(ex.Message, Does.Contain("typed-key family"));
    }

    /// <summary>
    /// Verifies that requesting a regular typed key for a name already registered as a list key fails.
    /// </summary>
    /// <remarks>
    /// <para>Purpose: Protect the inverse key-family conflict scenario.</para>
    /// <para>Why this matters: The catalog should be symmetric when rejecting incompatible regular and list key registrations.</para>
    /// <para>Expected result: The catalog throws an <see cref="InvalidOperationException"/> describing the typed-key family mismatch.</para>
    /// </remarks>
    [Test]
    public void Get_Throws_WhenNameAlreadyRegisteredWithDifferentKeyFamily() {
        ArtifactKeyCatalog catalog = new();
        ArtifactName name = ArtifactName.Create("incoming");

        catalog.GetList<int>(name);

        InvalidOperationException ex = Assert.Throws<InvalidOperationException>(() => catalog.Get<IReadOnlyList<int>>(name))!;
        TestNarrative.Observe($"exceptionMessage={ex.Message}");
        Assert.That(ex.Message, Does.Contain("typed-key family"));
    }
}
