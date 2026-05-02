using System;
using System.Collections.Generic;

namespace BeltRunner.Core.Plan.Artifacts;

/// <summary>
/// Compares artifact keys by their signature: (Name, ValueType).
/// </summary>
public sealed class ArtifactKeySignatureComparer : IEqualityComparer<IArtifactKey> {
    /// <summary>
    /// Gets the shared comparer instance.
    /// </summary>
    public static readonly ArtifactKeySignatureComparer Instance = new();

    /// <summary>
    /// Determines whether two artifact keys identify the same artifact slot.
    /// </summary>
    /// <param name="x">The first key to compare.</param>
    /// <param name="y">The second key to compare.</param>
    /// <returns><see langword="true"/> when both keys have the same name and value type; otherwise, <see langword="false"/>.</returns>
    public bool Equals(IArtifactKey? x, IArtifactKey? y) {
        if( ReferenceEquals(x, y) )
            return true;

        if( x is null || y is null )
            return false;

        return string.Equals(x.Name, y.Name, StringComparison.Ordinal)
               && ReferenceEquals(x.ValueType, y.ValueType);
    }

    /// <summary>
    /// Returns a hash code derived from the artifact key signature.
    /// </summary>
    /// <param name="obj">The artifact key to hash.</param>
    /// <returns>A hash code based on the key name and value type.</returns>
    public int GetHashCode(IArtifactKey obj) {
        if( obj is null ) throw new ArgumentNullException(nameof(obj));

        unchecked {
            int h1 = StringComparer.Ordinal.GetHashCode(obj.Name ?? string.Empty);
            int h2 = obj.ValueType.GetHashCode();
            return (h1 * 397) ^ h2;
        }
    }
}
