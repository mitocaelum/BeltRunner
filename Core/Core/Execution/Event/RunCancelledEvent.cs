using System;
using BeltRunner.Core.Execution;

namespace BeltRunner.Core.Execution.Event;

/// <summary>
/// Published when the run completes as cancelled (before <see cref="Run.TryComplete"/>).
/// </summary>
public sealed class RunCancelledEvent : RunEvent {
    /// <summary>
    /// Gets the cancellation reason associated with the run, if one is available.
    /// </summary>
    public string? Reason { get; }

    /// <summary>
    /// Initializes a new instance of the <see cref="RunCancelledEvent"/> class.
    /// </summary>
    /// <param name="runId">The identifier of the run that emitted the event.</param>
    /// <param name="reason">The cancellation reason associated with the run, if one is available.</param>
    public RunCancelledEvent(Guid runId, string? reason) : base(runId) {
        Reason = TextConstraints.NormalizeNullable(reason, TextConstraints.CANCEL_REASON_MAX_LENGTH);
    }
}
