using System;
using System.Collections.Generic;
using BeltRunner.Core.Plan;
using BeltRunner.Core.Plan.Artifacts;
namespace BeltRunner.Core.Phase;

/// <summary>
/// Provides a base implementation of <see cref="IPhaseFactory"/>.
/// </summary>
/// <remarks>
/// <para>
/// This base class stores the factory key and tracks declared consume/produce artifact contracts.
/// </para>
/// <para>
/// It is suitable for phases that manage one unit type or multiple unit types.
/// </para>
/// <para>
/// For new typed artifact-contract authoring, prefer <see cref="PhaseFactoryBase{TContract}"/>.
/// </para>
/// </remarks>
[Obsolete("Use PhaseFactoryBase<TFactory> for new implementations. This non-generic base remains only for legacy and low-level compatibility.")]
public abstract class PhaseFactoryBase : IPhaseFactory {
    /// <summary>
    /// Initializes a new instance of the <see cref="PhaseFactoryBase"/> class.
    /// </summary>
    /// <param name="phaseKey">
    /// The stable key string of the phase.
    /// </param>
    protected PhaseFactoryBase(string phaseKey) {
        Key = new PhaseKey(phaseKey);
    }

    #region Key
    /// <inheritdoc/>
    public PhaseKey Key { get; }

    /// <summary>
    /// Creates an artifact key using the provided logical name.
    /// </summary>
    /// <typeparam name="TValue">
    /// The artifact value type.
    /// </typeparam>
    /// <param name="logicalName">
    /// The logical artifact name.
    /// </param>
    /// <returns>
    /// A strongly typed artifact key.
    /// </returns>
    protected ArtifactKey<TValue> SharedKey<TValue>(string logicalName) {
        ArtifactName name = ArtifactName.Create(logicalName);
        return new ArtifactKey<TValue>(name);
    }

    /// <summary>
    /// Creates a list-shaped artifact key using the provided logical name.
    /// </summary>
    /// <typeparam name="TItem">The list item type.</typeparam>
    /// <param name="logicalName">The logical artifact name.</param>
    /// <returns>A list-shaped artifact key.</returns>
    protected ListArtifactKey<TItem> SharedList<TItem>(string logicalName) {
        ArtifactName name = ArtifactName.Create(logicalName);
        return new ListArtifactKey<TItem>(name);
    }
    #endregion

    #region Consumes / Produces
    /// <inheritdoc/>
    public IReadOnlyList<IArtifactKey> Consumes => this.consumes;
    private readonly List<IArtifactKey> consumes = new();

    /// <inheritdoc/>
    public IReadOnlyList<IArtifactKey> Produces => this.produces;
    private readonly List<IArtifactKey> produces = new();

    /// <summary>
    /// Declares an artifact consumed by the phase and returns its key.
    /// </summary>
    /// <typeparam name="TValue">
    /// The artifact value type.
    /// </typeparam>
    /// <param name="logicalName">
    /// The logical artifact name.
    /// </param>
    /// <returns>
    /// The declared artifact key.
    /// </returns>
    protected ArtifactKey<TValue> Consume<TValue>(string logicalName) {
        ArtifactKey<TValue> key = SharedKey<TValue>(logicalName);
        this.consumes.Add(key);
        return key;
    }

    /// <summary>
    /// Declares a list-shaped artifact consumed by the phase and returns its key.
    /// </summary>
    /// <typeparam name="TItem">The list item type.</typeparam>
    /// <param name="logicalName">The logical artifact name.</param>
    /// <returns>The declared list artifact key.</returns>
    protected ListArtifactKey<TItem> ConsumeList<TItem>(string logicalName) {
        ListArtifactKey<TItem> key = SharedList<TItem>(logicalName);
        this.consumes.Add(key);
        return key;
    }

    /// <summary>
    /// Declares an existing artifact key consumed by the phase.
    /// </summary>
    /// <typeparam name="TValue">
    /// The artifact value type.
    /// </typeparam>
    /// <param name="key">
    /// The pre-defined artifact key.
    /// </param>
    /// <returns>
    /// The declared artifact key.
    /// </returns>
    /// <exception cref="ArgumentNullException">
    /// Thrown when <paramref name="key"/> is <see langword="null"/>.
    /// </exception>
    protected IArtifactKey<TValue> Consume<TValue>(IArtifactKey<TValue> key) {
        if( key is null ) {
            throw new ArgumentNullException(nameof(key));
        }

        this.consumes.Add(key);
        return key;
    }

