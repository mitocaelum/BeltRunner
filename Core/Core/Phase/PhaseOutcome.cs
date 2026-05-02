using System.Collections.Generic;
using BeltRunner.Core.Plan.Artifacts;

namespace BeltRunner.Core.Phase;

/// <summary>
/// Mutable builder-style implementation of <see cref="IPhaseOutcome"/>.
/// </summary>
/// <remarks>
/// <para>
/// This type is intended to be assembled by phase implementations and returned from
/// <see cref="IPhase.ExecuteAsync"/>.
/// </para>
/// <para>
/// Most methods return <c>this</c> to support fluent composition.
/// </para>
/// </remarks>
public sealed class PhaseOutcome : IPhaseOutcome {
    private readonly List<IProducedArtifact> produced = new();

    /// <summary>
    /// Initializes a new <see cref="PhaseOutcome"/> instance.
    /// </summary>
    /// <param name="result">
    /// Initial phase result. Defaults to <see cref="PhaseResult.Succeeded"/>.
    /// </param>
    /// <param name="continuation">
    /// Initial continuation suggestion. Defaults to <see cref="PhaseContinuation.Continue"/>.
    /// </param>
    public PhaseOutcome(PhaseResult result = PhaseResult.Succeeded, PhaseContinuation continuation = PhaseContinuation.Continue) {
        this.Result = result;
        this.Continuation = continuation;
    }

    /// <inheritdoc/>
    public PhaseResult Result { get; private set; }

    /// <inheritdoc/>
    public PhaseContinuation Continuation { get; private set; }

    /// <inheritdoc/>
    public IReadOnlyList<IProducedArtifact> Produced => this.produced;

    /// <summary>
    /// Appends a produced artifact entry.
    /// </summary>
    /// <typeparam name="T">
    /// The artifact value type.
    /// </typeparam>
    /// <param name="key">
    /// The produced artifact key.
    /// </param>
    /// <param name="value">
    /// The produced artifact value.
    /// </param>
    /// <returns>
    /// This instance for fluent chaining.
    /// </returns>
    /// <exception cref="System.ArgumentNullException">
    /// Thrown when <paramref name="key"/> or <paramref name="value"/> is <see langword="null"/>.
    /// </exception>
    public PhaseOutcome Produce<T>(IArtifactKey<T> key, T value) {
        this.produced.Add(new ProducedArtifact<T>(key, value));
        return this;
    }

    /// <summary>
    /// Sets <see cref="Result"/>.
    /// </summary>
    /// <param name="result">
    /// The new phase result.
    /// </param>
    /// <returns>
    /// This instance for fluent chaining.
    /// </returns>
    public PhaseOutcome WithResult(PhaseResult result) {
        this.Result = result;
        return this;
    }

    /// <summary>
    /// Sets <see cref="Continuation"/>.
    /// </summary>
    /// <param name="continuation">
    /// The new continuation suggestion.
    /// </param>
    /// <returns>
    /// This instance for fluent chaining.
    /// </returns>
    public PhaseOutcome WithContinuation(PhaseContinuation continuation) {
        this.Continuation = continuation;
        return this;
    }

    /// <summary>
    /// Sets the result to <see cref="PhaseResult.Skipped"/>.
    /// </summary>
    /// <returns>
    /// This instance for fluent chaining.
    /// </returns>
    public PhaseOutcome Skipped() {
        this.Result = PhaseResult.Skipped;
        return this;
    }

    /// <summary>
    /// Sets the result to <see cref="PhaseResult.Failed"/> and continuation to <see cref="PhaseContinuation.Halt"/>.
    /// </summary>
    /// <returns>
    /// This instance for fluent chaining.
    /// </returns>
    public PhaseOutcome FailedAndHalt() {
        this.Result = PhaseResult.Failed;
        this.Continuation = PhaseContinuation.Halt;
        return this;
    }
}
