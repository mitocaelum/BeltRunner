using System;
using System.Collections.Generic;
using BeltRunner.Core.Phase;

namespace BeltRunner.Core.Execution;

/// <summary>
/// Default immutable implementation of <see cref="IRunSnapshot"/>.
/// </summary>
public sealed class RunSnapshot : IRunSnapshot {
    /// <summary>
    /// Initializes a new instance of the <see cref="RunSnapshot"/> class.
    /// </summary>
    public RunSnapshot(
        RunStatus status,
        PhaseKey? currentPhaseKey,
        string? currentPhaseName,
        double overallRatio,
        IReadOnlyList<IPhaseSnapshot> phases) {

        Status = status;
        CurrentPhaseKey = currentPhaseKey;
        CurrentPhaseName = currentPhaseName;
        OverallRatio = overallRatio;
        Phases = phases ?? throw new ArgumentNullException(nameof(phases));
    }

    /// <inheritdoc />
    public RunStatus Status { get; }

    /// <inheritdoc />
    public PhaseKey? CurrentPhaseKey { get; }

    /// <inheritdoc />
    public string? CurrentPhaseName { get; }

    /// <inheritdoc />
    public double OverallRatio { get; }

    /// <inheritdoc />
    public IReadOnlyList<IPhaseSnapshot> Phases { get; }

}
