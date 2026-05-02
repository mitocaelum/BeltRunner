using System;
using System.Threading;
using BeltRunner.Core.Units;

namespace BeltRunner.Core.Phase;

internal sealed class PhaseProgressTracker : IPhaseProgressTracker {
    private readonly Action<int?> setTotalUnits;
    private readonly Action<int> setProcessedUnits;
    private readonly Action<Guid, double> setUnitProgress;
    private readonly Action<Guid, UnitStatus> setUnitStatus;
    private readonly int totalUnits;
    private int completedUnits;

    public PhaseProgressTracker(
        int totalUnits,
        Action<int?> setTotalUnits,
        Action<int> setProcessedUnits,
        Action<Guid, double> setUnitProgress,
        Action<Guid, UnitStatus> setUnitStatus) {

        if( totalUnits <= 0 ) {
            throw new ArgumentOutOfRangeException(nameof(totalUnits), "Total units must be greater than zero.");
        }

        this.setTotalUnits = setTotalUnits ?? throw new ArgumentNullException(nameof(setTotalUnits));
        this.setProcessedUnits = setProcessedUnits ?? throw new ArgumentNullException(nameof(setProcessedUnits));
        this.setUnitProgress = setUnitProgress ?? throw new ArgumentNullException(nameof(setUnitProgress));
        this.setUnitStatus = setUnitStatus ?? throw new ArgumentNullException(nameof(setUnitStatus));
        this.totalUnits = totalUnits;

        this.setTotalUnits(totalUnits);
    }

    public void ReportCompleted(int completedUnits) {
        if( completedUnits < 0 ) {
            throw new ArgumentOutOfRangeException(nameof(completedUnits), "Completed units must be zero or greater.");
        }

        PublishCompleted(Math.Min(completedUnits, this.totalUnits));
    }

    public ITrackedUnitScope BeginUnit(Guid unitId) {
        this.setUnitStatus(unitId, UnitStatus.Running);
        return new TrackedUnitScope(unitId, this.setUnitProgress, this.setUnitStatus, CompleteOne);
    }

    public ITrackedUnitScope BeginUnit(IUnit unit) {
        if( unit is null ) {
            throw new ArgumentNullException(nameof(unit));
        }

        return BeginUnit(unit.Id);
    }

    public void Dispose() {
    }

    private void CompleteOne() {
        while( true ) {
            int current = Volatile.Read(ref this.completedUnits);
            if( current >= this.totalUnits ) {
                return;
            }

            int next = current + 1;
            if( Interlocked.CompareExchange(ref this.completedUnits, next, current) == current ) {
                this.setProcessedUnits(next);
                return;
            }
        }
    }

    private void PublishCompleted(int normalizedCompletedUnits) {
        while( true ) {
            int current = Volatile.Read(ref this.completedUnits);
            if( normalizedCompletedUnits <= current ) {
                return;
            }

            if( Interlocked.CompareExchange(ref this.completedUnits, normalizedCompletedUnits, current) == current ) {
                this.setProcessedUnits(normalizedCompletedUnits);
                return;
            }
        }
    }
}
