using System;
using System.Collections.Generic;

namespace BeltRunner.Core.Plan.Artifacts;

/// <summary>
/// Collects initial artifacts for run startup using a fluent builder surface.
/// </summary>
public sealed class ArtifactSeedBuilder {
    private readonly List<IProducedArtifact> artifacts = new();

    /// <summary>
    /// Adds a produced artifact instance directly.
    /// </summary>
    /// <param name="artifact">The artifact to add.</param>
    /// <returns>The current builder instance.</returns>
    public ArtifactSeedBuilder Add(IProducedArtifact artifact) {
        if( artifact is null ) {
            throw new ArgumentNullException(nameof(artifact));
        }

        this.artifacts.Add(artifact);
        return this;
    }

    /// <summary>
    /// Adds an initial artifact using an existing typed key.
    /// </summary>
    /// <typeparam name="T">The artifact value type.</typeparam>
    /// <param name="key">The artifact key.</param>
    /// <param name="value">The artifact value.</param>
    /// <returns>The current builder instance.</returns>
    public ArtifactSeedBuilder Add<T>(IArtifactKey<T> key, T value) {
        if( key is null ) {
            throw new ArgumentNullException(nameof(key));
        }

        this.artifacts.Add(ArtifactSeeds.Seed(key, value));
        return this;
    }

    /// <summary>
    /// Adds an initial artifact using an existing typed key.
    /// </summary>
    /// <typeparam name="T">The artifact value type.</typeparam>
    /// <param name="key">The artifact key.</param>
    /// <param name="value">The artifact value.</param>
    /// <returns>The current builder instance.</returns>
    public ArtifactSeedBuilder Add<T>(ArtifactKey<T> key, T value) {
        return Add((IArtifactKey<T>)key, value);
    }

    /// <summary>
    /// Adds an initial artifact using a logical artifact name.
    /// </summary>
    /// <typeparam name="T">The artifact value type.</typeparam>
    /// <param name="logicalName">The logical artifact name.</param>
    /// <param name="value">The artifact value.</param>
    /// <returns>The current builder instance.</returns>
    public ArtifactSeedBuilder Add<T>(string logicalName, T value) {
        this.artifacts.Add(ArtifactSeeds.Seed(logicalName, value));
        return this;
    }

    /// <summary>
    /// Builds the collected artifact seeds.
    /// </summary>
    /// <returns>The collected initial artifacts.</returns>
    public IReadOnlyList<IProducedArtifact> Build() {
        return this.artifacts.ToArray();
    }
}
