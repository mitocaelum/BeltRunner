using System;
using BeltRunner.Core.Phase;

namespace BeltRunner.Core.Execution.Event;

/// <summary>
/// Published when a phase faults.
/// </summary>
/// <remarks>
/// This event is the public event-stream representation of a phase fault.
/// The event carries sanitized fault information only and does not expose a raw exception instance.
/// </remarks>
public sealed class PhaseFaultedEvent : RunEvent {
    internal Exception? SourceException { get; }

    /// <summary>
    /// Gets the key of the phase that faulted.
    /// </summary>
    public PhaseKey PhaseKey { get; }

    /// <summary>
    /// Gets the zero-based index of the phase within the executed plan.
    /// </summary>
    public int PhaseIndex { get; }

    /// <summary>
    /// Gets the sanitized fault summary for the phase fault.
    /// </summary>
    /// <remarks>
    /// Use this property when subscribing to <see cref="IRun.EventStream"/>.
    /// Raw exception details are reserved for trusted diagnostics such as NLog, when configured.
    /// </remarks>
    public PublicFaultInfo FaultInfo { get; }

    /// <summary>
    /// Initializes a new instance of the <see cref="PhaseFaultedEvent"/> class.
    /// </summary>
    /// <param name="runId">The identifier of the run that emitted the event.</param>
    /// <param name="phaseKey">The key of the faulted phase.</param>
    /// <param name="phaseIndex">The zero-based index of the phase in the plan.</param>
    /// <param name="faultInfo">The sanitized fault summary for the phase fault.</param>
    public PhaseFaultedEvent(Guid runId, PhaseKey phaseKey, int phaseIndex, PublicFaultInfo faultInfo) : base(runId) {
        PhaseKey = phaseKey ?? throw new ArgumentNullException(nameof(phaseKey));
        PhaseIndex = phaseIndex;
        FaultInfo = faultInfo ?? throw new ArgumentNullException(nameof(faultInfo));
    }

    internal PhaseFaultedEvent(Guid runId, PhaseKey phaseKey, int phaseIndex, Exception exception, PublicFaultInfo faultInfo) : this(runId, phaseKey, phaseIndex, faultInfo) {
        SourceException = exception ?? throw new ArgumentNullException(nameof(exception));
    }
}
