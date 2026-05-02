using System;

namespace BeltRunner.Core.Execution.Event;

/// <summary>
/// Published when the run faults (before <see cref="Run.TryFault"/>).
/// </summary>
/// <remarks>
/// This event is the public event-stream representation of a run fault.
/// The event carries sanitized fault information only and does not expose a raw exception instance.
/// </remarks>
public sealed class RunFaultedEvent : RunEvent {
    internal Exception? SourceException { get; }

    /// <summary>
    /// Gets the sanitized fault summary for the run fault.
    /// </summary>
    /// <remarks>
    /// Use this property when subscribing to <see cref="IRun.EventStream"/>.
    /// Raw exception details are reserved for trusted diagnostics such as NLog, when configured.
    /// </remarks>
    public PublicFaultInfo FaultInfo { get; }

    /// <summary>
    /// Initializes a new instance of the <see cref="RunFaultedEvent"/> class.
    /// </summary>
    /// <param name="runId">The identifier of the run that emitted the event.</param>
    /// <param name="faultInfo">The sanitized fault summary for the run fault.</param>
    public RunFaultedEvent(Guid runId, PublicFaultInfo faultInfo) : base(runId) {
        FaultInfo = faultInfo ?? throw new ArgumentNullException(nameof(faultInfo));
    }

    internal RunFaultedEvent(Guid runId, Exception exception, PublicFaultInfo faultInfo) : this(runId, faultInfo) {
        SourceException = exception ?? throw new ArgumentNullException(nameof(exception));
    }
}
