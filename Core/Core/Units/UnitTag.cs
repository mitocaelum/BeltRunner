using System;

namespace BeltRunner.Core.Units;

/// <summary>
/// Represents a strongly typed tag assigned to a unit.
/// </summary>
/// <remarks>
/// <para>
/// A <see cref="UnitTag"/> is an immutable value object used to classify or label units.
/// Tags are typically used for filtering, grouping, routing decisions, diagnostics, or UI presentation.
/// </para>
/// <para>
/// The tag value is normalized by trimming leading and trailing whitespace in the constructor.
/// </para>
/// <para>
/// Comparison and hashing follow the default behavior of records. This means that equality is based on
/// the <see cref="Value"/> string and is case-sensitive by default.
/// If your application requires case-insensitive semantics, normalize the input (for example, by
/// converting to upper or lower case) before creating a <see cref="UnitTag"/>.
/// </para>
/// </remarks>
// ReSharper disable once ClassNeverInstantiated.Global
public sealed record UnitTag {
    /// <summary>
    /// Initializes a new instance of the <see cref="UnitTag"/> class.
    /// </summary>
    /// <param name="value">
    /// The raw tag value.
    /// </param>
    /// <remarks>
    /// <para>
    /// The provided value is validated and then normalized by trimming leading and trailing whitespace.
    /// The normalized value is stored in <see cref="Value"/>.
    /// </para>
    /// <para>
    /// This type does not enforce additional constraints such as allowed characters or length limits.
    /// Such rules, if needed, should be implemented by the application that defines the tag vocabulary.
    /// </para>
    /// </remarks>
    /// <exception cref="ArgumentException">
    /// Thrown when <paramref name="value"/> is <see langword="null"/>, empty, or whitespace.
    /// </exception>
    public UnitTag(string value) {
        if (string.IsNullOrWhiteSpace(value)) {
            throw new ArgumentException("The unit tag cannot be null, empty, or whitespace.", nameof(value));
        }

        Value = value.Trim();
    }

    /// <summary>
    /// Gets the normalized string value of the tag.
    /// </summary>
    /// <value>
    /// The trimmed tag value.
    /// </value>
    /// <remarks>
    /// <para>
    /// The returned value is never <see langword="null"/> and is never empty or whitespace-only.
    /// </para>
    /// </remarks>
    public string Value { get; }

    /// <summary>
    /// Returns the normalized string value of the tag.
    /// </summary>
    /// <returns>
    /// The value of <see cref="Value"/>.
    /// </returns>
    public override string ToString() {
        return Value;
    }
}