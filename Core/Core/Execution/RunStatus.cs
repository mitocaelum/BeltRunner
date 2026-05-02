namespace BeltRunner.Core.Execution;

/// <summary>
/// Represents the lifecycle state of a run snapshot.
/// </summary>
public enum RunStatus {
    /// <summary>
    /// The run has been created but execution has not started yet.
    /// </summary>
    Created = 0,

    /// <summary>
    /// The run is actively executing phases.
    /// </summary>
    Running,

    /// <summary>
    /// Cancellation was requested and the run is winding down.
    /// </summary>
    Cancelling,

    /// <summary>
    /// The run completed normally.
    /// </summary>
    Completed,

    /// <summary>
    /// The run ended because cancellation was requested.
    /// </summary>
    Cancelled,

    /// <summary>
    /// The run ended because of an unrecoverable error.
    /// </summary>
    Faulted
}
