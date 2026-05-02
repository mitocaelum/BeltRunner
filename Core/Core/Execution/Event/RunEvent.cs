using System;

namespace BeltRunner.Core.Execution.Event;

/// <summary>
/// Base type for run-scope events.
/// </summary>
public abstract class RunEvent {
    /// <summary>
    /// Gets the run id that emitted this event.
    /// </summary>
    public Guid RunId { get; }

    /// <summary>
    /// Initializes a new instance of the <see cref="RunEvent"/> class.
    /// </summary>
    /// <param name="runId">The identifier of the run that emitted the event.</param>
    protected RunEvent(Guid runId) {
        RunId = runId;
    }
}
