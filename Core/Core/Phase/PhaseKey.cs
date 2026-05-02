using System;

namespace BeltRunner.Core.Phase;

/// <summary>
/// Represents a strongly typed identifier for a phase.
/// </summary>
/// <remarks>
/// <para>
/// <see cref="PhaseKey"/> provides explicit identity semantics instead of using raw strings
/// across plan, factory, and execution contracts.
/// </para>
/// <para>
/// This record uses case-insensitive value-based equality; two keys are equal when their
/// <see cref="Value"/> strings are equal under <see cref="StringComparer.OrdinalIgnoreCase"/>.
/// </para>
/// </remarks>
public sealed record PhaseKey : IEquatable<PhaseKey> {
    private const int MAX_LENGTH = 256;
    private static readonly StringComparer keyComparer = StringComparer.OrdinalIgnoreCase;

    /// <summary>
    /// Initializes a new instance of the <see cref="PhaseKey"/> class.
    /// </summary>
    /// <param name="value">
    /// The raw key value.
    /// </param>
    /// <exception cref="ArgumentException">
    /// Thrown when <paramref name="value"/> is <see langword="null"/>, empty, or whitespace.
    /// </exception>
    public PhaseKey(string value) {
        if( string.IsNullOrWhiteSpace(value) ) {
            throw new ArgumentException("The phase key cannot be null, empty, or whitespace.", nameof(value));
        }

        if( value.Length > MAX_LENGTH ) {
            throw new ArgumentException($"The phase key cannot exceed {MAX_LENGTH} characters.", nameof(value));
        }

        if( BeltRunner.Core.Execution.TextConstraints.ContainsControlCharacters(value) ) {
            throw new ArgumentException("The phase key cannot contain control characters.", nameof(value));
        }

        Value = value;
    }

    /// <summary>
    /// Gets the raw key string.
    /// </summary>
    public string Value { get; }

    /// <summary>
    /// Determines whether this key is equal to another key using case-insensitive ordinal comparison.
    /// </summary>
    /// <param name="other">The other key.</param>
    /// <returns><see langword="true"/> when keys are equal; otherwise <see langword="false"/>.</returns>
    public bool Equals(PhaseKey? other) {
        if( ReferenceEquals(this, other) )
            return true;

        if( other is null )
            return false;

        return keyComparer.Equals(this.Value, other.Value);
    }


    /// <summary>
    /// Returns a hash code that matches case-insensitive equality semantics.
    /// </summary>
    /// <returns>The hash code of this key.</returns>
    public override int GetHashCode() {
        return keyComparer.GetHashCode(this.Value);
    }

    /// <summary>
    /// Returns the underlying key string.
    /// </summary>
    /// <returns>
    /// The value of <see cref="Value"/>.
    /// </returns>
    public override string ToString() {
        return Value;
    }
}
