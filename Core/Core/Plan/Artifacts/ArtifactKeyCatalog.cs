using System;
using System.Collections.Generic;

namespace BeltRunner.Core.Plan.Artifacts;

/// <summary>
/// Provides a run-agnostic catalog (intern pool) for typed artifact key instances.
/// </summary>
/// <remarks>
/// <para>
/// Identity rule is (Name, ValueType).
/// This catalog exists to centralize key creation and to catch accidental type mismatches for the same name.
/// </para>
/// <para>
/// The catalog keeps key-family determinism per name. If a name is already associated with one typed-key family,
/// requesting the same name through a different key family throws.
/// </para>
/// <para>
/// This is intentionally stricter than runtime artifact-slot matching in <see cref="ArtifactStore"/>,
/// which uses (Name, ValueType). The catalog is a definition-time guard that prevents mixing typed-key
/// families for the same name in one registry.
/// </para>
/// </remarks>
public sealed class ArtifactKeyCatalog {
    private const string FROZEN_MESSAGE = "The catalog is frozen. New keys cannot be created or registered.";
    private const string NAME_REQUIRED_MESSAGE = "Artifact key name is required.";
    private const string KEY_FAMILY_MISMATCH_MESSAGE = "Artifact key name is already registered with a different typed-key family.";

    private readonly object gate = new();
    private readonly Dictionary<string, IArtifactKey> keysByName = new(StringComparer.Ordinal);

    private bool isFrozen;

    /// <summary>
    /// Gets a value indicating whether the catalog rejects creation and registration of new keys.
    /// </summary>
    public bool IsFrozen {
        get {
            lock( this.gate ) {
                return this.isFrozen;
            }
        }
    }

    /// <summary>
    /// Prevents any further key creation or registration in this catalog.
    /// </summary>
    public void Freeze() {
        lock( this.gate ) {
            this.isFrozen = true;
        }
    }

    /// <summary>
    /// Gets or creates a scalar artifact key for the specified artifact name.
    /// </summary>
    /// <typeparam name="T">The artifact value type.</typeparam>
    /// <param name="name">The artifact name.</param>
    /// <returns>A cataloged <see cref="ArtifactKey{T}"/> instance.</returns>
    public ArtifactKey<T> Get<T>(ArtifactName name) {
        return GetCore<T, ArtifactKey<T>>(name, n => new ArtifactKey<T>(n));
    }

    /// <summary>
    /// Gets or creates a list-shaped artifact key for the specified artifact name.
    /// </summary>
    /// <typeparam name="TItem">The list item type.</typeparam>
    /// <param name="name">The artifact name.</param>
    /// <returns>A cataloged <see cref="ListArtifactKey{TItem}"/> instance.</returns>
    public ListArtifactKey<TItem> GetList<TItem>(ArtifactName name) {
        return GetCore<IReadOnlyList<TItem>, ListArtifactKey<TItem>>(name, n => new ListArtifactKey<TItem>(n));
    }

    /// <summary>
    /// Tries to get a scalar artifact key for the specified artifact name.
    /// </summary>
    /// <typeparam name="T">The artifact value type.</typeparam>
    /// <param name="name">The artifact name.</param>
    /// <param name="key">When this method returns, the matching key if found.</param>
    /// <returns><see langword="true"/> if the key exists; otherwise, <see langword="false"/>.</returns>
    public bool TryGet<T>(ArtifactName name, out ArtifactKey<T> key) {
        return TryGetCore<T, ArtifactKey<T>>(name, out key);
    }

    /// <summary>
    /// Tries to get a list-shaped artifact key for the specified artifact name.
    /// </summary>
    /// <typeparam name="TItem">The list item type.</typeparam>
    /// <param name="name">The artifact name.</param>
    /// <param name="key">When this method returns, the matching key if found.</param>
    /// <returns><see langword="true"/> if found; otherwise <see langword="false"/>.</returns>
    public bool TryGetList<TItem>(ArtifactName name, out ListArtifactKey<TItem> key) {
        return TryGetCore<IReadOnlyList<TItem>, ListArtifactKey<TItem>>(name, out key);
    }

