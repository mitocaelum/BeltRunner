using System;

namespace BeltRunner.Core.Phase;

/// <summary>
/// Represents a high-level scope for processing a single unit within a phase.
/// </summary>
/// <remarks>
/// <para>
/// The scope marks the unit as running when it is created, but the unit is considered completed successfully
/// only when <see cref="Complete"/> is called explicitly.
/// </para>
/// <para>
/// Disposing the scope without calling <see cref="Complete"/> is treated as an abandoned or externally-managed path,
/// not as an implicit success signal.
/// </para>
/// </remarks>
public interface ITrackedUnitScope : IDisposable {
    /// <summary>
    /// Marks the tracked unit as completed successfully.
    /// </summary>
    /// <remarks>
    /// This method is idempotent.
    /// A successful completion updates the unit state and increments the owning phase tracker by one completed unit.
    /// </remarks>
    void Complete();
}
