using System;
using System.Collections.Generic;
using System.Reactive.Subjects;
using System.Threading;
using BeltRunner.Core.Execution.Outcome;
using BeltRunner.Core.Phase;
using BeltRunner.Core.Plan;
using BeltRunner.Core.Units;

namespace BeltRunner.Core.Execution;

internal sealed class RunSnapshotStore : IDisposable {
    private static readonly IPhaseSnapshot[] emptyPhaseSnapshots = Array.Empty<IPhaseSnapshot>();
    private static readonly IReadOnlyList<IPhaseSnapshot> emptyPhaseSnapshotView = Array.AsReadOnly(emptyPhaseSnapshots);
    private static readonly IUnitSnapshot[] emptyUnitSnapshots = Array.Empty<IUnitSnapshot>();
    private static readonly IReadOnlyList<IUnitSnapshot> emptyUnitSnapshotView = Array.AsReadOnly(emptyUnitSnapshots);

    private readonly object gate = new();
    private readonly BehaviorSubject<IRunSnapshot> snapshotsSubject;
    private readonly ISubject<IRunSnapshot> snapshotsSink;
    private readonly TimeSpan? snapshotPublishCoalescingInterval;

    private readonly List<MutablePhaseState> phases = new();
    private readonly Dictionary<PhaseKey, MutablePhaseState> phasesByKey = new();

    private bool disposed;
    private RunStatus status = RunStatus.Created;
    private PhaseKey? currentPhaseKey;
    private string? currentPhaseName;
    private RunSnapshot currentSnapshot;
    private IPhaseSnapshot[] currentPhaseSnapshots = emptyPhaseSnapshots;
    private IReadOnlyList<IPhaseSnapshot> currentPhaseSnapshotView = emptyPhaseSnapshotView;
    private Timer? snapshotPublishTimer;
    private bool snapshotPublishScheduled;
    private bool hasPendingCoalescedSnapshot;
    private bool snapshotInvalidated;
    private bool allPhaseSnapshotsDirty;
    private readonly HashSet<int> dirtyPhaseIndices = new();

    public RunSnapshotStore(TimeSpan? snapshotPublishCoalescingInterval = null) {
        if( snapshotPublishCoalescingInterval.HasValue && snapshotPublishCoalescingInterval.Value <= TimeSpan.Zero )
            throw new ArgumentOutOfRangeException(nameof(snapshotPublishCoalescingInterval), "Snapshot publish coalescing interval must be greater than zero.");

        this.snapshotPublishCoalescingInterval = snapshotPublishCoalescingInterval;
        this.currentSnapshot = CreateRunSnapshot_NoLock();
        this.snapshotsSubject = new BehaviorSubject<IRunSnapshot>(this.currentSnapshot);
        this.snapshotsSink = Subject.Synchronize(this.snapshotsSubject);
    }

    public IRunSnapshot Snapshot {
        get {
            lock( this.gate ) {
                return this.currentSnapshot;
            }
        }
    }

    public IObservable<IRunSnapshot> Snapshots => this.snapshotsSubject;

    public RunStatus Status {
        get {
            lock( this.gate ) {
                return this.status;
            }
        }
    }

    public void Initialize(IReadOnlyList<SequentialPlanStep> steps) {
        if( steps is null ) throw new ArgumentNullException(nameof(steps));

        PublishUpdate(() => {
            DetachAllPhaseUnitSets_NoLock();
            this.phases.Clear();
            this.phasesByKey.Clear();
            this.currentPhaseKey = null;
            this.currentPhaseName = null;

            for( int i = 0; i < steps.Count; i++ ) {
                SequentialPlanStep step = steps[i] ?? throw new InvalidOperationException("The sequential plan contains null steps.");
                PhaseKey phaseKey = step.Factory.Key ?? throw new InvalidOperationException("PhaseKey is null.");
                string name = string.IsNullOrWhiteSpace(step.Name) ? phaseKey.Value : step.Name;

                MutablePhaseState state = new MutablePhaseState(phaseKey, name, i);
                this.phases.Add(state);
                this.phasesByKey.Add(phaseKey, state);
            }

            MarkAllPhasesDirty_NoLock();
        });
    }

