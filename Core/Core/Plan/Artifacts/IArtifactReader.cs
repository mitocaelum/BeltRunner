namespace BeltRunner.Core.Plan.Artifacts;

/// <summary>
/// Read-only view for phases.
/// Phases can query artifacts, but should not write to the store directly.
/// Writing is performed by the Runner when it merges PhaseOutcome.
/// </summary>
public interface IArtifactReader {
    /// <summary>
    /// Determines whether a value is present for the specified artifact key.
    /// </summary>
    /// <param name="key">The artifact key to check.</param>
    /// <returns><see langword="true"/> when the artifact is present; otherwise, <see langword="false"/>.</returns>
    bool Contains(IArtifactKey key);

    /// <summary>
    /// Tries to get the value associated with the specified typed artifact key.
    /// </summary>
    /// <typeparam name="T">The artifact value type.</typeparam>
    /// <param name="key">The typed artifact key.</param>
    /// <param name="value">When this method returns, the stored value if present.</param>
    /// <returns><see langword="true"/> when the value is present; otherwise, <see langword="false"/>.</returns>
    bool TryGet<T>(IArtifactKey<T> key, out T value);

    /// <summary>
    /// Gets the value associated with the specified typed artifact key or throws when the value is missing.
    /// </summary>
    /// <typeparam name="T">The artifact value type.</typeparam>
    /// <param name="key">The typed artifact key.</param>
    /// <returns>The stored artifact value.</returns>
    T GetRequired<T>(IArtifactKey<T> key);
}
