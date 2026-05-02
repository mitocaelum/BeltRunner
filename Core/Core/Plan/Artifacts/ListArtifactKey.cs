using System;
using System.Collections.Generic;

namespace BeltRunner.Core.Plan.Artifacts;

/// <summary>
/// Strongly typed artifact key for list-shaped artifacts whose value type is <see cref="IReadOnlyList{T}"/>.
/// </summary>
/// <typeparam name="TItem">
/// The list item type.
/// </typeparam>
public sealed class ListArtifactKey<TItem> : IArtifactKey<IReadOnlyList<TItem>> {
    /// <summary>
    /// Initializes a new instance of the <see cref="ListArtifactKey{TItem}"/> class.
    /// </summary>
    /// <param name="name">Artifact name.</param>
    /// <exception cref="ArgumentException">
    /// Thrown when the key name is missing.
    /// </exception>
    public ListArtifactKey(ArtifactName name) {
        string n = name.Value;
        if( string.IsNullOrWhiteSpace(n) )
            throw new ArgumentException("Artifact key name is required.", nameof(name));

        Name = n;
    }

    /// <inheritdoc/>
    public string Name { get; }

    /// <inheritdoc/>
    public Type ValueType => typeof(IReadOnlyList<TItem>);

    /// <inheritdoc/>
    public override string ToString() => $"{Name}<{ValueType.Name}>";
}
