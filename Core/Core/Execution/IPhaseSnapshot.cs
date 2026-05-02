using System;
using System.Collections.Generic;
using BeltRunner.Core.Phase;

namespace BeltRunner.Core.Execution;

/// <summary>
/// Represents an immutable snapshot of one phase within a run.
/// </summary>
/// <remarks>
/// A phase snapshot combines lifecycle state, aggregate progress, and unit-level progress for a single phase.
/// </remarks>
public interface IPhaseSnapshot {
    /// <summary>
    /// Gets the phase key.
    /// </summary>
    /// <remarks>
    /// This key is stable across the lifetime of the run and matches the key declared by the phase factory.
    /// </remarks>
    PhaseKey PhaseKey { get; }

    /// <summary>
    /// Gets the phase display name.
    /// </summary>
    /// <remarks>
    /// This value is intended for UI, logs, and other human-facing surfaces.
    /// </remarks>
    string PhaseName { get; }

    /// <summary>
    /// Gets the zero-based phase index.
    /// </summary>
    /// <remarks>
    /// The index reflects plan order, not execution order in any future non-sequential planner.
    /// </remarks>
    int PhaseIndex { get; }

    /// <summary>
    /// Gets the current phase status.
    /// </summary>
    PhaseStatus Status { get; }

    /// <summary>
    /// Gets the best-known total unit count used for progress calculation.
    /// </summary>
    /// <remarks>
    /// This value may be inferred from observed units or supplied explicitly through telemetry.
    /// </remarks>
    int? TotalUnits { get; }

    /// <summary>
    /// Gets the best-known number of completed units for this phase.
    /// This value may come from terminal unit states, explicit high-level tracking, or both.
    /// </summary>
    /// <remarks>
    /// This value is intended for aggregate progress reporting and may be ahead of the number of units whose
    /// final terminal status has already been observed in the snapshot.
    /// </remarks>
    int ProcessedUnits { get; }

    /// <summary>
    /// Gets the current phase ratio in the range [0, 1].
    /// </summary>
    /// <remarks>
    /// The runtime may derive this ratio from unit-level fractional progress, aggregate completed-unit tracking, or a combination of both.
    /// </remarks>
    double Ratio { get; }

    /// <summary>
    /// Gets the unit snapshots associated with this phase.
    /// </summary>
    /// <remarks>
    /// The returned list contains the units that the phase has registered with the runtime so far.
    /// </remarks>
    IReadOnlyList<IUnitSnapshot> Units { get; }
}
