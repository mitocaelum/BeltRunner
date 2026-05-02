namespace BeltRunner.Core.Units;

/// <summary>
/// Represents the framework-level lifecycle state of a unit.
/// </summary>
/// <remarks>
/// <para>
/// <see cref="UnitStatus"/> describes the processing lifecycle of a unit from the perspective of BeltRunner.
/// It is intended for framework-level tracking only.
/// </para>
/// <para>
/// Applications may define richer domain-specific states separately when needed,
/// but those states should not replace this framework-level status model.
/// </para>
/// <para>
/// Some values represent active or resumable states, while others represent terminal states
/// that indicate the unit has finished its lifecycle within the current run.
/// </para>
/// </remarks>
public enum UnitStatus {
    /// <summary>
    /// The unit is queued and waiting to be processed.
    /// </summary>
    /// <remarks>
    /// <para>
    /// This is the initial state for a newly created unit unless changed by the framework.
    /// </para>
    /// <para>
    /// The unit has not yet started active processing.
    /// </para>
    /// </remarks>
    Pending = 0,

    /// <summary>
    /// The unit is currently being processed.
    /// </summary>
    /// <remarks>
    /// <para>
    /// This is an active non-terminal state.
    /// </para>
    /// <para>
    /// A unit in this state is currently associated with ongoing framework execution.
    /// </para>
    /// </remarks>
    Running,

    /// <summary>
    /// The unit is temporarily paused and may resume later.
    /// </summary>
    /// <remarks>
    /// <para>
    /// This is a non-terminal state.
    /// </para>
    /// <para>
    /// It indicates that active processing has been suspended without being completed,
    /// and the unit may return to <see cref="Running"/> later.
    /// </para>
    /// </remarks>
    Paused,

    /// <summary>
    /// The unit completed successfully.
    /// </summary>
    /// <remarks>
    /// <para>
    /// This is a terminal state.
    /// </para>
    /// <para>
    /// The unit finished processing without failure, cancellation, or skip handling.
    /// </para>
    /// </remarks>
    Succeeded,

    /// <summary>
    /// The unit was intentionally skipped.
    /// </summary>
    /// <remarks>
    /// <para>
    /// This is a terminal state.
    /// </para>
    /// <para>
    /// BeltRunner treats this as a completed outcome, even though the unit was not fully processed.
    /// </para>
    /// <para>
    /// This value is appropriate when skipping is an intentional and non-error decision.
    /// </para>
    /// </remarks>
    Skipped,

    /// <summary>
    /// The unit completed with warnings.
    /// </summary>
    /// <remarks>
    /// <para>
    /// This is a terminal state.
    /// </para>
    /// <para>
    /// BeltRunner treats this as a completed outcome, but one that indicates notable issues,
    /// degraded conditions, or non-fatal concerns encountered during processing.
    /// </para>
    /// </remarks>
    Warning,

    /// <summary>
    /// The unit ended due to a failure.
    /// </summary>
    /// <remarks>
    /// <para>
    /// This is a terminal state.
    /// </para>
    /// <para>
    /// Use this value when the unit could not complete successfully because of an error condition
    /// or an unrecoverable processing problem.
    /// </para>
    /// </remarks>
    Failed,

    /// <summary>
    /// The unit was cancelled before completion.
    /// </summary>
    /// <remarks>
    /// <para>
    /// This is a terminal state.
    /// </para>
    /// <para>
    /// This value indicates that processing ended because cancellation was requested
    /// before the unit reached a completed outcome.
    /// </para>
    /// </remarks>
    Cancelled
}