    public void AttachPhase(PhaseKey phaseKey, IPhase phase) {
        if( phaseKey is null ) throw new ArgumentNullException(nameof(phaseKey));
        if( phase is null ) throw new ArgumentNullException(nameof(phase));
        if( phase.Units is null ) throw new InvalidOperationException($"Phase returned null units. phaseKey=\"{phaseKey}\"");

        PublishUpdate(() => {
            MutablePhaseState state = GetPhase_NoLock(phaseKey);
            state.DetachUnitSet();
            state.Name = string.IsNullOrWhiteSpace(phase.Name) ? state.Name : phase.Name;
            state.AttachUnitSet(phase.Units, (_, _) => OnPhaseUnitsChanged(phaseKey));
            SyncPhaseUnits_NoLock(state);
            MarkPhaseDirty_NoLock(state);
        });
    }

    public void OnRunStarted() {
        PublishUpdate(() => {
            this.status = RunStatus.Running;
            MarkRunSnapshotDirty_NoLock();
        });
    }

    public void OnRunCancelling() {
        PublishUpdate(() => {
            if( this.status is RunStatus.Completed or RunStatus.Cancelled or RunStatus.Faulted ) {
                return;
            }

            this.status = RunStatus.Cancelling;
            MarkRunSnapshotDirty_NoLock();
        });
    }

    public void OnRunCompleted(RunOutcome outcome) {
        if( outcome is null ) throw new ArgumentNullException(nameof(outcome));

        PublishUpdate(() => {
            if( outcome.Kind == RunOutcomeKind.Cancelled ) {
                this.status = RunStatus.Cancelled;
                if( this.currentPhaseKey is not null && this.phasesByKey.TryGetValue(this.currentPhaseKey, out MutablePhaseState? currentPhase) ) {
                    currentPhase.MarkCancelled();
                    MarkPhaseDirty_NoLock(currentPhase);
                }
            } else {
                this.status = RunStatus.Completed;
            }

            this.currentPhaseKey = null;
            this.currentPhaseName = null;
            MarkRunSnapshotDirty_NoLock();
        });
    }

    public void OnRunFaulted(Exception exception, RunOutcome outcome) {
        if( exception is null ) throw new ArgumentNullException(nameof(exception));
        if( outcome is null ) throw new ArgumentNullException(nameof(outcome));

        PublishUpdate(() => {
            this.status = RunStatus.Faulted;
            this.currentPhaseKey = null;
            this.currentPhaseName = null;
            MarkRunSnapshotDirty_NoLock();
        });
    }

    public void OnPhaseStarted(PhaseKey phaseKey) {
        if( phaseKey is null ) throw new ArgumentNullException(nameof(phaseKey));

        PublishUpdate(() => {
            MutablePhaseState phase = GetPhase_NoLock(phaseKey);
            SyncPhaseUnits_NoLock(phase);
            phase.MarkStarted();
            ApplyPhaseAssociation_NoLock(phase);
            this.currentPhaseKey = phaseKey;
            this.currentPhaseName = phase.Name;
            MarkPhaseDirty_NoLock(phase);
            MarkRunSnapshotDirty_NoLock();
        });
    }

    public void OnPhaseCompleted(PhaseKey phaseKey) {
        if( phaseKey is null ) throw new ArgumentNullException(nameof(phaseKey));

        PublishUpdate(() => {
            MutablePhaseState phase = GetPhase_NoLock(phaseKey);
            phase.MarkCompleted();
            if( Equals(this.currentPhaseKey, phaseKey) ) {
                this.currentPhaseKey = null;
                this.currentPhaseName = null;
            }

            MarkPhaseDirty_NoLock(phase);
            MarkRunSnapshotDirty_NoLock();
        });
    }

