using BeltRunner.Core.Execution.Interaction;
using BeltRunner.Core.Plan.Artifacts;
using System;
using System.Threading;

namespace BeltRunner.Core.Phase;

/// <summary>
/// Provides a typed factory base that exposes the concrete factory itself through the typed execution context.
/// </summary>
/// <typeparam name="TFactory">
/// The concrete factory type exposed through <see cref="IPhaseContext{TFactory}.Factory"/>.
/// </typeparam>
/// <remarks>
/// <para>
/// This is the recommended authoring surface for new artifact-producing or artifact-consuming phases.
/// </para>
/// <para>
/// Derived types declare their consume and produce keys in the constructor and expose those keys as immutable
/// properties on the factory itself.
/// </para>
/// </remarks>
#pragma warning disable CS0618 // The generic factory base intentionally builds on the legacy non-generic base for compatibility.
public abstract class PhaseFactoryBase<TFactory> : PhaseFactoryBase, IPhaseFactory<TFactory>, IPhaseContextFactoryBridge
    where TFactory : PhaseFactoryBase<TFactory> {
    /// <summary>
    /// Initializes a new instance of the <see cref="PhaseFactoryBase{TFactory}"/> class.
    /// </summary>
    /// <param name="phaseKey">
    /// The stable key string of the phase.
    /// </param>
    protected PhaseFactoryBase(string phaseKey) : base(phaseKey) {
    }

    IPhaseContext IPhaseContextFactoryBridge.CreateContext(
        CancellationToken cancellationToken,
        IArtifactReader artifacts,
        IInteractionRequester interaction,
        IPhaseTelemetry telemetry) {

        if( this is not TFactory typedFactory ) {
            throw new InvalidOperationException(
                $"The typed factory base was closed with a mismatched self type. factoryType=\"{GetType().FullName}\" expectedSelfType=\"{typeof(TFactory).FullName}\"");
        }

        return new PhaseContext<TFactory>(Key, cancellationToken, artifacts, interaction, telemetry, typedFactory);
    }
}
#pragma warning restore CS0618
