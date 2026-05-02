using System;

namespace BeltRunner.Core.Host;

/// <summary>
/// Published when the active run faults and the host transitions to <see cref="HostState.Faulted"/>.
/// </summary>
/// <remarks>
/// <para>
/// This event represents a host-scope fault. It is raised when the active run completes with
/// <see cref="BeltRunner.Core.Execution.Outcome.RunOutcome"/> whose kind is
/// <see cref="BeltRunner.Core.Execution.Outcome.RunOutcomeKind.Faulted"/>, or when the host treats a completed
/// run as faulted based on its policy.
/// </para>
/// <para>
/// This is intentionally a host-level notification and does not include detailed phase information.
/// For the phase that caused the failure and other run-scope details, observe the returned run:
/// <see cref="BeltRunner.Core.Execution.IRun.EventStream"/> and <see cref="BeltRunner.Core.Execution.IRun.Completion"/>.
/// </para>
/// <para>
/// The <see cref="FaultInfo"/> property contains a sanitized summary of the host fault
/// without exposing a raw exception instance.
/// </para>
/// <para>
/// Raw exception details can still be written to NLog for trusted diagnostics when the application
/// configures NLog to capture BeltRunner loggers. BeltRunner does not register such rules automatically.
/// </para>
/// </remarks>
public sealed class HostFaultedEvent : HostEvent {
    internal Exception? SourceException { get; }

    /// <summary>
    /// Initializes a new instance of the <see cref="HostFaultedEvent"/> class.
    /// </summary>
    /// <param name="faultInfo">The sanitized fault summary for the host fault.</param>
    public HostFaultedEvent(BeltRunner.Core.Execution.PublicFaultInfo faultInfo) {
        FaultInfo = faultInfo ?? throw new ArgumentNullException(nameof(faultInfo));
    }

    internal HostFaultedEvent(Exception exception, BeltRunner.Core.Execution.PublicFaultInfo faultInfo) : this(faultInfo) {
        SourceException = exception ?? throw new ArgumentNullException(nameof(exception));
    }

    /// <summary>
    /// Gets the sanitized fault summary for the host fault.
    /// </summary>
    /// <remarks>
    /// This is the host-level public fault payload and is intended for runtime observation.
    /// It does not contain a raw exception instance.
    /// </remarks>
    public BeltRunner.Core.Execution.PublicFaultInfo FaultInfo { get; }
}
