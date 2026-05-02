using BeltRunner.Core.Units;
using System;
using System.Threading;
using System.Threading.Tasks;

namespace BeltRunner.Core.Phase;

/// <summary>
/// Provides a typed phase base class that exposes factory metadata through the execution context.
/// </summary>
/// <typeparam name="TFactory">
/// The concrete factory type exposed through the execution context.
/// </typeparam>
/// <remarks>
/// <para>
/// This is the recommended base class for new phase implementations that want a typed authoring path.
/// </para>
/// <para>
/// Runtime execution still flows through <see cref="IPhase"/>, but the bridge method verifies that the supplied
/// context carries the expected <typeparamref name="TFactory"/> and fails fast when that factory metadata is missing.
/// </para>
/// </remarks>
public abstract class PhaseBase<TFactory> : IPhase {
    /// <inheritdoc />
    public abstract UnitSet Units { get; }

    /// <inheritdoc />
    public abstract string Name { get; }

    /// <summary>
    /// Executes the phase with typed factory metadata.
    /// </summary>
    /// <param name="context">
    /// The execution context that carries the typed factory metadata.
    /// </param>
    /// <param name="ct">
    /// A cancellation token that should be observed during execution.
    /// </param>
    /// <returns>
    /// A task that produces the outcome of the phase execution.
    /// </returns>
    public abstract Task<IPhaseOutcome> ExecuteAsync(IPhaseContext<TFactory> context, CancellationToken ct = default);

    async Task<IPhaseOutcome> IPhase.ExecuteAsync(IPhaseContext context, CancellationToken ct) {
        if( context is not IPhaseContext<TFactory> typedContext ) {
            throw new InvalidOperationException(
                $"Phase received a non-matching typed context. phaseType=\"{GetType().FullName}\" expectedFactoryType=\"{typeof(TFactory).FullName}\" actualContextType=\"{context?.GetType().FullName ?? "null"}\"");
        }

        return await ExecuteAsync(typedContext, ct).ConfigureAwait(false);
    }
}
