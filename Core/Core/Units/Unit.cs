using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Runtime.CompilerServices;
using BeltRunner.Core.Phase;

namespace BeltRunner.Core.Units;

/// <summary>
/// Provides a base implementation of <see cref="IUnit{T}"/> with property change notification support.
/// </summary>
/// <remarks>
/// <para>
/// <see cref="Unit{T}"/> is the default foundation for framework-managed units that flow through a BeltRunner plan.
/// It provides the core state required by the framework, including identity, display name,
/// framework-level status, current phase association, and tag management.
/// </para>
/// <para>
/// This type also implements <see cref="INotifyPropertyChanged"/> so that UI layers or observers can react to
/// changes in framework-managed properties such as <see cref="Status"/>, <see cref="CurrentPhaseKey"/>,
/// and <see cref="Tags"/>.
/// </para>
/// <para>
/// Application-specific unit types can derive from this class to add their own metadata, behavior,
/// or domain-specific state while reusing the standard BeltRunner tracking model.
/// </para>
/// <para>
/// This class is intended to expose a stable public surface for reading unit state,
/// while mutation of framework-managed state is restricted to BeltRunner internals through internal methods.
/// </para>
/// </remarks>
/// <typeparam name="T">
/// The type of payload carried by the unit.
/// </typeparam>
public abstract class Unit<T> : IUnit<T>, INotifyPropertyChanged, IRuntimeUnit {
    /// <inheritdoc cref="BeltRunner.Core.Units.IUnit.Id"/>
    /// <remarks>
    /// This value is generated automatically when the unit instance is created.
    /// </remarks>
    public Guid Id { get; }

    /// <inheritdoc cref="BeltRunner.Core.Units.IUnit.Name"/>
    /// <remarks>
    /// <para>
    /// This value is specified at construction time.
    /// </para>
    /// <para>
    /// If no usable name is provided, the string representation of <see cref="Id"/> is used instead.
    /// </para>
    /// </remarks>
    public string Name { get; }

    /// <inheritdoc/>
    public UnitStatus Status {
        get => this.status;
        private set => SetField(ref this.status, value);
    }
    private UnitStatus status;

    /// <inheritdoc/>
    public T Data { get; }

    /// <inheritdoc/>
    public PhaseKey? CurrentPhaseKey {
        get => this.currentPhaseKey;
        private set => SetField(ref this.currentPhaseKey, value);
    }
    private PhaseKey? currentPhaseKey;

    /// <summary>
    /// Initializes a new instance of the <see cref="Unit{T}"/> class.
    /// </summary>
    /// <param name="data">
    /// The payload carried by the unit.
    /// </param>
    /// <param name="name">
    /// An optional human-readable name.
    /// If <see langword="null"/>, empty, or whitespace, the generated <see cref="Id"/> is used as the name.
    /// </param>
    /// <param name="tags">
    /// An optional initial set of tags to assign to the unit.
    /// Duplicate tags are ignored.
    /// </param>
    /// <remarks>
    /// <para>
    /// A new unit starts in the <see cref="UnitStatus.Pending"/> state.
    /// </para>
    /// <para>
    /// The unit is not associated with any phase when created, so <see cref="CurrentPhaseKey"/> is initialized to
    /// <see langword="null"/>.
    /// </para>
    /// <para>
    /// If <paramref name="tags"/> is provided, the tags are copied into the internal tag set during construction.
    /// </para>
    /// </remarks>
    /// <exception cref="ArgumentNullException">
    /// Thrown when <paramref name="data"/> is <see langword="null"/>.
    /// </exception>
    public Unit(T data, string? name = null, IEnumerable<UnitTag>? tags = null) {
        if (data is null) throw new ArgumentNullException(nameof(data));

        Id = Guid.NewGuid();
        Name = string.IsNullOrWhiteSpace(name) ? Id.ToString() : name!;
        this.status = UnitStatus.Pending;
        Data = data;
        this.currentPhaseKey = null;

        if( tags is null ) return;
        
        foreach (UnitTag tag in tags) 
            this.tags.Add(tag);
    }

    #region Tags
    /// <inheritdoc/>
    /// <remarks>
    /// <para>
    /// The returned collection reflects the current framework-managed tag set.
    /// </para>
    /// <para>
    /// Because the underlying storage is a set, duplicate tags are not retained.
    /// </para>
    /// </remarks>
    public IReadOnlyCollection<UnitTag> Tags => this.tags;
    private readonly HashSet<UnitTag> tags = [];

    /// <summary>
    /// Adds a tag to the unit.
    /// </summary>
    /// <param name="tag">
    /// The tag to add.
    /// </param>
    /// <returns>
    /// <see langword="true"/> if the tag was added; otherwise, <see langword="false"/> if the tag was already present.
    /// </returns>
    /// <remarks>
    /// <para>
    /// This method is intended for BeltRunner internals only.
    /// </para>
    /// <para>
    /// If the tag set changes, <see cref="PropertyChanged"/> is raised for <see cref="Tags"/>.
    /// </para>
    /// </remarks>
    /// <exception cref="ArgumentNullException">
    /// Thrown when <paramref name="tag"/> is <see langword="null"/>.
    /// </exception>
    internal bool AddTag(UnitTag tag) {
        if (tag is null)
            throw new ArgumentNullException(nameof(tag));

        bool added = this.tags.Add(tag);
        if (added) OnPropertyChanged(nameof(Tags));

        return added;
    }