    public void OnPhaseFaulted(PhaseKey phaseKey, Exception exception) {
        if( phaseKey is null ) throw new ArgumentNullException(nameof(phaseKey));
        if( exception is null ) throw new ArgumentNullException(nameof(exception));

        PublishUpdate(() => {
            MutablePhaseState phase = GetPhase_NoLock(phaseKey);
            phase.MarkFaulted();

            if( Equals(this.currentPhaseKey, phaseKey) ) {
                this.currentPhaseKey = null;
                this.currentPhaseName = null;
            }

            MarkPhaseDirty_NoLock(phase);
            MarkRunSnapshotDirty_NoLock();
        });
    }

    public void SetTotalUnits(PhaseKey phaseKey, int? totalUnits) {
        if( phaseKey is null ) throw new ArgumentNullException(nameof(phaseKey));

        PublishUpdate(() => {
            if( totalUnits.HasValue && totalUnits.Value < 0 ) {
                throw new ArgumentOutOfRangeException(nameof(totalUnits), "TotalUnits must be non-negative.");
            }

            MutablePhaseState phase = GetPhase_NoLock(phaseKey);
            phase.TotalUnits = totalUnits;
            MarkPhaseDirty_NoLock(phase);
        });
    }

    public void SetProcessedUnits(PhaseKey phaseKey, int processedUnits) {
        if( phaseKey is null ) throw new ArgumentNullException(nameof(phaseKey));

        PublishCoalescedUpdate(() => {
            if( processedUnits < 0 ) {
                throw new ArgumentOutOfRangeException(nameof(processedUnits), "ProcessedUnits must be non-negative.");
            }

            MutablePhaseState phase = GetPhase_NoLock(phaseKey);
            int? effectiveTotalUnits = GetEffectiveTotalUnits_NoLock(phase, phase.Units.Length);
            int normalizedProcessedUnits = effectiveTotalUnits.HasValue
                ? Math.Min(processedUnits, effectiveTotalUnits.Value)
                : processedUnits;

            if( normalizedProcessedUnits > phase.ReportedProcessedUnits ) {
                phase.ReportedProcessedUnits = normalizedProcessedUnits;
                MarkPhaseDirty_NoLock(phase);
            }
        });
    }

    public void SetUnitProgress(PhaseKey phaseKey, Guid unitId, double ratio) {
        if( phaseKey is null ) throw new ArgumentNullException(nameof(phaseKey));

        PublishCoalescedUpdate(() => {
            MutablePhaseState phase = GetPhase_NoLock(phaseKey);
            IUnit unit = GetUnit_NoLock(phase, unitId);
            MutableUnitRuntimeState state = GetOrCreateUnitState_NoLock(phase, unit);
            state.Ratio = Clamp01(ratio);
            UpdateCachedUnitSnapshot_NoLock(phase, unit, state);
            MarkPhaseDirty_NoLock(phase);
        });
    }

    public void SetUnitStatus(PhaseKey phaseKey, Guid unitId, UnitStatus status) {
        if( phaseKey is null ) throw new ArgumentNullException(nameof(phaseKey));

        PublishCoalescedUpdate(() => {
            MutablePhaseState phase = GetPhase_NoLock(phaseKey);
            IUnit unit = GetUnit_NoLock(phase, unitId);
            MutableUnitRuntimeState state = GetOrCreateUnitState_NoLock(phase, unit);
            state.Status = status;
            UpdateCachedUnitSnapshot_NoLock(phase, unit, state);
            ApplyRuntimeState_NoLock(unit, phase.PhaseKey, status);
            MarkPhaseDirty_NoLock(phase);
        });
    }

    private void OnPhaseUnitsChanged(PhaseKey phaseKey) {
        if( this.disposed ) {
            return;
        }

        PublishCoalescedUpdate(() => {
            MutablePhaseState phase = GetPhase_NoLock(phaseKey);
            SyncPhaseUnits_NoLock(phase);

            if( phase.Status != PhaseStatus.Pending ) {
                ApplyPhaseAssociation_NoLock(phase);
            }

            MarkPhaseDirty_NoLock(phase);
        });
    }

    private void PublishUpdate(Action updater) {
        PublishUpdate(updater, false);
    }

