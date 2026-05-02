using BeltRunner.Core.Execution.Interaction;
using BeltRunner.Core.Plan.Artifacts;
using System.Threading;

namespace BeltRunner.Core.Phase;

/// <summary>
/// Represents the runtime context passed to a phase during execution.
/// </summary>
/// <remarks>
/// <para>
/// This context provides phase-safe access to run-scope services such as artifact reads,
/// interactions, and structured telemetry writes.
/// </para>
/// <para>
/// It is intentionally narrow so that <see cref="IPhase"/> implementations can depend on a stable,
/// framework-owned contract instead of concrete runner internals.
/// </para>
/// <para>
/// For new phase implementations that want a typed artifact contract, prefer
/// <see cref="IPhaseContext{TContract}"/> together with <see cref="PhaseBase{TContract}"/>
/// and <see cref="PhaseFactoryBase{TContract}"/>.
/// </para>
/// </remarks>
public interface IPhaseContext {
    /// <summary>
    /// Gets the key of the currently executing phase.
    /// </summary>
    PhaseKey Key { get; }

    /// <summary>
    /// Gets the run-scoped cancellation token propagated to the phase.
    /// </summary>
    CancellationToken CancellationToken { get; }

    /// <summary>
    /// Gets the run-scope artifact reader.
    /// </summary>
    IArtifactReader Artifacts { get; }

    /// <summary>
    /// Gets the structured phase telemetry writer.
    /// </summary>
    IPhaseTelemetry Telemetry { get; }

    /// <summary>
    /// Gets the requester used to publish interaction requests for user decisions.
    /// </summary>
    IInteractionRequester Interaction { get; }
}
