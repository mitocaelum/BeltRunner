using System;
using BeltRunner.Core.Phase;

namespace BeltRunner.Core.Execution.Event;

/// <summary>
/// Published when a phase starts executing.
/// </summary>
public sealed class PhaseStartedEvent : RunEvent {
    /// <summary>
    /// Gets the key of the phase that started.
    /// </summary>
    public PhaseKey PhaseKey { get; }

    /// <summary>
    /// Gets the zero-based index of the phase within the executed plan.
    /// </summary>
    public int PhaseIndex { get; }

    /// <summary>
    /// Initializes a new instance of the <see cref="PhaseStartedEvent"/> class.
    /// </summary>
    /// <param name="runId">The identifier of the run that emitted the event.</param>
    /// <param name="phaseKey">The key of the phase that started.</param>
    /// <param name="phaseIndex">The zero-based index of the phase in the plan.</param>
    public PhaseStartedEvent(Guid runId, PhaseKey phaseKey, int phaseIndex) : base(runId) {
        PhaseKey = phaseKey ?? throw new ArgumentNullException(nameof(phaseKey));
        PhaseIndex = phaseIndex;
    }
}
