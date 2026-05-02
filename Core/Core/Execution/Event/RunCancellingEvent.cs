using System;

namespace BeltRunner.Core.Execution.Event;

/// <summary>
/// Published when cancellation has been requested for a run that has already entered execution.
/// </summary>
public sealed class RunCancellingEvent : RunEvent {
    /// <summary>
    /// Initializes a new instance of the <see cref="RunCancellingEvent"/> class.
    /// </summary>
    /// <param name="runId">The identifier of the run that emitted the event.</param>
    public RunCancellingEvent(Guid runId) : base(runId) {
    }
}