    private void PublishCoalescedUpdate(Action updater) {
        PublishUpdate(updater, true);
    }

    private void PublishUpdate(Action updater, bool preferCoalesced) {
        if( updater is null ) throw new ArgumentNullException(nameof(updater));

        RunSnapshot? snapshot = null;
        bool shouldPublish = false;

        lock( this.gate ) {
            if( this.disposed ) {
                return;
            }

            updater();

            if( preferCoalesced && this.snapshotPublishCoalescingInterval.HasValue ) {
                this.hasPendingCoalescedSnapshot = true;
                ScheduleSnapshotPublish_NoLock();
                return;
            }

            this.hasPendingCoalescedSnapshot = false;
            CancelScheduledSnapshotPublish_NoLock();
            if( !this.snapshotInvalidated ) {
                return;
            }

            snapshot = RebuildSnapshot_NoLock();
            this.currentSnapshot = snapshot;
            shouldPublish = true;
        }

        if( shouldPublish ) {
            this.snapshotsSink.OnNext(snapshot!);
        }
    }

    private void ScheduleSnapshotPublish_NoLock() {
        if( this.snapshotPublishScheduled || !this.snapshotPublishCoalescingInterval.HasValue ) {
            return;
        }

        this.snapshotPublishTimer ??= new Timer(OnSnapshotPublishTimerTick, null, Timeout.Infinite, Timeout.Infinite);
        this.snapshotPublishTimer.Change(this.snapshotPublishCoalescingInterval.Value, Timeout.InfiniteTimeSpan);
        this.snapshotPublishScheduled = true;
    }

    private void CancelScheduledSnapshotPublish_NoLock() {
        if( !this.snapshotPublishScheduled ) {
            return;
        }

        this.snapshotPublishTimer?.Change(Timeout.Infinite, Timeout.Infinite);
        this.snapshotPublishScheduled = false;
    }

    private void OnSnapshotPublishTimerTick(object? state) {
        RunSnapshot? snapshot = null;

        lock( this.gate ) {
            if( this.disposed ) {
                return;
            }

            this.snapshotPublishScheduled = false;
            if( !this.hasPendingCoalescedSnapshot ) {
                return;
            }

            this.hasPendingCoalescedSnapshot = false;
            if( !this.snapshotInvalidated ) {
                return;
            }

            snapshot = RebuildSnapshot_NoLock();
            this.currentSnapshot = snapshot;
        }

        this.snapshotsSink.OnNext(snapshot);
    }

    private RunSnapshot RebuildSnapshot_NoLock() {
        bool rebuildAllPhaseSnapshots = this.allPhaseSnapshotsDirty || this.currentPhaseSnapshots.Length != this.phases.Count;

        if( rebuildAllPhaseSnapshots ) {
            IPhaseSnapshot[] phaseSnapshots = new IPhaseSnapshot[this.phases.Count];
            for( int i = 0; i < this.phases.Count; i++ ) {
                phaseSnapshots[i] = CreatePhaseSnapshot_NoLock(this.phases[i]);
            }

            this.currentPhaseSnapshots = phaseSnapshots;
            this.currentPhaseSnapshotView = Array.AsReadOnly(phaseSnapshots);
        } else if( this.dirtyPhaseIndices.Count > 0 ) {
            IPhaseSnapshot[] phaseSnapshots = new IPhaseSnapshot[this.currentPhaseSnapshots.Length];
            Array.Copy(this.currentPhaseSnapshots, phaseSnapshots, this.currentPhaseSnapshots.Length);

            foreach( int phaseIndex in this.dirtyPhaseIndices ) {
                phaseSnapshots[phaseIndex] = CreatePhaseSnapshot_NoLock(this.phases[phaseIndex]);
            }

            this.currentPhaseSnapshots = phaseSnapshots;
            this.currentPhaseSnapshotView = Array.AsReadOnly(phaseSnapshots);
        }

        this.snapshotInvalidated = false;
        this.allPhaseSnapshotsDirty = false;
        this.dirtyPhaseIndices.Clear();
        return CreateRunSnapshot_NoLock();
    }

