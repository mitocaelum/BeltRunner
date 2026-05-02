using System;
using BeltRunner.Core.Units;

namespace BeltRunner.Core.Execution;

/// <summary>
/// Represents an immutable snapshot of a tracked unit inside a phase.
/// </summary>
/// <remarks>
/// Unit snapshots are phase-scoped.
/// The same logical business item may appear in different phases as different unit snapshots.
/// </remarks>
public interface IUnitSnapshot {
    /// <summary>
    /// Gets the unit identifier.
    /// </summary>
    Guid Id { get; }

    /// <summary>
    /// Gets the display name of the unit.
    /// </summary>
    /// <remarks>
    /// This value is intended for diagnostics and UI surfaces and does not need to be globally unique.
    /// </remarks>
    string Name { get; }

    /// <summary>
    /// Gets the current unit status.
    /// </summary>
    UnitStatus Status { get; }

    /// <summary>
    /// Gets the current unit ratio in the range [0, 1].
    /// </summary>
    /// <remarks>
    /// This ratio reflects the best-known progress for the unit within the owning phase only.
    /// </remarks>
    double Ratio { get; }
}
