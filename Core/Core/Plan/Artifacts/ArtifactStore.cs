using System;
using System.Collections.Generic;

namespace BeltRunner.Core.Plan.Artifacts;

/// <summary>
/// Run-scope artifact store.
/// Artifact keys are matched by signature: (Name, ValueType).
/// </summary>
/// <remarks>
/// Different key instances are treated as the same artifact slot when both
/// <see cref="IArtifactKey.Name"/> and <see cref="IArtifactKey.ValueType"/> match.
/// </remarks>
public sealed class ArtifactStore : IArtifactReader {
    private readonly object gate = new();

    private readonly Dictionary<IArtifactKey, object?> values = new(ArtifactKeySignatureComparer.Instance);

    /// <inheritdoc />
    public bool Contains(IArtifactKey key) {
        if( key is null ) throw new ArgumentNullException(nameof(key));

        lock( this.gate ) {
            return this.values.ContainsKey(key);
        }
    }

    /// <inheritdoc />
    public bool TryGet<T>(IArtifactKey<T> key, out T value) {
        if( key is null ) throw new ArgumentNullException(nameof(key));

        lock( this.gate ) {
            if( !this.values.TryGetValue(key, out object? boxed) ) {
                value = default!;
                return false;
            }

            if( boxed is T typed ) {
                value = typed;
                return true;
            }

            throw new InvalidOperationException($"Artifact type mismatch. key=\"{key.Name}\" expected=\"{typeof(T).FullName}\" actual=\"{boxed?.GetType().FullName ?? "null"}\"");
        }
    }

    /// <inheritdoc />
    public T GetRequired<T>(IArtifactKey<T> key) {
        if( key is null ) throw new ArgumentNullException(nameof(key));

        if( TryGet(key, out T value) )
            return value;

        throw new InvalidOperationException($"Required artifact is missing. key=\"{key.Name}\" type=\"{typeof(T).FullName}\"");
    }

    /// <summary>
    /// Sets a strongly typed value for the specified artifact key.
    /// </summary>
    /// <typeparam name="T">The artifact value type.</typeparam>
    /// <param name="key">The artifact key that identifies the storage slot.</param>
    /// <param name="value">The value to store.</param>
    public void Set<T>(IArtifactKey<T> key, T value) {
        if( key is null ) throw new ArgumentNullException(nameof(key));

        SetBoxed(key, value);
    }

    /// <summary>
    /// Sets a value using an untyped key.
    /// Intended for internal wiring (e.g., merging phase outputs).
    /// </summary>
    internal void SetBoxed(IArtifactKey key, object? value) {
        if( key is null ) throw new ArgumentNullException(nameof(key));

        if( value is null ) {
            if( key.ValueType.IsValueType ) {
                throw new InvalidOperationException($"Null is not allowed for value type artifacts. key=\"{key.Name}\" type=\"{key.ValueType.FullName}\"");
            }
        } else {
            Type actual = value.GetType();
            if( !key.ValueType.IsAssignableFrom(actual) ) {
                throw new InvalidOperationException($"Artifact type mismatch. key=\"{key.Name}\" expected=\"{key.ValueType.FullName}\" actual=\"{actual.FullName}\"");
            }
        }

        lock( this.gate ) {
            this.values[key] = value;
        }
    }

    internal bool TryGetBoxed(IArtifactKey key, out object? value) {
        if( key is null ) throw new ArgumentNullException(nameof(key));

        lock( this.gate ) {
            return this.values.TryGetValue(key, out value);
        }
    }
}
