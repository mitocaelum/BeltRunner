using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using BeltRunner.Core.Units;

namespace BeltRunner.Core.Phase;

/// <summary>
/// Represents an append-only, framework-managed set of units owned by a phase.
/// </summary>
/// <remarks>
/// <para>
/// This collection stores units by <see cref="IUnit.Id"/> and can contain any combination of
/// <see cref="IUnit"/> implementations.
/// </para>
/// <para>
/// Units may be added until <see cref="Lock"/> is called. Existing units are not removed from this
/// collection because BeltRunner derives runtime history from the phase-owned set.
/// </para>
/// <para>
/// The <see cref="Changed"/> event is raised whenever the contents or lock state changes.
/// BeltRunner uses this signal to refresh runtime snapshots from the phase-owned unit collection.
/// </para>
/// </remarks>
public class UnitSet : IEnumerable<IUnit> {
    private readonly object syncRoot = new object();
    private readonly Dictionary<Guid, IUnit> unitsById = new Dictionary<Guid, IUnit>();
    private bool isLocked;

    /// <summary>
    /// Occurs when the set contents or lock state changes.
    /// </summary>
    public event EventHandler? Changed;

    /// <summary>
    /// Gets a value indicating whether this set is locked.
    /// </summary>
    /// <value>
    /// <see langword="true"/> when the set is immutable; otherwise <see langword="false"/>.
    /// </value>
    public bool IsLocked {
        get {
            lock( this.syncRoot ) {
                return this.isLocked;
            }
        }
    }

    /// <summary>
    /// Gets the number of units in the set.
    /// </summary>
    public int Count {
        get {
            lock( this.syncRoot ) {
                return this.unitsById.Count;
            }
        }
    }

    /// <summary>
    /// Gets a snapshot of units currently contained in the set.
    /// </summary>
    /// <value>
    /// A snapshot copy of current units.
    /// </value>
    public IReadOnlyCollection<IUnit> Items {
        get {
            lock( this.syncRoot ) {
                return this.unitsById.Values.ToArray();
            }
        }
    }

    /// <summary>
    /// Locks the set and disallows subsequent mutations.
    /// </summary>
    public void Lock() {
        bool raiseChanged = false;

        lock( this.syncRoot ) {
            if( !this.isLocked ) {
                this.isLocked = true;
                raiseChanged = true;
            }
        }

        if( raiseChanged ) {
            OnChanged();
        }
    }

    /// <summary>
    /// Adds the specified unit to the set and then locks the set.
    /// </summary>
    /// <param name="unit">
    /// The unit to add.
    /// </param>
    /// <exception cref="ArgumentNullException">
    /// Thrown when <paramref name="unit"/> is <see langword="null"/>.
    /// </exception>
    /// <exception cref="InvalidOperationException">
    /// Thrown when the set is locked, or when a unit with the same identifier already exists.
    /// </exception>
    public void AddAndLock(IUnit unit) {
        lock( this.syncRoot ) {
            EnsureNotLocked();
            ValidateUnit(unit);
            this.unitsById.Add(unit.Id, unit);
            this.isLocked = true;
        }

        OnChanged();
    }

    /// <summary>
    /// Adds the specified unit to the set.
    /// </summary>
    /// <param name="unit">
    /// The unit to add.
    /// </param>
    /// <exception cref="ArgumentNullException">
    /// Thrown when <paramref name="unit"/> is <see langword="null"/>.
    /// </exception>
    /// <exception cref="InvalidOperationException">
    /// Thrown when the set is locked, or when a unit with the same identifier already exists.
    /// </exception>
    public void Add(IUnit unit) {
        lock( this.syncRoot ) {
            EnsureNotLocked();
            ValidateUnit(unit);
            this.unitsById.Add(unit.Id, unit);
        }

        OnChanged();
    }

    /// <summary>
    /// Tries to add the specified unit to the set.
    /// </summary>
    /// <param name="unit">
    /// The unit to add.
    /// </param>
    /// <returns>
    /// <see langword="true"/> if the unit was added; otherwise, <see langword="false"/>.
    /// </returns>
    /// <exception cref="ArgumentNullException">
    /// Thrown when <paramref name="unit"/> is <see langword="null"/>.
    /// </exception>
    /// <exception cref="InvalidOperationException">
    /// Thrown when the set is locked.
    /// </exception>
    public bool TryAdd(IUnit unit) {
        bool added;

        lock( this.syncRoot ) {
            EnsureNotLocked();

            if( unit is null ) throw new ArgumentNullException(nameof(unit));
            if( this.unitsById.ContainsKey(unit.Id) ) return false;

            this.unitsById.Add(unit.Id, unit);
            added = true;
        }

        if( added ) {
            OnChanged();
        }

        return true;
    }

    /// <summary>
    /// Adds the specified units to the set.
    /// </summary>
    /// <param name="units">
    /// The units to add.
    /// </param>
    /// <exception cref="ArgumentNullException">
    /// Thrown when <paramref name="units"/> is <see langword="null"/>, or when any contained unit is <see langword="null"/>.
    /// </exception>
    /// <exception cref="InvalidOperationException">
    /// Thrown when the set is locked, when a unit with the same identifier already exists,
    /// or when duplicate identifiers are present in <paramref name="units"/>.
    /// </exception>
    public void AddRange(IEnumerable<IUnit> units) {
        if( units is null ) throw new ArgumentNullException(nameof(units));

        lock( this.syncRoot ) {
            EnsureNotLocked();

            IUnit[] buffered = units.ToArray();
            ValidateUnits(buffered);

            for( int i = 0; i < buffered.Length; i++ ) {
                IUnit unit = buffered[i];
                this.unitsById.Add(unit.Id, unit);
            }
        }

        OnChanged();
    }

