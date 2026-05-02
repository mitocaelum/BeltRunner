using System;

namespace BeltRunner.Core.Plan.Artifacts;

/// <summary>
/// Strongly typed artifact key.
/// </summary>
/// <remarks>
/// <para>
/// Identity rule: (Name, ValueType).
/// Keys with the same name and value type are treated as the same artifact slot,
/// even if they are different instances.
/// </para>
/// </remarks>
public sealed class ArtifactKey<T> : IArtifactKey<T> {
    /// <summary>
    /// Initializes a new instance of the <see cref="ArtifactKey{T}"/> class.
    /// </summary>
    /// <param name="name">The validated logical name of the artifact slot.</param>
    public ArtifactKey(ArtifactName name) {
        string n = name.Value;
        if( string.IsNullOrWhiteSpace(n) )
            throw new ArgumentException("Artifact key name is required.", nameof(name));

        Name = n;
    }

    /// <inheritdoc />
    public string Name { get; }

    /// <inheritdoc />
    public Type ValueType => typeof(T);

    /// <summary>
    /// Returns the display representation of the artifact key.
    /// </summary>
    /// <returns>A string composed from the artifact name and value type.</returns>
    public override string ToString() => $"{Name}<{ValueType.Name}>";
}
