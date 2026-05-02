using System;
using System.Collections.Generic;
using BeltRunner.Core.Phase;

namespace BeltRunner.Core.Execution;

/// <summary>
/// Represents an immutable snapshot of the current run state.
/// </summary>
/// <remarks>
/// <para>
/// Snapshot instances are point-in-time views that can be safely cached, logged, or rendered by UI code without
/// holding a live lock on the run.
/// </para>
/// <para>
/// Each new snapshot supersedes the previous current-state view, but older snapshots remain valid historical data.
/// </para>
/// </remarks>
public interface IRunSnapshot {
    /// <summary>
    /// Gets the current run status captured by this snapshot.
    /// </summary>
    /// <remarks>
    /// <see cref="IRunSnapshot"/> is the authoritative current-state representation for a run.
    /// <see cref="IRun.Status"/> mirrors this value as a convenience shortcut.
    /// </remarks>
    RunStatus Status { get; }

    /// <summary>
    /// Gets the key of the currently running phase, if one exists.
    /// </summary>
    /// <remarks>
    /// This value is typically <see langword="null"/> before the first phase starts and after the run has reached a terminal state.
    /// </remarks>
    PhaseKey? CurrentPhaseKey { get; }

    /// <summary>
    /// Gets the display name of the currently running phase, if one exists.
    /// </summary>
    /// <remarks>
    /// This value tracks the same phase as <see cref="CurrentPhaseKey"/> and is exposed as a display-oriented convenience.
    /// </remarks>
    string? CurrentPhaseName { get; }

    /// <summary>
    /// Gets the overall run ratio in the range [0, 1].
    /// </summary>
    /// <remarks>
    /// This ratio is an aggregate progress view across the run's phases.
    /// The exact weighting model is runtime-defined, but the value is always normalized to the inclusive range <c>[0, 1]</c>.
    /// </remarks>
    double OverallRatio { get; }

    /// <summary>
    /// Gets the phase snapshots in plan order.
    /// </summary>
    /// <remarks>
    /// The returned list covers every phase in the plan, including phases that have not started yet.
    /// </remarks>
    IReadOnlyList<IPhaseSnapshot> Phases { get; }
}
