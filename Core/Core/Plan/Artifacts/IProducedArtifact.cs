namespace BeltRunner.Core.Plan.Artifacts;

/// <summary>
/// Non-generic produced item for Runner consumption.
/// </summary>
public interface IProducedArtifact {
    /// <summary>
    /// Gets the artifact key that identifies the produced artifact slot.
    /// </summary>
    IArtifactKey Key { get; }

    /// <summary>
    /// Gets the produced artifact value as an object.
    /// </summary>
    object Value { get; }
}
