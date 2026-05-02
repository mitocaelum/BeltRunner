using System.Threading;
using System.Threading.Tasks;
namespace BeltRunner.Core.Phase;

/// <summary>
/// Represents a processing phase in a plan.
/// </summary>
/// <remarks>
/// <para>
/// A phase is a framework-defined processing step that participates in a run under the identity
/// declared by its <see cref="IPhaseFactory"/>.
/// </para>
/// <para>
/// A phase typically receives framework services through <see cref="ExecuteAsync"/>, such as reporting,
/// artifact access, and cancellation propagation, by way of <see cref="IPhaseContext"/>.
/// </para>
/// <para>
/// Implementations may process one or more units, update artifacts, publish reports, and produce an
/// <see cref="IPhaseOutcome"/> that describes the result of the phase execution.
/// </para>
/// </remarks>
public interface IPhase {
    /// <summary>
    /// Gets the set of units currently held by this phase.
    /// </summary>
    /// <value>
    /// The current <see cref="UnitSet"/> associated with this phase.
    /// </value>
    /// <remarks>
    /// <para>
    /// This collection represents the units that the phase has discovered or enrolled for processing.
    /// </para>
    /// <para>
    /// A phase may hold one concrete unit type or mix multiple <see cref="BeltRunner.Core.Units.IUnit"/> implementations
    /// in the same collection.
    /// </para>
    /// <para>
    /// BeltRunner runtime state is derived from this collection, so phases are expected to treat it as
    /// an append-only registry of tracked units rather than a temporary work queue.
    /// </para>
    /// </remarks>
    UnitSet Units { get; }

    /// <summary>
    /// Gets a human-readable name used for diagnostics and reporting.
    /// </summary>
    /// <value>
    /// A display-friendly name for logs, UI surfaces, and diagnostic output.
    /// </value>
    /// <remarks>
    /// <para>
    /// This value does not need to be globally unique.
    /// </para>
    /// <para>
    /// Choose a name that helps developers and operators quickly understand the role of the phase.
    /// </para>
    /// </remarks>
    string Name { get; }

    /// <summary>
    /// Executes the phase logic for the units and state currently associated with the phase.
    /// </summary>
    /// <param name="context">
    /// The execution context used for reporting, artifact access, and other framework services.
    /// </param>
    /// <param name="ct">
    /// A cancellation token that should be observed during execution.
    /// </param>
    /// <returns>
    /// A task that produces the outcome of the phase execution.
    /// </returns>
    /// <remarks>
    /// <para>
    /// This method represents the primary execution entry point for the phase.
    /// </para>
    /// <para>
    /// Implementations are expected to honor cancellation, use the supplied <paramref name="context"/>
    /// for framework interactions, and return an <see cref="IPhaseOutcome"/> describing the final result.
    /// </para>
    /// <para>
    /// The exact processing model is implementation-specific. A phase may process all currently held units,
    /// process only a subset, or coordinate additional internal logic before completing.
    /// </para>
    /// <para>
    /// Exceptions may still be thrown for unrecoverable failures if the implementation chooses not to convert
    /// them into a normal outcome object.
    /// </para>
    /// </remarks>
    Task<IPhaseOutcome> ExecuteAsync(IPhaseContext context, CancellationToken ct = default);
}