    /// <summary>
    /// Adds the specified units to the set and then locks the set.
    /// </summary>
    /// <param name="units">
    /// The units to add.
    /// </param>
    /// <exception cref="ArgumentNullException">
    /// Thrown when <paramref name="units"/> is <see langword="null"/>, or when any contained unit is <see langword="null"/>.
    /// </exception>
    /// <exception cref="InvalidOperationException">
    /// Thrown when the set is locked, when a unit with the same identifier already exists,
    /// or when duplicate identifiers are present in <paramref name="units"/>.
    /// </exception>
    public void AddRangeAndLock(IEnumerable<IUnit> units) {
        if( units is null ) throw new ArgumentNullException(nameof(units));

        lock( this.syncRoot ) {
            EnsureNotLocked();

            IUnit[] buffered = units.ToArray();
            ValidateUnits(buffered);

            for( int i = 0; i < buffered.Length; i++ ) {
                IUnit unit = buffered[i];
                this.unitsById.Add(unit.Id, unit);
            }

            this.isLocked = true;
        }

        OnChanged();
    }

    /// <summary>
    /// Determines whether the set contains the specified unit identifier.
    /// </summary>
    /// <param name="unitId">
    /// The unit identifier to locate.
    /// </param>
    /// <returns>
    /// <see langword="true"/> if the unit exists; otherwise, <see langword="false"/>.
    /// </returns>
    public bool Contains(Guid unitId) {
        lock( this.syncRoot ) {
            return this.unitsById.ContainsKey(unitId);
        }
    }

    /// <summary>
    /// Determines whether the set contains the specified unit.
    /// </summary>
    /// <param name="unit">
    /// The unit to locate.
    /// </param>
    /// <returns>
    /// <see langword="true"/> if the unit exists; otherwise, <see langword="false"/>.
    /// </returns>
    public bool Contains(IUnit unit) {
        if( unit is null ) {
            return false;
        }

        lock( this.syncRoot ) {
            return this.unitsById.ContainsKey(unit.Id);
        }
    }

    /// <summary>
    /// Tries to get a unit by its identifier.
    /// </summary>
    /// <param name="unitId">
    /// The identifier of the unit to get.
    /// </param>
    /// <param name="unit">
    /// When this method returns, contains the located unit if found; otherwise, <see langword="null"/>.
    /// </param>
    /// <returns>
    /// <see langword="true"/> if the unit was found; otherwise, <see langword="false"/>.
    /// </returns>
    public bool TryGet(Guid unitId, out IUnit? unit) {
        lock( this.syncRoot ) {
            return this.unitsById.TryGetValue(unitId, out unit);
        }
    }

    /// <summary>
    /// Returns a filtered snapshot of units assignable to <typeparamref name="TUnit"/>.
    /// </summary>
    /// <typeparam name="TUnit">
    /// The unit type to filter.
    /// </typeparam>
    /// <returns>
    /// A snapshot sequence of matching units.
    /// </returns>
    public IReadOnlyCollection<TUnit> OfType<TUnit>()
        where TUnit : IUnit {
        lock( this.syncRoot ) {
            return this.unitsById.Values.OfType<TUnit>().ToArray();
        }
    }

    /// <summary>
    /// Returns an enumerator that iterates through a snapshot of the set.
    /// </summary>
    /// <returns>
    /// An enumerator for the current snapshot.
    /// </returns>
    public IEnumerator<IUnit> GetEnumerator() {
        IUnit[] snapshot;

        lock( this.syncRoot ) {
            snapshot = this.unitsById.Values.ToArray();
        }

        return ((IEnumerable<IUnit>)snapshot).GetEnumerator();
    }

    IEnumerator IEnumerable.GetEnumerator() {
        return GetEnumerator();
    }

    /// <summary>
    /// Raises the <see cref="Changed"/> event.
    /// </summary>
    protected virtual void OnChanged() {
        this.Changed?.Invoke(this, EventArgs.Empty);
    }

    private void EnsureNotLocked() {
        if( this.isLocked ) {
            throw new InvalidOperationException("The unit set is locked and cannot be modified.");
        }
    }

    private void ValidateUnits(IEnumerable<IUnit> units) {
        HashSet<Guid> seen = new HashSet<Guid>(this.unitsById.Keys);

        foreach( IUnit unit in units ) {
            if( unit is null ) throw new ArgumentNullException(nameof(units));
            if( !seen.Add(unit.Id) ) throw new InvalidOperationException($"A unit with the same id already exists. Id={unit.Id}");
        }
    }

    private void ValidateUnit(IUnit unit) {
        if( unit is null ) throw new ArgumentNullException(nameof(unit));
        if( this.unitsById.ContainsKey(unit.Id) ) throw new InvalidOperationException($"A unit with the same id already exists. Id={unit.Id}");
    }
}