    private RunSnapshot CreateRunSnapshot_NoLock() {
        return new RunSnapshot(
            this.status,
            this.currentPhaseKey,
            this.currentPhaseName,
            CalculateOverallRatio_NoLock(this.currentPhaseSnapshots),
            this.currentPhaseSnapshotView);
    }

    private PhaseSnapshot CreatePhaseSnapshot_NoLock(MutablePhaseState phase) {
        EnsureUnitSnapshotGraph_NoLock(phase);
        int observedUnitCount = phase.CurrentUnitSnapshots.Length;
        int? effectiveTotalUnits = GetEffectiveTotalUnits_NoLock(phase, observedUnitCount);
        int processedUnits = Math.Max(phase.ProcessedUnits, phase.ReportedProcessedUnits);
        double phaseRatio = CalculatePhaseRatio_NoLock(phase.Status, effectiveTotalUnits, observedUnitCount, phase.RatioSum, processedUnits);
        return new PhaseSnapshot(
            phase.PhaseKey,
            phase.Name,
            phase.Index,
            phase.Status,
            effectiveTotalUnits,
            processedUnits,
            phaseRatio,
            phase.CurrentUnitSnapshotView);
    }

    private double CalculateOverallRatio_NoLock(IReadOnlyList<IPhaseSnapshot> phaseSnapshots) {
        int count = phaseSnapshots.Count;
        if( count == 0 ) {
            return 0.0;
        }

        if( this.status == RunStatus.Completed ) {
            return 1.0;
        }

        if( this.currentPhaseKey is not null ) {
            for( int i = 0; i < count; i++ ) {
                IPhaseSnapshot currentPhase = phaseSnapshots[i];
                if( Equals(currentPhase.PhaseKey, this.currentPhaseKey) ) {
                    return Clamp01(((double)currentPhase.PhaseIndex + currentPhase.Ratio) / count);
                }
            }
        }

        IPhaseSnapshot? lastTouched = null;
        for( int i = 0; i < count; i++ ) {
            IPhaseSnapshot phase = phaseSnapshots[i];
            if( phase.Status != PhaseStatus.Pending ) {
                lastTouched = phase;
            }
        }

        if( lastTouched is null ) {
            return 0.0;
        }

        if( lastTouched.Status == PhaseStatus.Completed ) {
            return Clamp01((double)(lastTouched.PhaseIndex + 1) / count);
        }

        return Clamp01(((double)lastTouched.PhaseIndex + lastTouched.Ratio) / count);
    }

    private static double CalculatePhaseRatio_NoLock(PhaseStatus phaseStatus, int? effectiveTotalUnits, int unitCount, double ratioSum, int processedUnits) {
        if( phaseStatus == PhaseStatus.Completed ) {
            return 1.0;
        }

        double aggregateProgress = Math.Max(ratioSum, processedUnits);

        if( effectiveTotalUnits.HasValue && effectiveTotalUnits.Value > 0 ) {
            return Clamp01(aggregateProgress / effectiveTotalUnits.Value);
        }

        if( unitCount == 0 ) {
            return 0.0;
        }

        return Clamp01(aggregateProgress / unitCount);
    }

    private MutablePhaseState GetPhase_NoLock(PhaseKey phaseKey) {
        if( !this.phasesByKey.TryGetValue(phaseKey, out MutablePhaseState? phase) ) {
            throw new InvalidOperationException($"Unknown phase key. phaseKey=\"{phaseKey}\"");
        }

        return phase;
    }

    private IUnit GetUnit_NoLock(MutablePhaseState phase, Guid unitId) {
        if( !phase.TryGetUnit(unitId, out IUnit? unit) ) {
            SyncPhaseUnits_NoLock(phase);
        }

        if( unit is null && !phase.TryGetUnit(unitId, out unit) ) {
            throw new InvalidOperationException($"Unknown unit id. phaseKey=\"{phase.PhaseKey}\" unitId=\"{unitId}\"");
        }

        return unit!;
    }

