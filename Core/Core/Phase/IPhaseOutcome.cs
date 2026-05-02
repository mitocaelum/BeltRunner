using System.Collections.Generic;
using BeltRunner.Core.Plan.Artifacts;

namespace BeltRunner.Core.Phase;

/// <summary>
/// Represents the execution outcome returned by <see cref="IPhase.ExecuteAsync"/>.
/// </summary>
/// <remarks>
/// <para>
/// A phase outcome combines three concerns: what happened, whether downstream phases should continue,
/// and which artifacts were produced.
/// </para>
/// <para>
/// The runner can apply policy on top of this outcome, but this contract is the phase-side declaration
/// of intent.
/// </para>
/// </remarks>
public interface IPhaseOutcome {
    /// <summary>
    /// Gets the phase result.
    /// </summary>
    /// <value>
    /// A <see cref="PhaseResult"/> describing whether the phase succeeded, skipped, failed, or was cancelled.
    /// </value>
    PhaseResult Result { get; }

    /// <summary>
    /// Gets the continuation suggestion emitted by the phase.
    /// </summary>
    /// <value>
    /// A <see cref="PhaseContinuation"/> indicating whether execution should continue or halt.
    /// </value>
    /// <remarks>
    /// The runner may override this suggestion according to host policy.
    /// </remarks>
    PhaseContinuation Continuation { get; }

    /// <summary>
    /// Gets the artifacts produced by the phase.
    /// </summary>
    /// <value>
    /// A read-only list of <see cref="IProducedArtifact"/> entries to merge into the run-level artifact store.
    /// </value>
    IReadOnlyList<IProducedArtifact> Produced { get; }
}
