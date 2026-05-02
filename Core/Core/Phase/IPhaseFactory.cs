using System.Collections.Generic;
using BeltRunner.Core.Plan;
using BeltRunner.Core.Plan.Artifacts;
namespace BeltRunner.Core.Phase;

/// <summary>
/// Creates phase instances and declares their artifact contract.
/// </summary>
/// <remarks>
/// Factories define the stable plan-time metadata for a phase, including its key and the artifact keys it consumes
/// and produces. The runtime uses this metadata for preflight validation before invoking <see cref="Create"/>.
/// For new typed artifact-contract authoring, prefer <see cref="IPhaseFactory{TContract}"/> together with
/// <see cref="PhaseFactoryBase{TContract}"/>.
/// </remarks>
public interface IPhaseFactory {
    /// <summary>
    /// Gets the stable phase identifier.
    /// </summary>
    /// <value>
    /// A <see cref="PhaseKey"/> used for plan wiring, diagnostics, and provenance.
    /// </value>
    PhaseKey Key { get; }

    /// <summary>
    /// Gets artifacts required before phase execution begins.
    /// </summary>
    /// <value>
    /// A read-only list of consumed artifact keys.
    /// </value>
    IReadOnlyList<IArtifactKey> Consumes { get; }

    /// <summary>
    /// Gets artifacts that this phase is allowed to publish.
    /// </summary>
    /// <value>
    /// A read-only list of produced artifact keys declared by this factory.
    /// </value>
    IReadOnlyList<IArtifactKey> Produces { get; }

    /// <summary>
    /// Creates a new phase instance.
    /// </summary>
    /// <returns>
    /// A new phase instance exposed through the non-generic execution contract.
    /// </returns>
    IPhase Create();
}
