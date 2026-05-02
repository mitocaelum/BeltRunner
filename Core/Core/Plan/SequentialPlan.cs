using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using BeltRunner.Core.Phase;

namespace BeltRunner.Core.Plan;

/// <summary>
/// Represents a simple plan that stores phases in execution order.
/// </summary>
/// <remarks>
/// Use the factory-based constructors for the common case where each phase maps to one default step name.
/// </remarks>
public sealed class SequentialPlan {
    private readonly ReadOnlyCollection<SequentialPlanStep> steps;

    /// <summary>
    /// Initializes a new instance of the <see cref="SequentialPlan"/> class.
    /// </summary>
    /// <param name="factories">
    /// The phase factories declared in execution order.
    /// </param>
    /// <remarks>
    /// Each factory is normalized to a sequential step that uses the default step name behavior.
    /// </remarks>
    public SequentialPlan(IEnumerable<IPhaseFactory> factories)
        : this(CreateSteps(factories)) {
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="SequentialPlan"/> class.
    /// </summary>
    /// <param name="factories">
    /// The phase factories declared in execution order.
    /// </param>
    /// <remarks>
    /// This overload is a convenience for simple sequential plan declarations.
    /// </remarks>
    public SequentialPlan(params IPhaseFactory[] factories)
        : this((IEnumerable<IPhaseFactory>)factories) {
    }

    internal SequentialPlan(IEnumerable<SequentialPlanStep> steps) {
        if( steps is null ) {
            throw new ArgumentNullException(nameof(steps));
        }

        SequentialPlanStep[] materializedSteps = System.Linq.Enumerable.ToArray(steps);
        Validate(materializedSteps);

        this.steps = Array.AsReadOnly(materializedSteps);
    }

    internal IReadOnlyList<SequentialPlanStep> Steps => this.steps;

    private static IReadOnlyList<SequentialPlanStep> CreateSteps(IEnumerable<IPhaseFactory> factories) {
        if( factories is null ) {
            throw new ArgumentNullException(nameof(factories));
        }

        List<SequentialPlanStep> steps = new();
        foreach( IPhaseFactory factory in factories ) {
            if( factory is null ) {
                steps.Add(null!);
            } else {
                steps.Add(new SequentialPlanStep(factory, string.Empty));
            }
        }

        return steps;
    }

    private static void Validate(IReadOnlyList<SequentialPlanStep> steps) {
        HashSet<PhaseKey> keys = new();

        foreach( SequentialPlanStep step in steps ) {
            if( step is null ) {
                throw new ArgumentException("The step collection must not contain null.", nameof(steps));
            }

            PhaseKey phaseKey = step.Factory.Key ?? throw new InvalidOperationException("PhaseKey is null.");
            if( !keys.Add(phaseKey) ) {
                throw new ArgumentException(
                    $"A duplicate phase key was found in the sequential plan: '{phaseKey}'.",
                    nameof(steps));
            }
        }
    }
}
