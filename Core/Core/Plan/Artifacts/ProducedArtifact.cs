using System;

namespace BeltRunner.Core.Plan.Artifacts;

/// <summary>
/// Generic produced artifact, convenient for phase code.
/// </summary>
public sealed class ProducedArtifact<T> : IProducedArtifact {
    /// <summary>
    /// Initializes a new produced artifact from a typed artifact key.
    /// </summary>
    /// <param name="key">The typed artifact key.</param>
    /// <param name="value">The produced value.</param>
    public ProducedArtifact(IArtifactKey<T> key, T value) {
        if( value is null ) throw new ArgumentNullException(nameof(value));

        TypedKey = key ?? throw new ArgumentNullException(nameof(key));
        TypedValue = value;
    }

    /// <summary>
    /// Initializes a new produced artifact from an <see cref="ArtifactKey{T}"/>.
    /// </summary>
    /// <param name="key">The typed artifact key.</param>
    /// <param name="value">The produced value.</param>
    public ProducedArtifact(ArtifactKey<T> key, T value) : this((IArtifactKey<T>)key, value) {
    }

    /// <summary>
    /// Gets the typed artifact key.
    /// </summary>
    public IArtifactKey<T> TypedKey { get; }

    /// <summary>
    /// Gets the typed produced value.
    /// </summary>
    public T TypedValue { get; }

    /// <inheritdoc/>
    public IArtifactKey Key => TypedKey;

    /// <inheritdoc/>
    public object Value => TypedValue!;
}