    private void SyncPhaseUnits_NoLock(MutablePhaseState phase) {
        if( phase.UnitSet is null ) {
            phase.SetUnits(Array.Empty<IUnit>());
            return;
        }

        IReadOnlyCollection<IUnit> items = phase.UnitSet.Items;
        IUnit[] units;
        if( items.Count == 0 ) {
            units = Array.Empty<IUnit>();
        } else if( items is IUnit[] array ) {
            units = array;
        } else {
            units = new IUnit[items.Count];
            int next = 0;
            foreach( IUnit unit in items ) {
                units[next++] = unit;
            }
        }

        phase.SetUnits(units);

        for( int i = 0; i < units.Length; i++ ) {
            IUnit unit = units[i];
            GetOrCreateUnitState_NoLock(phase, unit);
        }
    }

    private void EnsureUnitSnapshotGraph_NoLock(MutablePhaseState phase) {
        if( !phase.UnitSnapshotGraphDirty ) {
            return;
        }

        IUnit[] units = phase.Units;
        if( units.Length == 0 ) {
            phase.SetUnitSnapshotGraph(emptyUnitSnapshots, emptyUnitSnapshotView, 0, 0.0);
            return;
        }

        IUnitSnapshot[] unitSnapshots = new IUnitSnapshot[units.Length];
        int processedUnits = 0;
        double ratioSum = 0.0;

        for( int i = 0; i < units.Length; i++ ) {
            IUnitSnapshot unitSnapshot = CreateUnitSnapshot_NoLock(phase, units[i]);
            unitSnapshots[i] = unitSnapshot;
            ratioSum += unitSnapshot.Ratio;

            if( IsTerminal(unitSnapshot.Status) ) {
                processedUnits++;
            }
        }

        phase.SetUnitSnapshotGraph(unitSnapshots, Array.AsReadOnly(unitSnapshots), processedUnits, ratioSum);
    }

    private void UpdateCachedUnitSnapshot_NoLock(MutablePhaseState phase, IUnit unit, MutableUnitRuntimeState state) {
        if( phase is null ) throw new ArgumentNullException(nameof(phase));
        if( unit is null ) throw new ArgumentNullException(nameof(unit));
        if( state is null ) throw new ArgumentNullException(nameof(state));

        EnsureUnitSnapshotGraph_NoLock(phase);
        int unitIndex = phase.GetRequiredUnitIndex(unit.Id);
        IUnitSnapshot previousSnapshot = phase.CurrentUnitSnapshots[unitIndex];
        IUnitSnapshot nextSnapshot = new UnitSnapshot(unit.Id, unit.Name, state.Status, state.Ratio);
        int processedUnitsDelta = GetTerminalContribution(nextSnapshot.Status) - GetTerminalContribution(previousSnapshot.Status);
        double ratioDelta = nextSnapshot.Ratio - previousSnapshot.Ratio;
        phase.ReplaceUnitSnapshot(unitIndex, nextSnapshot, processedUnitsDelta, ratioDelta);
    }

    private MutableUnitRuntimeState GetOrCreateUnitState_NoLock(MutablePhaseState phase, IUnit unit) {
        if( !phase.UnitStates.TryGetValue(unit.Id, out MutableUnitRuntimeState? state) ) {
            state = new MutableUnitRuntimeState(UnitStatus.Pending);
            phase.UnitStates.Add(unit.Id, state);
        }

        return state;
    }

    private IUnitSnapshot CreateUnitSnapshot_NoLock(MutablePhaseState phase, IUnit unit) {
        MutableUnitRuntimeState? state = null;
        phase.UnitStates.TryGetValue(unit.Id, out state);

        UnitStatus status = state?.Status ?? UnitStatus.Pending;
        double ratio = state?.Ratio ?? 0.0;
        return new UnitSnapshot(unit.Id, unit.Name, status, ratio);
    }

    private static void ApplyRuntimeState_NoLock(IUnit unit, PhaseKey phaseKey, UnitStatus status) {
        if( unit is IRuntimeUnit runtimeUnit ) {
            runtimeUnit.SetPhase(phaseKey);
            runtimeUnit.SetStatus(status);
        }
    }

