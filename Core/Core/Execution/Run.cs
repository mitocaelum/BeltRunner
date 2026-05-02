using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reactive;
using System.Reactive.Disposables;
using System.Reactive.Linq;
using System.Reactive.Subjects;
using System.Threading;
using System.Threading.Tasks;
using BeltRunner.Core.Execution.Event;
using BeltRunner.Core.Execution.Interaction;
using BeltRunner.Core.Execution.Outcome;
using BeltRunner.Core.Host;
using BeltRunner.Core.Logging;
using BeltRunner.Core.Phase;
using BeltRunner.Core.Plan;
using BeltRunner.Core.Plan.Artifacts;
using BeltRunner.Core.Units;
using NLog;

namespace BeltRunner.Core.Execution;

/// <summary>
/// Default in-memory implementation of <see cref="IRun"/>.
/// </summary>
public sealed class Run : IRun {
    private const string INITIAL_ARTIFACTS_NULL_ITEM_MESSAGE = "Initial artifacts list contains null.";
    private const string INITIAL_ARTIFACT_KEY_NULL_MESSAGE = "Initial artifact Key is null.";
    private const string TOKEN_CANCELLATION_REASON = "Cancellation was requested via CancellationToken.";
    private const string DISPOSE_CANCELLATION_REASON = "Run was disposed before settlement.";
    private static readonly Logger logger = BeltRunnerLogger.GetLogger<Run>();

    /// <summary>
    /// Gets the unique identifier assigned to the run instance.
    /// </summary>
    public Guid Id { get; } = Guid.NewGuid();

    private readonly object gate = new();
    private readonly ArtifactStore artifacts = new();
    private readonly RunSnapshotStore snapshotStore;
    private readonly IDisposable activeRequestsSubscription;
    private readonly TaskCompletionSource<RunOutcome> completionTcs = new(TaskCreationOptions.RunContinuationsAsynchronously);
    private readonly Subject<StoredRunEvent> eventsSubject = new();
    private readonly ISubject<StoredRunEvent> events;
    private readonly CircularBuffer<StoredRunEvent> eventLog;
    private readonly IReadOnlyList<RunEvent> eventLogView;
    private readonly IObservable<RunEvent> eventsObservable;
    private readonly ReplaySubject<IDiagnosticEntry> diagnosticsSubject;
    private readonly ISubject<IDiagnosticEntry> diagnostics;
    private readonly CircularBuffer<DiagnosticEntry> diagnosticLog;
    private readonly IReadOnlyList<IDiagnosticEntry> diagnosticLogView;
    private readonly IObservable<IDiagnosticEntry> diagnosticsObservable;
    private readonly List<InteractionSnapshot> activeInteractions = new();
    private readonly IReadOnlyList<IInteractionSnapshot> activeInteractionView;
    private readonly Func<IRun, ValueTask>? beforeExecutionStartAsync;
    private readonly Func<IRun, ValueTask>? onCompletedAsync;
    private readonly Func<IRun, ValueTask>? beforeRunDisposeAsync;
    private readonly TaskCompletionSource<object?> onCompletedCallbackSettledTcs = new(TaskCreationOptions.RunContinuationsAsynchronously);
    private readonly DiagnosticMode diagnosticMode;
    private readonly IPublicFaultInfoPolicy publicFaultInfoPolicy;

    private CancellationTokenRegistration cancellationRegistration;
    private int disposeState;
    private int onCompletedCallbackState;
    private long lastSequence;
    private bool isCompleted;
    private bool isCancellationRequested;
    private bool executionActivated;
    private bool cancellingEventPublished;
    private string? cancelReason;
    private Exception? faultException;
    private RunOutcome? outcome;
    private Exception? lastPhaseFaultException;

