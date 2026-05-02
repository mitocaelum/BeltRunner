using BeltRunner.Core.Execution.Interaction;
using BeltRunner.Core.Plan.Artifacts;
using System;
using System.Threading;

namespace BeltRunner.Core.Phase;

/// <summary>
/// Internal typed implementation of <see cref="IPhaseContext{TFactory}"/>.
/// </summary>
internal sealed class PhaseContext<TFactory> : PhaseContext, IPhaseContext<TFactory> {
    /// <inheritdoc />
    public TFactory Factory { get; }

    internal PhaseContext(
        PhaseKey phaseKey,
        CancellationToken cancellationToken,
        IArtifactReader artifacts,
        IInteractionRequester interaction,
        IPhaseTelemetry telemetry,
        TFactory factory)
        : base(phaseKey, cancellationToken, artifacts, interaction, telemetry) {

        if( factory is null ) {
            throw new ArgumentNullException(nameof(factory));
        }

        Factory = factory;
    }
}
