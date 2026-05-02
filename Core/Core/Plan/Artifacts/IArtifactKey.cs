using System;

namespace BeltRunner.Core.Plan.Artifacts;

/// <summary>
/// Untyped artifact key contract.
/// </summary>
public interface IArtifactKey {
    /// <summary>
    /// Stable identifier of the artifact slot.
    /// Avoid using UI display names.
    /// </summary>
    string Name { get; }

    /// <summary>
    /// The value type stored under this key.
    /// </summary>
    Type ValueType { get; }
}

/// <summary>
/// Strongly typed artifact key contract.
/// </summary>
public interface IArtifactKey<T> : IArtifactKey {
}