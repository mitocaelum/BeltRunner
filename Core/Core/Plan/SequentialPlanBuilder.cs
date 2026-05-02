using System;
using System.Collections.Generic;
using BeltRunner.Core.Phase;

namespace BeltRunner.Core.Plan;

/// <summary>
/// Builds a <see cref="SequentialPlan"/> using a fluent, mutable composition surface.
/// </summary>
/// <remarks>
/// This builder is intended as a convenience entry point for callers who want to declare
/// simple sequential plans without constructing low-level runtime state manually.
/// </remarks>
public sealed class SequentialPlanBuilder {
    private readonly List<SequentialPlanStep> steps = new();

    /// <summary>
    /// Adds a phase factory as the next sequential step.
    /// </summary>
    /// <param name="factory">The phase factory to add.</param>
    /// <param name="name">An optional display name for the step.</param>
    /// <returns>The current builder instance.</returns>
    public SequentialPlanBuilder Add(IPhaseFactory factory, string name = "") {
        if( factory is null ) {
            throw new ArgumentNullException(nameof(factory));
        }

        this.steps.Add(new SequentialPlanStep(factory, name));
        return this;
    }

    /// <summary>
    /// Resolves and adds a phase factory as the next sequential step.
    /// </summary>
    /// <param name="factoryProvider">The callback that supplies the phase factory.</param>
    /// <param name="name">An optional display name for the step.</param>
    /// <returns>The current builder instance.</returns>
    /// <remarks>
    /// This overload is intended for composition styles where factories come from an external container
    /// or another factory abstraction.
    /// </remarks>
    public SequentialPlanBuilder Add(Func<IPhaseFactory> factoryProvider, string name = "") {
        if( factoryProvider is null ) {
            throw new ArgumentNullException(nameof(factoryProvider));
        }

        IPhaseFactory factory = factoryProvider() ?? throw new InvalidOperationException("Phase factory provider returned null.");
        return Add(factory, name);
    }

    /// <summary>
    /// Builds the sequential plan.
    /// </summary>
    /// <returns>A new <see cref="SequentialPlan"/>.</returns>
    public SequentialPlan Build() {
        return new SequentialPlan(this.steps.ToArray());
    }
}