    internal Run(IInteractionBroker interaction, RunConfiguration? configuration = null) {
        this.Interaction = interaction ?? throw new ArgumentNullException(nameof(interaction));
        RunConfiguration effectiveConfiguration = configuration ?? new RunConfiguration();
        CancellationToken cancellationToken = effectiveConfiguration.CancellationToken;
        Host.RunLifecycleCallbacks? lifecycleCallbacks = effectiveConfiguration.LifecycleCallbacks;
        int? eventLogMaxRetainedCount = effectiveConfiguration.EventLogMaxRetainedCount;
        int? runDiagnosticsMaxRetainedCount = effectiveConfiguration.RunDiagnosticsMaxRetainedCount;
        DiagnosticMode diagnosticMode = effectiveConfiguration.DiagnosticMode;
        TimeSpan? snapshotPublishCoalescingInterval = effectiveConfiguration.SnapshotPublishCoalescingInterval;
        if( eventLogMaxRetainedCount.HasValue && eventLogMaxRetainedCount.Value <= 0 )
            throw new ArgumentOutOfRangeException(nameof(eventLogMaxRetainedCount), "Run event log max retained count must be greater than zero.");
        if( runDiagnosticsMaxRetainedCount.HasValue && runDiagnosticsMaxRetainedCount.Value <= 0 )
            throw new ArgumentOutOfRangeException(nameof(runDiagnosticsMaxRetainedCount), "Run diagnostics max retained count must be greater than zero.");
        if( snapshotPublishCoalescingInterval.HasValue && snapshotPublishCoalescingInterval.Value <= TimeSpan.Zero )
            throw new ArgumentOutOfRangeException(nameof(snapshotPublishCoalescingInterval), "Snapshot publish coalescing interval must be greater than zero.");
        if( !Enum.IsDefined(typeof(DiagnosticMode), diagnosticMode) )
            throw new ArgumentOutOfRangeException(nameof(diagnosticMode), "Diagnostic mode is invalid.");

        this.CancellationToken = cancellationToken;
        this.beforeExecutionStartAsync = lifecycleCallbacks?.BeforeExecutionStartAsync;
        this.onCompletedAsync = lifecycleCallbacks?.OnCompletedAsync;
        this.beforeRunDisposeAsync = lifecycleCallbacks?.BeforeRunDisposeAsync;
        this.diagnosticMode = diagnosticMode;
        this.publicFaultInfoPolicy = effectiveConfiguration.PublicFaultInfoPolicy ?? throw new ArgumentNullException(nameof(effectiveConfiguration.PublicFaultInfoPolicy));
        this.events = Subject.Synchronize(this.eventsSubject);
        this.eventLog = new CircularBuffer<StoredRunEvent>(eventLogMaxRetainedCount);
        this.eventLogView = new RunEventLogView(this.eventLog, this.gate);
        this.eventsObservable = CreateReplayableEventsObservable();
        this.diagnosticsSubject = runDiagnosticsMaxRetainedCount.HasValue
            ? new ReplaySubject<IDiagnosticEntry>(runDiagnosticsMaxRetainedCount.Value)
            : new ReplaySubject<IDiagnosticEntry>();
        this.diagnostics = Subject.Synchronize(this.diagnosticsSubject);
        this.diagnosticLog = new CircularBuffer<DiagnosticEntry>(runDiagnosticsMaxRetainedCount);
        this.diagnosticLogView = new DiagnosticLogView(this.diagnosticLog, this.gate);
        this.diagnosticsObservable = this.diagnosticsSubject.AsObservable();
        this.activeInteractionView = new ActiveInteractionView(this.activeInteractions, this.gate);
        this.snapshotStore = new RunSnapshotStore(snapshotPublishCoalescingInterval);
        this.activeRequestsSubscription = this.Interaction.ActiveRequestsChanges.Subscribe(UpdateActiveInteractions);
        UpdateActiveInteractions(this.Interaction.ActiveRequests);
        this.cancellationRegistration = cancellationToken.Register(this.OnCancellationRequested);

        if( cancellationToken.IsCancellationRequested ) {
            this.OnCancellationRequested();
        }
    }

    /// <inheritdoc />
    public RunStatus Status => this.snapshotStore.Status;

    /// <inheritdoc />
    public IRunSnapshot Snapshot => this.snapshotStore.Snapshot;

    /// <inheritdoc />
    public IObservable<IRunSnapshot> SnapshotStream => this.snapshotStore.Snapshots;

    /// <inheritdoc />
    public IInteractionBroker Interaction { get; }

    /// <inheritdoc />
    public IReadOnlyList<IInteractionSnapshot> ActiveInteractions => this.activeInteractionView;

    /// <inheritdoc />
    public IArtifactReader Artifacts => this.artifacts;

    /// <inheritdoc />
    public IObservable<RunEvent> EventStream => this.eventsObservable;

    /// <inheritdoc />
    public IObservable<IDiagnosticEntry> DiagnosticStream => this.diagnosticsObservable;

    /// <inheritdoc />
    public IReadOnlyList<RunEvent> EventLog => this.eventLogView;

