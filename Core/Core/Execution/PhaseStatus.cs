namespace BeltRunner.Core.Execution;

/// <summary>
/// Represents the lifecycle state of a phase snapshot.
/// </summary>
public enum PhaseStatus {
    /// <summary>
    /// The phase has not started yet.
    /// </summary>
    Pending = 0,

    /// <summary>
    /// The phase is currently running.
    /// </summary>
    Running,

    /// <summary>
    /// The phase completed normally.
    /// </summary>
    Completed,

    /// <summary>
    /// The phase ended because cancellation was requested.
    /// </summary>
    Cancelled,

    /// <summary>
    /// The phase ended because of an unrecoverable error.
    /// </summary>
    Faulted
}
