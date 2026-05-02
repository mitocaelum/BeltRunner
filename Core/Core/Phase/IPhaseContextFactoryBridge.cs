using BeltRunner.Core.Execution.Interaction;
using BeltRunner.Core.Plan.Artifacts;
using System.Threading;

namespace BeltRunner.Core.Phase;

internal interface IPhaseContextFactoryBridge {
    IPhaseContext CreateContext(
        CancellationToken cancellationToken,
        IArtifactReader artifacts,
        IInteractionRequester interaction,
        IPhaseTelemetry telemetry);
}