    /// <inheritdoc />
    public IReadOnlyList<IDiagnosticEntry> DiagnosticLog => this.diagnosticLogView;

    /// <inheritdoc />
    public Task<RunOutcome> Completion => this.completionTcs.Task;

    /// <inheritdoc />
    public CancellationToken CancellationToken { get; }

    /// <inheritdoc />
    public bool IsCancellationRequested => this.isCancellationRequested;

    /// <inheritdoc />
    public string? CancelReason => this.cancelReason;

    internal ValueTask InvokeBeforeExecutionStartAsync() {
        if( this.beforeExecutionStartAsync is null ) {
            return default;
        }

        return this.beforeExecutionStartAsync(this);
    }

    internal ValueTask InvokeBeforeRunDisposeAsync() {
        if( this.beforeRunDisposeAsync is null ) {
            return default;
        }

        return this.beforeRunDisposeAsync(this);
    }

    internal void EnsureOnCompletedCallbackStarted() {
        if( this.completionTcs.Task.IsCompleted ) {
            StartOnCompletedCallbackIfNeeded();
        }
    }

    internal void ActivateExecution() {
        bool shouldPublishCancellingEvent = false;

        lock( this.gate ) {
            if( this.executionActivated ) {
                return;
            }

            this.executionActivated = true;
            shouldPublishCancellingEvent = this.isCancellationRequested && !this.cancellingEventPublished && !this.isCompleted;

            if( shouldPublishCancellingEvent ) {
                this.cancellingEventPublished = true;
            }
        }

        if( shouldPublishCancellingEvent ) {
            this.Publish(new RunCancellingEvent(this.Id));
        }
    }

    internal void InitializeRuntimeState(IReadOnlyList<SequentialPlanStep> steps) {
        if( steps is null ) throw new ArgumentNullException(nameof(steps));
        this.snapshotStore.Initialize(steps);
    }

    internal void Publish(RunEvent ev) {
        if( ev is null ) throw new ArgumentNullException(nameof(ev));

        StoredRunEvent stored;

        lock( this.gate ) {
            if( this.isCompleted ) return;

            long seq = ++this.lastSequence;
            stored = new StoredRunEvent(seq, ev);
            this.eventLog.Add(stored);
        }

        ApplySnapshotEvent(ev);
        LogPublishedEvent(ev);
        this.events.OnNext(stored);
    }

    internal void ValidateConsumes(IReadOnlyList<IArtifactKey> consumes, PhaseKey phaseKeyForError) {
        if( consumes is null ) throw new ArgumentNullException(nameof(consumes));
        if( phaseKeyForError is null ) throw new ArgumentNullException(nameof(phaseKeyForError));

        for( int i = 0; i < consumes.Count; i++ ) {
            IArtifactKey key = consumes[i] ?? throw new ArgumentException("Consumes list contains null.", nameof(consumes));
            if( !this.artifacts.Contains(key) ) {
                throw new InvalidOperationException($"Missing consumed artifact. phaseKey=\"{phaseKeyForError}\" key=\"{key.Name}\" type=\"{key.ValueType.Name}\"");
            }
        }
    }

    internal void MergeProduced(IReadOnlyList<IArtifactKey> produces, IReadOnlyList<IProducedArtifact> produced, PhaseKey phaseKeyForError) {
        if( produces is null ) throw new ArgumentNullException(nameof(produces));
        if( produced is null ) throw new ArgumentNullException(nameof(produced));
        if( phaseKeyForError is null ) throw new ArgumentNullException(nameof(phaseKeyForError));

        HashSet<IArtifactKey> allowed = new(ArtifactKeySignatureComparer.Instance);

        for( int i = 0; i < produces.Count; i++ ) {
            IArtifactKey key = produces[i] ?? throw new ArgumentException("Produces list contains null.", nameof(produces));
            allowed.Add(key);
        }

        for( int i = 0; i < produced.Count; i++ ) {
            IProducedArtifact item = produced[i] ?? throw new ArgumentException("Produced list contains null.", nameof(produced));
            IArtifactKey key = item.Key ?? throw new InvalidOperationException("Produced artifact Key is null.");

            if( !allowed.Contains(key) ) {
                throw new InvalidOperationException($"Produced an undeclared artifact. phaseKey=\"{phaseKeyForError}\" key=\"{key.Name}\" type=\"{key.ValueType.Name}\"");
            }

            this.artifacts.SetBoxed(key, item.Value);
        }
    }

