using System;

namespace BeltRunner.Core.Execution.Event;

/// <summary>
/// Published when the run completes without an unhandled exception (before <see cref="Run.TryComplete"/>).
/// This does not necessarily mean that all phases succeeded.
/// </summary>
public sealed class RunCompletedEvent : RunEvent {
    /// <summary>
    /// Initializes a new instance of the <see cref="RunCompletedEvent"/> class.
    /// </summary>
    /// <param name="runId">The identifier of the run that emitted the event.</param>
    public RunCompletedEvent(Guid runId) : base(runId) {
    }
}
