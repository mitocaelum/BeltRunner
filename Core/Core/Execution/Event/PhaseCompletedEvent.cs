using System;
using BeltRunner.Core.Phase;

namespace BeltRunner.Core.Execution.Event;

/// <summary>
/// Published when a phase completes (after produced artifacts are merged).
/// </summary>
public sealed class PhaseCompletedEvent : RunEvent {
    /// <summary>
    /// Gets the key of the phase that completed.
    /// </summary>
    public PhaseKey PhaseKey { get; }

    /// <summary>
    /// Gets the zero-based index of the phase within the executed plan.
    /// </summary>
    public int PhaseIndex { get; }

    /// <summary>
    /// Gets the phase-level result reported by the phase outcome.
    /// </summary>
    public PhaseResult Result { get; }

    /// <summary>
    /// Initializes a new instance of the <see cref="PhaseCompletedEvent"/> class.
    /// </summary>
    /// <param name="runId">The identifier of the run that emitted the event.</param>
    /// <param name="phaseKey">The key of the completed phase.</param>
    /// <param name="phaseIndex">The zero-based index of the phase in the plan.</param>
    /// <param name="result">The result reported by the completed phase.</param>
    public PhaseCompletedEvent(Guid runId, PhaseKey phaseKey, int phaseIndex, PhaseResult result = PhaseResult.Succeeded) : base(runId) {
        PhaseKey = phaseKey ?? throw new ArgumentNullException(nameof(phaseKey));
        PhaseIndex = phaseIndex;
        Result = result;
    }
}
