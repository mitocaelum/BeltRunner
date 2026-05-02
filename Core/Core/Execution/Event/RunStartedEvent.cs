using System;

namespace BeltRunner.Core.Execution.Event;

/// <summary>
/// Published when the run execution loop starts.
/// </summary>
public sealed class RunStartedEvent : RunEvent {
    /// <summary>
    /// Initializes a new instance of the <see cref="RunStartedEvent"/> class.
    /// </summary>
    /// <param name="runId">The identifier of the run that emitted the event.</param>
    public RunStartedEvent(Guid runId) : base(runId) {
    }
}