    internal void SetPhaseTotalUnits(PhaseKey phaseKey, int? totalUnits) {
        this.snapshotStore.SetTotalUnits(phaseKey, totalUnits);
    }

    internal void SetPhaseProcessedUnits(PhaseKey phaseKey, int processedUnits) {
        this.snapshotStore.SetProcessedUnits(phaseKey, processedUnits);
    }

    internal void AttachPhase(PhaseKey phaseKey, IPhase phase) {
        if( phaseKey is null ) throw new ArgumentNullException(nameof(phaseKey));
        if( phase is null ) throw new ArgumentNullException(nameof(phase));
        this.snapshotStore.AttachPhase(phaseKey, phase);
    }

    internal void SetUnitProgress(PhaseKey phaseKey, Guid unitId, double ratio) {
        this.snapshotStore.SetUnitProgress(phaseKey, unitId, ratio);
    }

    internal void SetUnitStatus(PhaseKey phaseKey, Guid unitId, UnitStatus status) {
        this.snapshotStore.SetUnitStatus(phaseKey, unitId, status);
    }

    internal void PublishDiagnostic(PhaseKey phaseKey, DiagnosticSeverity severity, string message, Exception? exception = null, Guid? unitId = null) {
        if( phaseKey is null ) throw new ArgumentNullException(nameof(phaseKey));
        if( message is null ) throw new ArgumentNullException(nameof(message));
        if( !ShouldEmitDiagnostic(severity) ) {
            return;
        }

        string safeMessage = TextConstraints.NormalizeRequired(message, TextConstraints.DIAGNOSTIC_MESSAGE_MAX_LENGTH, nameof(message));
        PublicFaultInfo? faultInfo = exception is null ? null : CreatePhaseFaultInfo(phaseKey, exception);
        DiagnosticEntry entry = new(Guid.NewGuid(), DateTimeOffset.UtcNow, severity, safeMessage, faultInfo, phaseKey, unitId);
        AddDiagnostic(entry);
        LogDiagnostic(phaseKey, severity, safeMessage, exception, unitId);
    }

    /// <inheritdoc />
    public void RequestCancellation(string? reason = null) {
        string? safeReason = TextConstraints.NormalizeNullable(reason, TextConstraints.CANCEL_REASON_MAX_LENGTH);
        bool shouldPublishCancellingEvent = false;

        lock( this.gate ) {
            if( this.isCompleted )
                return;

            if( !this.isCancellationRequested ) {
                this.isCancellationRequested = true;
            }

            if( safeReason is not null ) {
                if( this.cancelReason is null || string.Equals(this.cancelReason, TOKEN_CANCELLATION_REASON, StringComparison.Ordinal) ) {
                    this.cancelReason = safeReason;
                }
            }

            if( this.executionActivated && !this.cancellingEventPublished && this.isCancellationRequested ) {
                this.cancellingEventPublished = true;
                shouldPublishCancellingEvent = true;
            }
        }

        if( shouldPublishCancellingEvent ) {
            this.Publish(new RunCancellingEvent(this.Id));
        }
    }

    internal bool TryComplete(RunOutcome finalOutcome) {
        if( finalOutcome is null ) throw new ArgumentNullException(nameof(finalOutcome));

        bool shouldComplete;

        lock( this.gate ) {
            if( this.isCompleted ) return false;

            this.isCompleted = true;
            this.outcome = finalOutcome;
            this.faultException = null;
            shouldComplete = true;
        }

        if( shouldComplete ) {
            this.snapshotStore.OnRunCompleted(finalOutcome);
            this.events.OnCompleted();
            this.diagnostics.OnCompleted();
            this.completionTcs.TrySetResult(finalOutcome);
            StartOnCompletedCallbackIfNeeded();
        }

        return true;
    }

    internal bool TryFault(Exception exception) {
        if( exception is null ) throw new ArgumentNullException(nameof(exception));

        RunOutcome finalOutcome = RunOutcome.Faulted(CreateRunFaultInfo(exception));
        bool shouldFault;

        lock( this.gate ) {
            if( this.isCompleted ) return false;

            this.isCompleted = true;
            this.outcome = finalOutcome;
            this.faultException = exception;
            shouldFault = true;
        }

        if( shouldFault ) {
            this.snapshotStore.OnRunFaulted(exception, finalOutcome);
            this.events.OnCompleted();
            this.diagnostics.OnCompleted();
            this.completionTcs.TrySetResult(finalOutcome);
            StartOnCompletedCallbackIfNeeded();
        }

        return true;
    }