    /// <summary>
    /// Removes a tag from the unit.
    /// </summary>
    /// <param name="tag">
    /// The tag to remove.
    /// </param>
    /// <returns>
    /// <see langword="true"/> if the tag was removed; otherwise, <see langword="false"/> if the tag was not present.
    /// </returns>
    /// <remarks>
    /// <para>
    /// This method is intended for BeltRunner internals only.
    /// </para>
    /// <para>
    /// If the tag set changes, <see cref="PropertyChanged"/> is raised for <see cref="Tags"/>.
    /// </para>
    /// </remarks>
    /// <exception cref="ArgumentNullException">
    /// Thrown when <paramref name="tag"/> is <see langword="null"/>.
    /// </exception>
    internal bool RemoveTag(UnitTag tag) {
        if (tag is null)
            throw new ArgumentNullException(nameof(tag));

        bool removed = this.tags.Remove(tag);
        if (removed) OnPropertyChanged(nameof(Tags));

        return removed;
    }

    /// <summary>
    /// Replaces all tags assigned to the unit.
    /// </summary>
    /// <param name="tagSet">
    /// The tags to assign.
    /// Duplicate tags are ignored.
    /// </param>
    /// <remarks>
    /// <para>
    /// This method is intended for BeltRunner internals only.
    /// </para>
    /// <para>
    /// If the provided tag set is equivalent to the current tag set, no change is made and
    /// no property notification is raised.
    /// </para>
    /// <para>
    /// Otherwise, the existing set is cleared, the new tags are applied, and
    /// <see cref="PropertyChanged"/> is raised for <see cref="Tags"/>.
    /// </para>
    /// </remarks>
    /// <exception cref="ArgumentNullException">
    /// Thrown when <paramref name="tagSet"/> is <see langword="null"/>.
    /// </exception>
    internal void SetTags(IEnumerable<UnitTag> tagSet) {
        if (tagSet is null)
            throw new ArgumentNullException(nameof(tagSet));

        UnitTag[] normalized = tagSet.ToArray();

        if (this.tags.SetEquals(normalized)) return;

        this.tags.Clear();

        foreach (UnitTag tag in normalized) {
            this.tags.Add(tag);
        }

        OnPropertyChanged(nameof(Tags));
    }
    #endregion

    /// <summary>
    /// Updates the framework-level status of the unit.
    /// </summary>
    /// <param name="statusValue">
    /// The new status to assign.
    /// </param>
    /// <remarks>
    /// <para>
    /// This method is intended for BeltRunner internals only.
    /// </para>
    /// <para>
    /// When the value changes, <see cref="PropertyChanged"/> is raised for <see cref="Status"/>.
    /// </para>
    /// <para>
    /// Implementations may later enforce valid status transition rules here.
    /// </para>
    /// </remarks>
    internal void SetStatus(UnitStatus statusValue) {
        // TODO: Implement validation for status transitions.
        this.Status = statusValue;
    }

    /// <summary>
    /// Updates the current phase associated with the unit.
    /// </summary>
    /// <param name="phaseKey">
    /// The key of the phase to associate with the unit, or <see langword="null"/> to clear the association.
    /// </param>
    /// <remarks>
    /// <para>
    /// This method is intended for BeltRunner internals only.
    /// </para>
    /// <para>
    /// When the value changes, <see cref="PropertyChanged"/> is raised for <see cref="CurrentPhaseKey"/>.
    /// </para>
    /// </remarks>
    internal void SetPhase(PhaseKey? phaseKey) {
        CurrentPhaseKey = phaseKey;
    }

    void IRuntimeUnit.SetStatus(UnitStatus status) {
        SetStatus(status);
    }

    void IRuntimeUnit.SetPhase(PhaseKey? phaseKey) {
        SetPhase(phaseKey);
    }

    #region INotifyPropertyChanged
    /// <summary>
    /// Occurs when a property value changes.
    /// </summary>
    /// <remarks>
    /// This event supports observation of framework-managed state changes by UI layers and other listeners.
    /// </remarks>
    public event PropertyChangedEventHandler? PropertyChanged;

    /// <summary>
    /// Raises the <see cref="PropertyChanged"/> event.
    /// </summary>
    /// <param name="propertyName">
    /// The name of the property that changed.
    /// If omitted, the caller member name is used.
    /// </param>
    protected virtual void OnPropertyChanged([CallerMemberName] string? propertyName = null) {
        this.PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }

    /// <summary>
    /// Sets the backing field to the specified value if the value has changed.
    /// </summary>
    /// <typeparam name="TField">
    /// The type of the field being updated.
    /// </typeparam>
    /// <param name="field">
    /// A reference to the backing field.
    /// </param>
    /// <param name="value">
    /// The new value to assign.
    /// </param>
    /// <param name="propertyName">
    /// The name of the property associated with the field.
    /// If omitted, the caller member name is used.
    /// </param>
    /// <returns>
    /// <see langword="true"/> if the field value changed; otherwise, <see langword="false"/>.
    /// </returns>
    /// <remarks>
    /// If the value changes, this method updates the field and raises <see cref="PropertyChanged"/>.
    /// </remarks>
    protected bool SetField<TField>(ref TField field, TField value, [CallerMemberName] string? propertyName = null) {
        if (EqualityComparer<TField>.Default.Equals(field, value))
            return false;

        field = value;
        OnPropertyChanged(propertyName);
        return true;
    }
    #endregion
}