    /// <summary>
    /// Declares an existing <see cref="ArtifactKey{T}"/> consumed by the phase.
    /// </summary>
    /// <typeparam name="TValue">The artifact value type.</typeparam>
    /// <param name="key">The pre-defined artifact key.</param>
    /// <returns>The declared artifact key.</returns>
    protected ArtifactKey<TValue> Consume<TValue>(ArtifactKey<TValue> key) {
        return (ArtifactKey<TValue>)Consume((IArtifactKey<TValue>)key);
    }

    /// <summary>
    /// Declares an existing <see cref="ListArtifactKey{TItem}"/> consumed by the phase.
    /// </summary>
    /// <typeparam name="TItem">The list item type.</typeparam>
    /// <param name="key">The pre-defined list artifact key.</param>
    /// <returns>The declared artifact key.</returns>
    protected ListArtifactKey<TItem> Consume<TItem>(ListArtifactKey<TItem> key) {
        return (ListArtifactKey<TItem>)Consume((IArtifactKey<IReadOnlyList<TItem>>)key);
    }

    /// <summary>
    /// Declares an artifact produced by the phase and returns its key.
    /// </summary>
    /// <typeparam name="TValue">
    /// The artifact value type.
    /// </typeparam>
    /// <param name="logicalName">
    /// The logical artifact name.
    /// </param>
    /// <returns>
    /// The declared artifact key.
    /// </returns>
    protected ArtifactKey<TValue> Produce<TValue>(string logicalName) {
        ArtifactKey<TValue> key = SharedKey<TValue>(logicalName);
        this.produces.Add(key);
        return key;
    }

    /// <summary>
    /// Declares a list-shaped artifact produced by the phase and returns its key.
    /// </summary>
    /// <typeparam name="TItem">The list item type.</typeparam>
    /// <param name="logicalName">The logical artifact name.</param>
    /// <returns>The declared list artifact key.</returns>
    protected ListArtifactKey<TItem> ProduceList<TItem>(string logicalName) {
        ListArtifactKey<TItem> key = SharedList<TItem>(logicalName);
        this.produces.Add(key);
        return key;
    }

    /// <summary>
    /// Declares an existing artifact key produced by the phase.
    /// </summary>
    /// <typeparam name="TValue">
    /// The artifact value type.
    /// </typeparam>
    /// <param name="key">
    /// The pre-defined artifact key.
    /// </param>
    /// <returns>
    /// The declared artifact key.
    /// </returns>
    /// <exception cref="ArgumentNullException">
    /// Thrown when <paramref name="key"/> is <see langword="null"/>.
    /// </exception>
    protected IArtifactKey<TValue> Produce<TValue>(IArtifactKey<TValue> key) {
        if( key is null ) {
            throw new ArgumentNullException(nameof(key));
        }

        this.produces.Add(key);
        return key;
    }

    /// <summary>
    /// Declares an existing <see cref="ArtifactKey{T}"/> produced by the phase.
    /// </summary>
    /// <typeparam name="TValue">The artifact value type.</typeparam>
    /// <param name="key">The pre-defined artifact key.</param>
    /// <returns>The declared artifact key.</returns>
    protected ArtifactKey<TValue> Produce<TValue>(ArtifactKey<TValue> key) {
        return (ArtifactKey<TValue>)Produce((IArtifactKey<TValue>)key);
    }

    /// <summary>
    /// Declares an existing <see cref="ListArtifactKey{TItem}"/> produced by the phase.
    /// </summary>
    /// <typeparam name="TItem">The list item type.</typeparam>
    /// <param name="key">The pre-defined list artifact key.</param>
    /// <returns>The declared artifact key.</returns>
    protected ListArtifactKey<TItem> Produce<TItem>(ListArtifactKey<TItem> key) {
        return (ListArtifactKey<TItem>)Produce((IArtifactKey<IReadOnlyList<TItem>>)key);
    }
    #endregion

    /// <summary>
    /// Creates a new phase instance.
    /// </summary>
    /// <returns>
    /// A new phase instance.
    /// </returns>
    public abstract IPhase Create();
}