    private bool TryCancelFromDispose() {
        RunOutcome cancelledOutcome = RunOutcome.Cancelled(DISPOSE_CANCELLATION_REASON);
        bool shouldCancel;

        lock( this.gate ) {
            if( this.isCompleted )
                return false;

            this.isCompleted = true;
            this.outcome = cancelledOutcome;
            this.faultException = null;
            this.isCancellationRequested = true;
            this.cancelReason = DISPOSE_CANCELLATION_REASON;
            shouldCancel = true;
        }

        if( shouldCancel ) {
            try { this.snapshotStore.OnRunCompleted(cancelledOutcome); } catch { }
            try { this.events.OnCompleted(); } catch { }
            try { this.diagnostics.OnCompleted(); } catch { }
            try { this.completionTcs.TrySetResult(cancelledOutcome); } catch { }
            try { StartOnCompletedCallbackIfNeeded(); } catch { }
        }

        return true;
    }

    private void EnsureCompletionSettledFromCurrentState() {
        if( this.completionTcs.Task.IsCompleted )
            return;

        RunOutcome? outcomeSnapshot;
        Exception? faultSnapshot;
        bool completedSnapshot;

        lock( this.gate ) {
            completedSnapshot = this.isCompleted;
            outcomeSnapshot = this.outcome;
            faultSnapshot = this.faultException;
        }

        if( !completedSnapshot )
            return;

        if( outcomeSnapshot is not null ) {
            this.completionTcs.TrySetResult(outcomeSnapshot);
            return;
        }

        if( faultSnapshot is not null ) {
            this.completionTcs.TrySetResult(RunOutcome.Faulted(CreateRunFaultInfo(faultSnapshot)));
            return;
        }

        this.completionTcs.TrySetResult(RunOutcome.Cancelled(DISPOSE_CANCELLATION_REASON));
    }

    private void StartOnCompletedCallbackIfNeeded() {
        if( Interlocked.CompareExchange(ref this.onCompletedCallbackState, 1, 0) != 0 ) {
            return;
        }

        if( this.onCompletedAsync is null ) {
            this.onCompletedCallbackSettledTcs.TrySetResult(null);
            return;
        }

        try {
            ValueTask callbackTask = this.onCompletedAsync(this);
            if( callbackTask.IsCompletedSuccessfully ) {
                this.onCompletedCallbackSettledTcs.TrySetResult(null);
                return;
            }

            _ = ObserveOnCompletedCallbackAsync(callbackTask);
        } catch( Exception ex ) {
            LogOnCompletedCallbackFailure(ex);
            this.onCompletedCallbackSettledTcs.TrySetResult(null);
        }
    }

    private async Task ObserveOnCompletedCallbackAsync(ValueTask callbackTask) {
        try {
            await callbackTask.ConfigureAwait(false);
        } catch( Exception ex ) {
            LogOnCompletedCallbackFailure(ex);
        } finally {
            this.onCompletedCallbackSettledTcs.TrySetResult(null);
        }
    }

    private void WaitForOnCompletedCallback() {
        EnsureOnCompletedCallbackStarted();
        this.onCompletedCallbackSettledTcs.Task.GetAwaiter().GetResult();
    }

    private void OnCancellationRequested() {
        this.RequestCancellation(TOKEN_CANCELLATION_REASON);
    }

