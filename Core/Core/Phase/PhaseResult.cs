namespace BeltRunner.Core.Phase;

/// <summary>
/// Describes the outcome category of a phase execution.
/// </summary>
public enum PhaseResult {
    /// <summary>
    /// The phase completed successfully.
    /// </summary>
    Succeeded,

    /// <summary>
    /// The phase intentionally skipped execution.
    /// </summary>
    Skipped,

    /// <summary>
    /// The phase completed with failure semantics.
    /// </summary>
    Failed,

    /// <summary>
    /// The phase completed due to cancellation.
    /// </summary>
    Cancelled,

    /// <summary>
    /// The phase completed with mixed success and failure.
    /// </summary>
    PartiallySucceeded
}
