using System;
using System.Threading;
using BeltRunner.Core.Execution.Interaction;
using BeltRunner.Core.Plan.Artifacts;

namespace BeltRunner.Core.Phase;

/// <summary>
/// Minimal implementation of <see cref="IPhaseContext"/>.
/// </summary>
internal class PhaseContext : IPhaseContext {
    /// <inheritdoc/>
    public PhaseKey Key { get; }

    /// <inheritdoc/>
    public CancellationToken CancellationToken { get; }

    /// <inheritdoc/>
    public IArtifactReader Artifacts { get; }

    /// <inheritdoc/>
    public IPhaseTelemetry Telemetry { get; }

    /// <inheritdoc/>
    public IInteractionRequester Interaction { get; }

    /// <summary>
    /// Initializes a new <see cref="PhaseContext"/> instance.
    /// </summary>
    internal PhaseContext(
        PhaseKey phaseKey,
        CancellationToken cancellationToken,
        IArtifactReader artifacts,
        IInteractionRequester interaction,
        IPhaseTelemetry telemetry) {

        Key = phaseKey ?? throw new ArgumentNullException(nameof(phaseKey));
        CancellationToken = cancellationToken;
        Artifacts = artifacts ?? throw new ArgumentNullException(nameof(artifacts));
        Interaction = interaction ?? throw new ArgumentNullException(nameof(interaction));
        Telemetry = telemetry ?? throw new ArgumentNullException(nameof(telemetry));
    }
}
