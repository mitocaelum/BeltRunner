using System;
using BeltRunner.Core.Units;

namespace BeltRunner.Core.Execution;

/// <summary>
/// Default immutable implementation of <see cref="IUnitSnapshot"/>.
/// </summary>
public sealed class UnitSnapshot : IUnitSnapshot {
    /// <summary>
    /// Initializes a new instance of the <see cref="UnitSnapshot"/> class.
    /// </summary>
    public UnitSnapshot(Guid id, string name, UnitStatus status, double ratio) {
        Id = id;
        Name = name ?? throw new ArgumentNullException(nameof(name));
        Status = status;
        Ratio = ratio;
    }

    /// <inheritdoc />
    public Guid Id { get; }

    /// <inheritdoc />
    public string Name { get; }

    /// <inheritdoc />
    public UnitStatus Status { get; }

    /// <inheritdoc />
    public double Ratio { get; }
}
