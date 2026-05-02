namespace BeltRunner.Core.Execution.Outcome;

/// <summary>
/// Identifies the settled terminal kind of a run.
/// </summary>
public enum RunOutcomeKind {
    /// <summary>
    /// The run completed successfully without reported warnings or failures.
    /// </summary>
    Succeeded = 0,

    /// <summary>
    /// The run completed, but at least one phase reported a partial-success condition.
    /// </summary>
    PartiallySucceeded = 1,

    /// <summary>
    /// The run completed without throwing, but at least one phase reported failure.
    /// </summary>
    Failed = 2,

    /// <summary>
    /// The run ended because cancellation was requested.
    /// </summary>
    Cancelled = 3,

    /// <summary>
    /// The run ended because of an unhandled exception.
    /// </summary>
    Faulted = 4
}
