using System;
using System.Collections.Generic;
using BeltRunner.Core.Phase;

namespace BeltRunner.Core.Execution;

/// <summary>
/// Default immutable implementation of <see cref="IPhaseSnapshot"/>.
/// </summary>
public sealed class PhaseSnapshot : IPhaseSnapshot {
    /// <summary>
    /// Initializes a new instance of the <see cref="PhaseSnapshot"/> class.
    /// </summary>
    public PhaseSnapshot(
        PhaseKey phaseKey,
        string name,
        int index,
        PhaseStatus status,
        int? totalUnits,
        int processedUnits,
        double ratio,
        IReadOnlyList<IUnitSnapshot> units) {

        PhaseKey = phaseKey ?? throw new ArgumentNullException(nameof(phaseKey));
        PhaseName = name ?? throw new ArgumentNullException(nameof(name));
        PhaseIndex = index;
        Status = status;
        TotalUnits = totalUnits;
        ProcessedUnits = processedUnits;
        Ratio = ratio;
        Units = units ?? throw new ArgumentNullException(nameof(units));
    }

    /// <inheritdoc />
    public PhaseKey PhaseKey { get; }

    /// <inheritdoc />
    public string PhaseName { get; }

    /// <inheritdoc />
    public int PhaseIndex { get; }

    /// <inheritdoc />
    public PhaseStatus Status { get; }

    /// <inheritdoc />
    public int? TotalUnits { get; }

    /// <inheritdoc />
    public int ProcessedUnits { get; }

    /// <inheritdoc />
    public double Ratio { get; }

    /// <inheritdoc />
    public IReadOnlyList<IUnitSnapshot> Units { get; }

}
