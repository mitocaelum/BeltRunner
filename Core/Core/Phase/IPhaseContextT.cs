using BeltRunner.Core.Execution.Interaction;
using BeltRunner.Core.Plan.Artifacts;
using System.Threading;

namespace BeltRunner.Core.Phase;

/// <summary>
/// Represents a runtime context that also carries the typed factory metadata for the current phase.
/// </summary>
/// <typeparam name="TFactory">
/// The factory type that declared the artifact keys and created the current phase.
/// </typeparam>
/// <remarks>
/// <para>
/// This is the recommended authoring surface for new phases that want to consume or produce artifacts
/// without copying artifact keys through phase constructors.
/// </para>
/// <para>
/// The factory instance is attached to the runtime context for the duration of phase execution so that
/// phases can read factory-owned artifact keys and other immutable metadata directly from
/// <see cref="Factory"/>.
/// </para>
/// </remarks>
public interface IPhaseContext<TFactory> : IPhaseContext {
    /// <summary>
    /// Gets the factory instance that declared the artifact contract and created the current phase.
    /// </summary>
    TFactory Factory { get; }
}