    private void LogPublishedEvent(RunEvent ev) {
        switch( ev ) {
            case RunStartedEvent runStarted:
                ClearLastPhaseFaultException();
                BeltRunnerLogger.Write(logger, LogLevel.Info, "Run started.", logEvent => {
                    PopulateRunEventProperties(logEvent, runStarted);
                });
                break;
            case RunCancellingEvent runCancelling:
                BeltRunnerLogger.Write(logger, LogLevel.Warn, "Run cancellation requested.", logEvent => {
                    PopulateRunEventProperties(logEvent, runCancelling);
                    BeltRunnerLogger.SetProperty(logEvent, "cancelReason", this.CancelReason);
                });
                break;
            case RunCancelledEvent runCancelled:
                ClearLastPhaseFaultException();
                BeltRunnerLogger.Write(logger, LogLevel.Warn, "Run cancelled.", logEvent => {
                    PopulateRunEventProperties(logEvent, runCancelled);
                    BeltRunnerLogger.SetProperty(logEvent, "cancelReason", runCancelled.Reason);
                });
                break;
            case PhaseStartedEvent phaseStarted:
                BeltRunnerLogger.Write(logger, LogLevel.Info, "Phase started.", logEvent => {
                    PopulateRunEventProperties(logEvent, phaseStarted);
                    PopulatePhaseProperties(logEvent, phaseStarted.PhaseKey, phaseStarted.PhaseIndex);
                });
                break;
            case PhaseCompletedEvent phaseCompleted:
                BeltRunnerLogger.Write(logger, GetPhaseCompletedLevel(phaseCompleted.Result), "Phase completed.", logEvent => {
                    PopulateRunEventProperties(logEvent, phaseCompleted);
                    PopulatePhaseProperties(logEvent, phaseCompleted.PhaseKey, phaseCompleted.PhaseIndex);
                    BeltRunnerLogger.SetProperty(logEvent, "phaseResult", phaseCompleted.Result.ToString());
                });
                break;
            case PhaseFaultedEvent phaseFaulted:
                if( phaseFaulted.SourceException is not null ) {
                    RememberLastPhaseFaultException(phaseFaulted.SourceException);
                }

                BeltRunnerLogger.Write(logger, LogLevel.Error, "Phase faulted.", logEvent => {
                    PopulateRunEventProperties(logEvent, phaseFaulted);
                    PopulatePhaseProperties(logEvent, phaseFaulted.PhaseKey, phaseFaulted.PhaseIndex);
                }, phaseFaulted.SourceException);
                break;
            case RunCompletedEvent runCompleted:
                ClearLastPhaseFaultException();
                BeltRunnerLogger.Write(logger, LogLevel.Info, "Run completed.", logEvent => {
                    PopulateRunEventProperties(logEvent, runCompleted);
                });
                break;
            case RunFaultedEvent runFaulted:
                if( runFaulted.SourceException is not null && ShouldSuppressRunFaultLog(runFaulted.SourceException) ) {
                    return;
                }

                BeltRunnerLogger.Write(logger, LogLevel.Error, "Run faulted.", logEvent => {
                    PopulateRunEventProperties(logEvent, runFaulted);
                }, runFaulted.SourceException);
                break;
        }
    }

    private void LogDiagnostic(PhaseKey phaseKey, DiagnosticSeverity severity, string message, Exception? exception, Guid? unitId) {
        BeltRunnerLogger.Write(logger, MapDiagnosticLevel(severity), message, logEvent => {
            BeltRunnerLogger.SetProperty(logEvent, "runId", this.Id);
            BeltRunnerLogger.SetProperty(logEvent, "phaseKey", phaseKey.ToString());
            BeltRunnerLogger.SetProperty(logEvent, "unitId", unitId);
            BeltRunnerLogger.SetProperty(logEvent, "diagnosticSeverity", severity.ToString());
            BeltRunnerLogger.SetProperty(logEvent, "eventType", "PhaseDiagnostic");
        }, exception);
    }

    private void LogOnCompletedCallbackFailure(Exception exception) {
        BeltRunnerLogger.Write(logger, LogLevel.Warn, "Run OnCompletedAsync callback failed.", logEvent => {
            BeltRunnerLogger.SetProperty(logEvent, "runId", this.Id);
            BeltRunnerLogger.SetProperty(logEvent, "eventType", "RunOnCompletedCallbackFailure");
        }, exception);
    }

    private static void PopulateRunEventProperties(LogEventInfo logEvent, RunEvent ev) {
        BeltRunnerLogger.SetProperty(logEvent, "runId", ev.RunId);
        BeltRunnerLogger.SetProperty(logEvent, "eventType", ev.GetType().Name);
    }

    private static void PopulatePhaseProperties(LogEventInfo logEvent, PhaseKey phaseKey, int phaseIndex) {
        BeltRunnerLogger.SetProperty(logEvent, "phaseKey", phaseKey.ToString());
        BeltRunnerLogger.SetProperty(logEvent, "phaseIndex", phaseIndex);
    }

    private static LogLevel GetPhaseCompletedLevel(PhaseResult result) {
        return result switch {
            PhaseResult.Failed => LogLevel.Error,
            PhaseResult.PartiallySucceeded => LogLevel.Warn,
            _ => LogLevel.Info
        };
    }

