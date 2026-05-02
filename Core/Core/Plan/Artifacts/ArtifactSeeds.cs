using System;

namespace BeltRunner.Core.Plan.Artifacts;

/// <summary>
/// Helpers for creating artifact keys and seeding initial artifacts.
/// Convenience APIs in this type build on top of <see cref="ArtifactName"/> and typed key contracts.
/// </summary>
public static class ArtifactSeeds {
    /// <summary>
    /// Creates a produced artifact for initial seeding using an existing typed key.
    /// </summary>
    /// <param name="key">The typed artifact key that identifies the artifact slot.</param>
    /// <param name="value">The value to seed for the specified key.</param>
    /// <typeparam name="T">The artifact value type.</typeparam>
    /// <returns>A produced artifact that can be passed as initial run artifacts.</returns>
    public static IProducedArtifact Seed<T>(IArtifactKey<T> key, T value) {
        return new ProducedArtifact<T>(key, value);
    }

    /// <summary>
    /// Creates a produced artifact for initial seeding using an existing <see cref="ArtifactKey{T}"/>.
    /// </summary>
    /// <param name="key">The typed artifact key that identifies the artifact slot.</param>
    /// <param name="value">The value to seed for the specified key.</param>
    /// <typeparam name="T">The artifact value type.</typeparam>
    /// <returns>A produced artifact that can be passed as initial run artifacts.</returns>
    public static IProducedArtifact Seed<T>(ArtifactKey<T> key, T value) {
        return Seed((IArtifactKey<T>)key, value);
    }

    /// <summary>
    /// Creates a produced artifact for initial seeding using a logical artifact name.
    /// </summary>
    /// <param name="logicalName">The logical artifact name.</param>
    /// <param name="value">The value to seed for the computed key.</param>
    /// <typeparam name="T">The artifact value type.</typeparam>
    /// <returns>A produced artifact that can be passed as initial run artifacts.</returns>
    public static IProducedArtifact Seed<T>(string logicalName, T value) {
        ArtifactName name = ArtifactName.Create(logicalName);
        ArtifactKey<T> key = new ArtifactKey<T>(name);

        return new ProducedArtifact<T>(key, value);
    }

    /// <summary>
    /// Creates an artifact key using the provided logical name.
    /// This is a low-level helper that directly composes <see cref="ArtifactName"/> and <see cref="ArtifactKey{T}"/>.
    /// </summary>
    /// <param name="logicalName">The logical artifact name.</param>
    /// <typeparam name="T">The artifact value type.</typeparam>
    /// <returns>A typed artifact key for the specified logical name.</returns>
    public static ArtifactKey<T> Key<T>(string logicalName) {
        return new ArtifactKey<T>(ArtifactName.Create(logicalName));
    }
}
