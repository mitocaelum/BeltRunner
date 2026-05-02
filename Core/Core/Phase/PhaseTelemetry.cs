using System;
using BeltRunner.Core.Execution;
using BeltRunner.Core.Units;

namespace BeltRunner.Core.Phase;

internal sealed class PhaseTelemetry : IPhaseTelemetry {
    private readonly Run run;
    private readonly PhaseKey phaseKey;

    public PhaseTelemetry(Run run, PhaseKey phaseKey) {
        this.run = run ?? throw new ArgumentNullException(nameof(run));
        this.phaseKey = phaseKey ?? throw new ArgumentNullException(nameof(phaseKey));
    }

    public void SetTotalUnits(int? totalUnits) {
        this.run.SetPhaseTotalUnits(this.phaseKey, totalUnits);
    }

    public IPhaseProgressTracker BeginPhaseProgressTracking(int totalUnits) {
        return new PhaseProgressTracker(
            totalUnits,
            SetTotalUnits,
            SetProcessedUnits,
            SetUnitProgress,
            SetUnitStatus);
    }

    public void SetUnitProgress(Guid unitId, double ratio) {
        this.run.SetUnitProgress(this.phaseKey, unitId, ratio);
    }

    public void SetUnitStatus(Guid unitId, UnitStatus status) {
        this.run.SetUnitStatus(this.phaseKey, unitId, status);
    }

    private void SetProcessedUnits(int processedUnits) {
        this.run.SetPhaseProcessedUnits(this.phaseKey, processedUnits);
    }

    public void PublishDiagnostic(DiagnosticSeverity severity, string message, Exception? exception = null, Guid? unitId = null) {
        this.run.PublishDiagnostic(this.phaseKey, severity, message, exception, unitId);
    }
}