    private static LogLevel MapDiagnosticLevel(DiagnosticSeverity severity) {
        return severity switch {
            DiagnosticSeverity.Warning => LogLevel.Warn,
            DiagnosticSeverity.Error => LogLevel.Error,
            _ => LogLevel.Info
        };
    }

    private void RememberLastPhaseFaultException(Exception exception) {
        lock( this.gate ) {
            this.lastPhaseFaultException = exception;
        }
    }

    private bool ShouldSuppressRunFaultLog(Exception exception) {
        lock( this.gate ) {
            bool shouldSuppress = ReferenceEquals(this.lastPhaseFaultException, exception);
            this.lastPhaseFaultException = null;
            return shouldSuppress;
        }
    }

    private void ClearLastPhaseFaultException() {
        lock( this.gate ) {
            this.lastPhaseFaultException = null;
        }
    }

    private void ApplySnapshotEvent(RunEvent ev) {
        switch( ev ) {
            case RunStartedEvent:
                this.snapshotStore.OnRunStarted();
                break;
            case RunCancellingEvent:
                this.snapshotStore.OnRunCancelling();
                break;
            case PhaseStartedEvent phaseStarted:
                this.snapshotStore.OnPhaseStarted(phaseStarted.PhaseKey);
                break;
            case PhaseCompletedEvent phaseCompleted:
                this.snapshotStore.OnPhaseCompleted(phaseCompleted.PhaseKey);
                break;
            case PhaseFaultedEvent phaseFaulted:
                if( phaseFaulted.SourceException is not null ) {
                    this.snapshotStore.OnPhaseFaulted(phaseFaulted.PhaseKey, phaseFaulted.SourceException);
                    PublishAutomaticDiagnostic(phaseFaulted.PhaseKey, phaseFaulted.SourceException);
                }
                break;
        }
    }

    private void PublishAutomaticDiagnostic(PhaseKey phaseKey, Exception exception) {
        if( phaseKey is null ) throw new ArgumentNullException(nameof(phaseKey));
        if( exception is null ) throw new ArgumentNullException(nameof(exception));
        if( !ShouldEmitDiagnostic(DiagnosticSeverity.Error) ) {
            return;
        }

        PublicFaultInfo faultInfo = CreatePhaseFaultInfo(phaseKey, exception);
        AddDiagnostic(new DiagnosticEntry(
            Guid.NewGuid(),
            DateTimeOffset.UtcNow,
            DiagnosticSeverity.Error,
            faultInfo.PublicMessage,
            faultInfo,
            phaseKey,
            null));
    }

    private bool ShouldEmitDiagnostic(DiagnosticSeverity severity) {
        return this.diagnosticMode switch {
            DiagnosticMode.Disabled => false,
            DiagnosticMode.ErrorsOnly => severity == DiagnosticSeverity.Error,
            _ => true
        };
    }

    private void AddDiagnostic(DiagnosticEntry entry) {
        if( entry is null ) throw new ArgumentNullException(nameof(entry));

        bool shouldPublish = false;

        lock( this.gate ) {
            if( this.isCompleted ) {
                return;
            }

            this.diagnosticLog.Add(entry);
            shouldPublish = true;
        }

        if( shouldPublish ) {
            this.diagnostics.OnNext(entry);
        }
    }

    private void UpdateActiveInteractions(IReadOnlyList<IInteractionRequest> requests) {
        if( requests is null ) throw new ArgumentNullException(nameof(requests));

        lock( this.gate ) {
            this.activeInteractions.Clear();

            for( int i = 0; i < requests.Count; i++ ) {
                IInteractionRequest request = requests[i] ?? throw new InvalidOperationException("Interaction request list contains null.");
                this.activeInteractions.Add(new InteractionSnapshot(
                    request.RequestId,
                    request.Kind,
                    request.Title,
                    request.Message,
                    request.PhaseKey,
                    request.ResponseType,
                    request.Timestamp));
            }
        }
    }

