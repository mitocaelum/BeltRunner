namespace BeltRunner.Core.Host;

/// <summary>
/// Published when the host lifecycle state changes.
/// </summary>
/// <remarks>
/// <para>
/// This is a host-scope notification. It is intended for consumers that need to update UI state,
/// enable or disable commands, or react to cancellation and termination at the host level.
/// </para>
/// <para>
/// The new state is provided by <see cref="NewState"/>. Typical transitions include:
/// <c>Idle</c> → <c>Running</c> → (<c>Completed</c> | <c>Faulted</c>) and
/// <c>Running</c> → <c>Cancelling</c> → <c>Cancelled</c>.
/// </para>
/// <para>
/// This event does not include run/phase details. For phase start/end and other execution details,
/// observe <see cref="BeltRunner.Core.Execution.IRun.EventStream"/> on the started run.
/// </para>
/// </remarks>
public sealed class HostStateChangedEvent(HostState newState) : HostEvent {
    /// <summary>
    /// Gets the new host state after the transition.
    /// </summary>
    public HostState NewState { get; } = newState;
}