    private static void ApplyPhaseAssociation_NoLock(MutablePhaseState phase) {
        IUnit[] units = phase.Units;
        if( units.Length == 0 ) {
            return;
        }

        for( int i = 0; i < units.Length; i++ ) {
            if( units[i] is IRuntimeUnit runtimeUnit ) {
                runtimeUnit.SetPhase(phase.PhaseKey);
            }
        }
    }

    private static int? GetEffectiveTotalUnits_NoLock(MutablePhaseState phase, int observedUnitCount) {
        if( observedUnitCount < 0 ) {
            throw new ArgumentOutOfRangeException(nameof(observedUnitCount));
        }

        if( phase.TotalUnits.HasValue ) {
            return Math.Max(phase.TotalUnits.Value, observedUnitCount);
        }

        if( observedUnitCount > 0 ) {
            return observedUnitCount;
        }

        return null;
    }

    private void DetachAllPhaseUnitSets_NoLock() {
        for( int i = 0; i < this.phases.Count; i++ ) {
            this.phases[i].DetachUnitSet();
        }
    }

    private void MarkRunSnapshotDirty_NoLock() {
        this.snapshotInvalidated = true;
    }

    private void MarkPhaseDirty_NoLock(MutablePhaseState phase) {
        this.snapshotInvalidated = true;
        if( !this.allPhaseSnapshotsDirty ) {
            this.dirtyPhaseIndices.Add(phase.Index);
        }
    }

    private void MarkAllPhasesDirty_NoLock() {
        this.snapshotInvalidated = true;
        this.allPhaseSnapshotsDirty = true;
        this.dirtyPhaseIndices.Clear();
    }

    private static bool IsTerminal(UnitStatus status) {
        return status is UnitStatus.Succeeded or UnitStatus.Warning or UnitStatus.Failed or UnitStatus.Skipped or UnitStatus.Cancelled;
    }

    private static int GetTerminalContribution(UnitStatus status) {
        return IsTerminal(status) ? 1 : 0;
    }

    private static double Clamp01(double value) {
        if( value < 0.0 ) return 0.0;
        if( value > 1.0 ) return 1.0;
        return value;
    }

    public void Dispose() {
        Timer? timerToDispose = null;

        lock( this.gate ) {
            if( this.disposed ) {
                return;
            }

            this.disposed = true;
            this.snapshotPublishScheduled = false;
            this.hasPendingCoalescedSnapshot = false;
            DetachAllPhaseUnitSets_NoLock();
            timerToDispose = this.snapshotPublishTimer;
            this.snapshotPublishTimer = null;
        }

        timerToDispose?.Dispose();
        this.snapshotsSink.OnCompleted();
        this.snapshotsSubject.Dispose();
    }

    private sealed class MutablePhaseState {
        public MutablePhaseState(PhaseKey phaseKey, string name, int index) {
            this.PhaseKey = phaseKey;
            this.Name = name;
            this.Index = index;
        }

        public PhaseKey PhaseKey { get; }
        public string Name { get; set; }
        public int Index { get; }
        public PhaseStatus Status { get; private set; } = PhaseStatus.Pending;
        public int? TotalUnits { get; set; }
        public UnitSet? UnitSet { get; private set; }
        public EventHandler? UnitSetChangedHandler { get; private set; }
        public IUnit[] Units { get; private set; } = Array.Empty<IUnit>();
        public Dictionary<Guid, int> UnitIndices { get; } = new();
        public Dictionary<Guid, MutableUnitRuntimeState> UnitStates { get; } = new();
        public IUnitSnapshot[] CurrentUnitSnapshots { get; private set; } = emptyUnitSnapshots;
        public IReadOnlyList<IUnitSnapshot> CurrentUnitSnapshotView { get; private set; } = emptyUnitSnapshotView;
        public int ProcessedUnits { get; private set; }
        public int ReportedProcessedUnits { get; set; }
        public double RatioSum { get; private set; }
        public bool UnitSnapshotGraphDirty { get; private set; } = true;