    /// <summary>
    /// Registers an existing typed artifact key.
    /// </summary>
    /// <typeparam name="T">The artifact value type.</typeparam>
    /// <param name="key">The key to register.</param>
    public void Register<T>(IArtifactKey<T> key) {
        if( key is null )
            throw new ArgumentNullException(nameof(key));

        if( string.IsNullOrWhiteSpace(key.Name) )
            throw new ArgumentException(NAME_REQUIRED_MESSAGE, nameof(key));

        lock( this.gate ) {
            if( this.keysByName.TryGetValue(key.Name, out IArtifactKey existing) ) {
                if( existing is IArtifactKey<T> && existing.GetType() == key.GetType() )
                    return;

                if( existing is IArtifactKey<T> ) {
                    throw BuildFamilyMismatchException(key.Name, existing, key.GetType());
                }

                throw BuildTypeMismatchException(key.Name, existing, typeof(T));
            }

            if( this.isFrozen )
                throw new InvalidOperationException(FROZEN_MESSAGE);

            this.keysByName.Add(key.Name, key);
        }
    }

    /// <summary>
    /// Registers an existing scalar artifact key.
    /// </summary>
    /// <typeparam name="T">The artifact value type.</typeparam>
    /// <param name="key">The key to register.</param>
    public void Register<T>(ArtifactKey<T> key) {
        Register((IArtifactKey<T>)key);
    }

    /// <summary>
    /// Registers an existing list-shaped artifact key.
    /// </summary>
    /// <typeparam name="TItem">The list item type.</typeparam>
    /// <param name="key">The key to register.</param>
    public void Register<TItem>(ListArtifactKey<TItem> key) {
        Register((IArtifactKey<IReadOnlyList<TItem>>)key);
    }

    /// <summary>
    /// Returns a stable snapshot of all keys currently registered in the catalog.
    /// </summary>
    /// <returns>A read-only snapshot of the registered keys.</returns>
    public IReadOnlyList<IArtifactKey> Snapshot() {
        lock( this.gate ) {
            IArtifactKey[] arr = new IArtifactKey[this.keysByName.Count];
            int i = 0;
            foreach( IArtifactKey key in this.keysByName.Values )
                arr[i++] = key;

            return Array.AsReadOnly(arr);
        }
    }

    private TKey GetCore<TValue, TKey>(ArtifactName name, Func<ArtifactName, TKey> create)
        where TKey : class, IArtifactKey<TValue> {

        string n = ValidateAndGetName(name);

        lock( this.gate ) {
            if( this.keysByName.TryGetValue(n, out IArtifactKey existing) ) {
                if( existing is TKey typed )
                    return typed;

                if( existing is IArtifactKey<TValue> )
                    throw BuildFamilyMismatchException(n, existing, typeof(TKey));

                throw BuildTypeMismatchException(n, existing, typeof(TValue));
            }

            if( this.isFrozen )
                throw new InvalidOperationException(FROZEN_MESSAGE);

            TKey created = create(name) ?? throw new InvalidOperationException("Key factory returned null.");
            this.keysByName.Add(n, created);
            return created;
        }
    }

    private bool TryGetCore<TValue, TKey>(ArtifactName name, out TKey key)
        where TKey : class, IArtifactKey<TValue> {

        string n = ValidateAndGetName(name);

        lock( this.gate ) {
            if( !this.keysByName.TryGetValue(n, out IArtifactKey existing) ) {
                key = default!;
                return false;
            }

            if( existing is TKey typed ) {
                key = typed;
                return true;
            }

            if( existing is IArtifactKey<TValue> )
                throw BuildFamilyMismatchException(n, existing, typeof(TKey));

            throw BuildTypeMismatchException(n, existing, typeof(TValue));
        }
    }

    private static string ValidateAndGetName(ArtifactName name) {
        string n = name.Value;
        if( string.IsNullOrWhiteSpace(n) )
            throw new ArgumentException(NAME_REQUIRED_MESSAGE, nameof(name));

        return n;
    }

    private static InvalidOperationException BuildTypeMismatchException(string name, IArtifactKey existing, Type requestedValueType) {
        return new InvalidOperationException(
            $"Artifact key name is already registered with a different value type. name=\"{name}\" " +
            $"registeredType=\"{existing.ValueType.FullName}\" requestedType=\"{requestedValueType.FullName}\"");
    }

    private static InvalidOperationException BuildFamilyMismatchException(string name, IArtifactKey existing, Type requestedKeyType) {
        return new InvalidOperationException(
            $"{KEY_FAMILY_MISMATCH_MESSAGE} name=\"{name}\" " +
            $"registeredKeyType=\"{existing.GetType().FullName}\" requestedKeyType=\"{requestedKeyType.FullName}\" " +
            $"valueType=\"{existing.ValueType.FullName}\"");
    }
}
