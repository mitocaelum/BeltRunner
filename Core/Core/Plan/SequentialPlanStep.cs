using System;
using BeltRunner.Core.Phase;

namespace BeltRunner.Core.Plan;

internal sealed class SequentialPlanStep {
    public SequentialPlanStep(IPhaseFactory factory, string name) {
        this.Factory = factory ?? throw new ArgumentNullException(nameof(factory));
        this.Name = string.IsNullOrWhiteSpace(name) ? GetDefaultName(factory) : name;
    }

    public string Name { get; }

    public IPhaseFactory Factory { get; }

    private static string GetDefaultName(IPhaseFactory factory) {
        PhaseKey phaseKey = factory.Key ?? throw new InvalidOperationException("PhaseKey is null.");
        return phaseKey.Value;
    }
}