        public void AttachUnitSet(UnitSet unitSet, EventHandler changedHandler) {
            if( unitSet is null ) throw new ArgumentNullException(nameof(unitSet));
            if( changedHandler is null ) throw new ArgumentNullException(nameof(changedHandler));

            this.UnitSet = unitSet;
            this.UnitSetChangedHandler = changedHandler;
            unitSet.Changed += changedHandler;
        }

        public void DetachUnitSet() {
            if( this.UnitSet is not null && this.UnitSetChangedHandler is not null ) {
                this.UnitSet.Changed -= this.UnitSetChangedHandler;
            }

            this.UnitSet = null;
            this.UnitSetChangedHandler = null;
            SetUnits(Array.Empty<IUnit>());
        }

        public void SetUnits(IUnit[] units) {
            this.Units = units ?? throw new ArgumentNullException(nameof(units));
            this.UnitIndices.Clear();

            for( int i = 0; i < this.Units.Length; i++ ) {
                this.UnitIndices[this.Units[i].Id] = i;
            }

            this.UnitSnapshotGraphDirty = true;
        }

        public void SetUnitSnapshotGraph(IUnitSnapshot[] unitSnapshots, IReadOnlyList<IUnitSnapshot> unitSnapshotView, int processedUnits, double ratioSum) {
            this.CurrentUnitSnapshots = unitSnapshots ?? throw new ArgumentNullException(nameof(unitSnapshots));
            this.CurrentUnitSnapshotView = unitSnapshotView ?? throw new ArgumentNullException(nameof(unitSnapshotView));
            this.ProcessedUnits = processedUnits;
            this.RatioSum = ratioSum;
            this.UnitSnapshotGraphDirty = false;
        }

        public int GetRequiredUnitIndex(Guid unitId) {
            if( !this.UnitIndices.TryGetValue(unitId, out int index) || index < 0 || index >= this.Units.Length ) {
                throw new InvalidOperationException($"Unknown unit id. unitId=\"{unitId}\"");
            }

            return index;
        }

        public void ReplaceUnitSnapshot(int unitIndex, IUnitSnapshot unitSnapshot, int processedUnitsDelta, double ratioDelta) {
            if( unitIndex < 0 || unitIndex >= this.CurrentUnitSnapshots.Length ) {
                throw new ArgumentOutOfRangeException(nameof(unitIndex));
            }

            IUnitSnapshot[] nextSnapshots = new IUnitSnapshot[this.CurrentUnitSnapshots.Length];
            Array.Copy(this.CurrentUnitSnapshots, nextSnapshots, this.CurrentUnitSnapshots.Length);
            nextSnapshots[unitIndex] = unitSnapshot ?? throw new ArgumentNullException(nameof(unitSnapshot));
            this.CurrentUnitSnapshots = nextSnapshots;
            this.CurrentUnitSnapshotView = Array.AsReadOnly(nextSnapshots);
            this.ProcessedUnits += processedUnitsDelta;
            this.RatioSum += ratioDelta;
            this.UnitSnapshotGraphDirty = false;
        }

        public bool TryGetUnit(Guid unitId, out IUnit? unit) {
            if( this.UnitIndices.TryGetValue(unitId, out int index) && index >= 0 && index < this.Units.Length ) {
                unit = this.Units[index];
                return true;
            }

            unit = null;
            return false;
        }

        public void MarkStarted() {
            this.Status = PhaseStatus.Running;
        }

        public void MarkCompleted() {
            this.Status = PhaseStatus.Completed;
        }

        public void MarkCancelled() {
            this.Status = PhaseStatus.Cancelled;
        }

        public void MarkFaulted() {
            this.Status = PhaseStatus.Faulted;
        }
    }

    private sealed class MutableUnitRuntimeState {
        public MutableUnitRuntimeState(UnitStatus status) {
            this.Status = status;
        }

        public UnitStatus Status { get; set; }
        public double Ratio { get; set; }
    }
}