    private IObservable<RunEvent> CreateReplayableEventsObservable() {
        return Observable.Create<RunEvent>(observer => {
            if( observer is null ) throw new ArgumentNullException(nameof(observer));

            IObserver<RunEvent> sink = Observer.Synchronize(observer);
            StoredRunEvent[] snapshot;
            long cutoff;
            bool completed;

            object bufferGate = new();
            bool replaying = true;
            Queue<StoredRunEvent> buffer = new();
            bool bufferedCompleted = false;
            IDisposable? liveSubscription = null;

            lock( this.gate ) {
                snapshot = this.eventLog.ToArray();
                cutoff = this.lastSequence;
                completed = this.isCompleted;

                if( !completed ) {
                    liveSubscription = this.events.Where(x => x.Sequence > cutoff)
                        .Subscribe(x => {
                            lock( bufferGate ) {
                                if( replaying ) buffer.Enqueue(x);
                                else sink.OnNext(x.Event);
                            }
                        }, ex => {
                            lock( bufferGate ) {
                                bufferedCompleted = true;
                                if( !replaying ) {
                                    sink.OnCompleted();
                                }
                            }
                        }, () => {
                            lock( bufferGate ) {
                                if( replaying ) bufferedCompleted = true;
                                else sink.OnCompleted();
                            }
                        });
                }
            }

            for( int i = 0; i < snapshot.Length; i++ )
                sink.OnNext(snapshot[i].Event);

            lock( bufferGate ) {
                replaying = false;

                while( buffer.Count > 0 )
                    sink.OnNext(buffer.Dequeue().Event);

                if( bufferedCompleted ) {
                    sink.OnCompleted();
                }
            }

            if( completed ) {
                liveSubscription?.Dispose();
                sink.OnCompleted();
                return Disposable.Empty;
            }

            return Disposable.Create(() => liveSubscription?.Dispose());
        });
    }

    internal PublicFaultInfo CreateRunFaultInfo(Exception exception) {
        if( exception is null ) throw new ArgumentNullException(nameof(exception));

        return this.publicFaultInfoPolicy.Create(exception, "run");
    }

    internal PublicFaultInfo CreatePhaseFaultInfo(PhaseKey phaseKey, Exception exception) {
        if( phaseKey is null ) throw new ArgumentNullException(nameof(phaseKey));
        if( exception is null ) throw new ArgumentNullException(nameof(exception));

        return this.publicFaultInfoPolicy.Create(exception, $"phase:{phaseKey}");
    }

    internal void SeedInitialArtifacts(IReadOnlyList<IProducedArtifact> initialArtifacts) {
        if( initialArtifacts is null ) throw new ArgumentNullException(nameof(initialArtifacts));

        HashSet<IArtifactKey> seen = new(ArtifactKeySignatureComparer.Instance);

        for( int i = 0; i < initialArtifacts.Count; i++ ) {
            IProducedArtifact item = initialArtifacts[i] ?? throw new ArgumentException(INITIAL_ARTIFACTS_NULL_ITEM_MESSAGE, nameof(initialArtifacts));
            IArtifactKey key = item.Key ?? throw new InvalidOperationException(INITIAL_ARTIFACT_KEY_NULL_MESSAGE);

            if( !seen.Add(key) ) {
                throw new InvalidOperationException($"Duplicate initial artifact key signature was provided. key=\"{key.Name}\" type=\"{key.ValueType.FullName}\"");
            }

            this.artifacts.SetBoxed(key, item.Value);
        }
    }

    /// <summary>
    /// Releases resources owned by the run and settles pending completion state if necessary.
    /// </summary>
    /// <remarks>
    /// Disposal is best-effort. Teardown exceptions are swallowed so cleanup paths can safely dispose a run without
    /// risking secondary failures.
    /// </remarks>
    public void Dispose() {
        if( Interlocked.Exchange(ref this.disposeState, 1) != 0 ) {
            return;
        }

        try { TryCancelFromDispose(); } catch { }
        try { EnsureCompletionSettledFromCurrentState(); } catch { }

        try {
            // Best-effort: completion callbacks must not prevent run disposal.
            this.WaitForOnCompletedCallback();
        } catch {
            // Intentionally ignored. Dispose must not throw.
        }

        try {
            // Best-effort: external cleanup must not prevent run disposal.
            this.InvokeBeforeRunDisposeAsync().GetAwaiter().GetResult();
        } catch {
            // Intentionally ignored. Dispose must not throw.
        }

        try { this.cancellationRegistration.Dispose(); } catch { }
        try { this.activeRequestsSubscription.Dispose(); } catch { }
        try { this.Interaction.Dispose(); } catch { }
        try { this.snapshotStore.Dispose(); } catch { }
        try { this.diagnosticsSubject.Dispose(); } catch { }
        try { this.eventsSubject.Dispose(); } catch { }
    }
}
