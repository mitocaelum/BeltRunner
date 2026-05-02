using BeltRunner.Core.Phase;

namespace BeltRunner.Core.Units;

internal interface IRuntimeUnit {
    void SetStatus(UnitStatus status);
    void SetPhase(PhaseKey? phaseKey);
}
