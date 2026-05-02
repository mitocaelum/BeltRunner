using System.Collections.Generic;
using System.Runtime.CompilerServices;

namespace BeltRunner.Core.Plan;

/// <summary>
/// Compares reference-type values by object identity.
/// </summary>
/// <typeparam name="T">The reference type being compared.</typeparam>
public sealed class ReferenceEqualityComparer<T> : IEqualityComparer<T> where T : class {
    /// <summary>
    /// Gets the shared comparer instance.
    /// </summary>
    public static readonly ReferenceEqualityComparer<T> Instance = new();

    /// <summary>
    /// Determines whether two values refer to the same object instance.
    /// </summary>
    /// <param name="x">The first value to compare.</param>
    /// <param name="y">The second value to compare.</param>
    /// <returns><see langword="true"/> when both values reference the same object; otherwise, <see langword="false"/>.</returns>
    public bool Equals(T? x, T? y) => ReferenceEquals(x, y);

    /// <summary>
    /// Returns a hash code based on object identity.
    /// </summary>
    /// <param name="obj">The value to hash.</param>
    /// <returns>A runtime hash code tied to the object instance.</returns>
    public int GetHashCode(T obj) => RuntimeHelpers.GetHashCode(obj);
}
