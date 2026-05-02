using BeltRunner.Core.Plan.Artifacts;

namespace BeltRunner.Core.TEST.Plan.Artifacts;

/// <summary>
/// Verifies fluent artifact-seed composition behavior.
/// </summary>
[TestFixture]
[TestOf(typeof(ArtifactSeedBuilder))]
public sealed class ArtifactSeedBuilderTests {
    /// <summary>
    /// Verifies that the builder creates produced artifacts from both typed and logical keys.
    /// </summary>
    [Test]
    public void Build_WithTypedAndLogicalKeys_ProducesSeedArtifacts() {
        ArtifactKey<int> typedKey = ArtifactSeeds.Key<int>("typedValue");

        IReadOnlyList<IProducedArtifact> artifacts = new ArtifactSeedBuilder()
            .Add(typedKey, 42)
            .Add("logicalValue", "hello")
            .Build();

        Assert.Multiple(() => {
            Assert.That(artifacts, Has.Count.EqualTo(2));
            Assert.That(artifacts[0].Key.Name, Is.EqualTo("typedValue"));
            Assert.That(artifacts[0].Value, Is.EqualTo(42));
            Assert.That(artifacts[1].Key.Name, Is.EqualTo("logicalValue"));
            Assert.That(artifacts[1].Value, Is.EqualTo("hello"));
        });
    }
}
