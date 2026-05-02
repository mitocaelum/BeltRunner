namespace BeltRunner.Core.Phase;

/// <summary>
/// Indicates whether execution should proceed to downstream phases.
/// </summary>
/// <remarks>
/// This value is a phase-side recommendation, not the final control decision.
/// </remarks>
public enum PhaseContinuation {
    /// <summary>
    /// Suggests that the runner continue with the next phase.
    /// </summary>
    Continue,

    /// <summary>
    /// Suggests that the runner stop before executing downstream phases.
    /// </summary>
    Halt
}
