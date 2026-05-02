using System;

namespace BeltRunner.Core.Plan.Artifacts;

/// <summary>
/// Represents an artifact name.
/// This value object validates the naming rule at creation time.
/// </summary>
public readonly struct ArtifactName : IEquatable<ArtifactName> {
    private const string NAME_REQUIRED_MESSAGE = "Artifact name is required.";
    private const string INVALID_NAME_MESSAGE = "Artifact name contains invalid characters.";

    private readonly string value;

    private ArtifactName(string value) {
        this.value = value;
    }

    /// <summary>
    /// Gets the validated artifact name value.
    /// </summary>
    public string Value => this.value ?? string.Empty;

    /// <summary>
    /// Creates an artifact name from a logical name.
    /// </summary>
    /// <param name="logicalName">The logical artifact name.</param>
    /// <returns>A validated artifact name value.</returns>
    public static ArtifactName Create(string logicalName) {
        if( string.IsNullOrWhiteSpace(logicalName) )
            throw new ArgumentException(NAME_REQUIRED_MESSAGE, nameof(logicalName));

        string n = logicalName.Trim();
        if( !IsValidName(n) )
            throw new ArgumentException($"{INVALID_NAME_MESSAGE} logicalName=\"{n}\"", nameof(logicalName));

        return new ArtifactName(n);
    }

    private static bool IsValidName(string s) {
        // Conservative rule (easy to analyze later):
        // - Non-empty
        // - [A-Za-z] as first char
        // - [A-Za-z0-9_]* for the rest
        if( string.IsNullOrEmpty(s) )
            return false;

        char c0 = s[0];
        if( !IsAsciiLetter(c0) )
            return false;

        for( int i = 1; i < s.Length; i++ ) {
            char c = s[i];
            if( IsAsciiLetter(c) || IsAsciiDigit(c) || c == '_' )
                continue;

            return false;
        }

        return true;
    }

    private static bool IsAsciiLetter(char c) {
        return (c >= 'A' && c <= 'Z') || (c >= 'a' && c <= 'z');
    }

    private static bool IsAsciiDigit(char c) {
        return c >= '0' && c <= '9';
    }

    /// <summary>
    /// Returns the validated artifact name as a string.
    /// </summary>
    /// <returns>The artifact name value.</returns>
    public override string ToString() => this.value ?? string.Empty;

    /// <summary>
    /// Determines whether this instance and another artifact name have the same value.
    /// </summary>
    /// <param name="other">The other artifact name to compare.</param>
    /// <returns><see langword="true"/> when both names have the same ordinal value; otherwise, <see langword="false"/>.</returns>
    public bool Equals(ArtifactName other) {
        return string.Equals(this.Value, other.Value, StringComparison.Ordinal);
    }

    /// <summary>
    /// Determines whether this instance and another object are equal.
    /// </summary>
    /// <param name="obj">The object to compare with the current value.</param>
    /// <returns><see langword="true"/> when <paramref name="obj"/> is an equal <see cref="ArtifactName"/>; otherwise, <see langword="false"/>.</returns>
    public override bool Equals(object? obj) => obj is ArtifactName other && Equals(other);

    /// <summary>
    /// Returns a hash code for the current artifact name.
    /// </summary>
    /// <returns>An ordinal hash code for <see cref="Value"/>.</returns>
    public override int GetHashCode() => StringComparer.Ordinal.GetHashCode(this.Value);

    /// <summary>
    /// Determines whether two artifact names have the same value.
    /// </summary>
    /// <param name="left">The first value to compare.</param>
    /// <param name="right">The second value to compare.</param>
    /// <returns><see langword="true"/> when both values are equal; otherwise, <see langword="false"/>.</returns>
    public static bool operator ==(ArtifactName left, ArtifactName right) => left.Equals(right);

    /// <summary>
    /// Determines whether two artifact names have different values.
    /// </summary>
    /// <param name="left">The first value to compare.</param>
    /// <param name="right">The second value to compare.</param>
    /// <returns><see langword="true"/> when the values differ; otherwise, <see langword="false"/>.</returns>
    public static bool operator !=(ArtifactName left, ArtifactName right) => !left.Equals(right);
}
