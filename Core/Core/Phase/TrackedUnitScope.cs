using System;
using System.Threading;
using BeltRunner.Core.Units;

namespace BeltRunner.Core.Phase;

internal sealed class TrackedUnitScope : ITrackedUnitScope {
    private readonly Guid unitId;
    private readonly Action<Guid, double> setUnitProgress;
    private readonly Action<Guid, UnitStatus> setUnitStatus;
    private readonly Action onCompleted;
    private int completed;

    public TrackedUnitScope(
        Guid unitId,
        Action<Guid, double> setUnitProgress,
        Action<Guid, UnitStatus> setUnitStatus,
        Action onCompleted) {

        this.unitId = unitId;
        this.setUnitProgress = setUnitProgress ?? throw new ArgumentNullException(nameof(setUnitProgress));
        this.setUnitStatus = setUnitStatus ?? throw new ArgumentNullException(nameof(setUnitStatus));
        this.onCompleted = onCompleted ?? throw new ArgumentNullException(nameof(onCompleted));
    }

    public void Complete() {
        if( Interlocked.Exchange(ref this.completed, 1) != 0 ) {
            return;
        }

        this.setUnitProgress(this.unitId, 1.0);
        this.setUnitStatus(this.unitId, UnitStatus.Succeeded);
        this.onCompleted();
    }

    public void Dispose() {
    }
}
