namespace BeltRunner.Core.Host;

/// <summary>
/// Base type for host-scope events published by <see cref="Host"/>.
/// </summary>
/// <remarks>
/// Host events represent host-level notifications (for example, state changes and host faults).
/// They must not include detailed run or phase events; use run-scope observables for those details.
/// </remarks>
public class HostEvent {
}