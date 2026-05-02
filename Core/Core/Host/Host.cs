using System;
using System.Collections.Generic;
using System.Reactive.Linq;
using System.Reactive.Subjects;
using System.Threading;
using System.Threading.Tasks;
using BeltRunner.Core.Execution;
using BeltRunner.Core.Execution.Outcome;
using BeltRunner.Core.Logging;
using BeltRunner.Core.Plan;
using BeltRunner.Core.Plan.Artifacts;
using NLog;

namespace BeltRunner.Core.Host;

/// <summary>
/// Default implementation of <see cref="IHost"/>.
/// </summary>
/// <remarks>
/// <para>
/// This host controls the lifecycle of a single active run at a time.
/// It is responsible for host-level state and cancellation orchestration.
/// </para>
/// <para>
/// Thread-safety:
/// Public members are safe to call concurrently.
/// Internal state is guarded by a private lock.
/// </para>
/// </remarks>
public class Host : IHost {
    private const string HOST_DISPOSED_CANCEL_REASON = "Host was disposed.";
    private const string HOST_DEFAULT_CANCEL_REASON = "Cancellation was requested by host.";
    private static readonly Logger logger = BeltRunnerLogger.GetLogger<Host>();

    #region State
    /// <inheritdoc />
    public bool IsRunning => this.isRunning;
    private bool isRunning;

    /// <inheritdoc />
    public HostState State => this.state;
    private HostState state = HostState.Idle;
    #endregion

    #region Events

    /// <inheritdoc />
    public IObservable<HostEvent> EventStream => this.eventsStream;

    /// <inheritdoc />
    public IObservable<HostStateChangedEvent> StateChanges => this.stateChanges;

    /// <inheritdoc />
    public IObservable<HostFaultedEvent> Faults => this.faults;

    private readonly Subject<HostEvent> eventsSubject = new();

    // A synchronized wrapper over the subject to make OnNext thread-safe.
    private readonly ISubject<HostEvent> events;

    private readonly IObservable<HostEvent> eventsStream;
    private readonly IObservable<HostStateChangedEvent> stateChanges;
    private readonly IObservable<HostFaultedEvent> faults;

    #endregion

    private readonly object gate = new();
    private bool disposed;
    private readonly Launcher launcher;
    private readonly HostOptions options;

    private IRun? currentRun;
    private Guid? lastRunId;

    // Monotonic version used to ignore completion from previous runs.
    private int runVersion;

    // Run-scope CTS linked to StartAsync token and cancelled from Cancel/Dispose.
    private CancellationTokenSource? runCts;

    // Cancellation reason captured even if the run has not been created yet.
    private string pendingCancelReason = string.Empty;

    /// <summary>
    /// Initializes a new instance of the <see cref="Host"/> class with default options.
    /// </summary>
    public Host() : this(new HostOptions()) {
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="Host"/> class.
    /// </summary>
    /// <param name="options">Host-level behavior options.</param>
    public Host(HostOptions options) {
        this.options = options ?? throw new ArgumentNullException(nameof(options));
        LauncherConfiguration launcherConfiguration = new() {
            PublicFaultInfoPolicy = this.options.PublicFaultInfoPolicy,
            RunEventLogMaxRetainedCount = this.options.RunEventLogMaxRetainedCount,
            InteractionRequestLogMaxRetainedCount = this.options.InteractionRequestLogMaxRetainedCount,
            InteractionMaxPendingRequestCount = this.options.InteractionMaxPendingRequestCount,
            RunDiagnosticsMaxRetainedCount = this.options.RunDiagnosticsMaxRetainedCount,
            DiagnosticMode = this.options.DiagnosticMode,
            SnapshotPublishCoalescingInterval = this.options.SnapshotPublishCoalescingInterval == TimeSpan.Zero
                ? null
                : this.options.SnapshotPublishCoalescingInterval
        };
        this.launcher = new Launcher(this.options.InteractionBrokerFactory, launcherConfiguration);
        this.events = Subject.Synchronize(this.eventsSubject);
        this.eventsStream = this.events.AsObservable();
        this.stateChanges = this.eventsStream.OfType<HostStateChangedEvent>();
        this.faults = this.eventsStream.OfType<HostFaultedEvent>();
    }

    /// <inheritdoc />
    public Task<IRun> StartAsync(SequentialPlan plan, CancellationToken ct = default) {
        return StartAsync(plan, Array.Empty<IProducedArtifact>(), ct);
    }

    /// <inheritdoc />
    public async Task<IRun> StartAsync(SequentialPlan plan, IReadOnlyList<IProducedArtifact> initialArtifacts, CancellationToken ct = default) {
        return await StartAsync(plan, initialArtifacts, new RunLaunchOptions(), ct).ConfigureAwait(false);
    }

    /// <inheritdoc />
    public Task<IRun> StartAsync(SequentialPlan plan, RunLaunchOptions options, CancellationToken ct = default) {
        return StartAsync(plan, Array.Empty<IProducedArtifact>(), options, ct);
    }

    /// <inheritdoc />
    public async Task<IRun> StartAsync(SequentialPlan plan, IReadOnlyList<IProducedArtifact> initialArtifacts, RunLaunchOptions options, CancellationToken ct = default) {
        if( plan is null ) throw new ArgumentNullException(nameof(plan));
        if( initialArtifacts is null ) throw new ArgumentNullException(nameof(initialArtifacts));
        if( options is null ) throw new ArgumentNullException(nameof(options));
        ThrowIfDisposed();
        LogStartRequested(plan, initialArtifacts.Count);

        CancellationTokenSource linkedCts;

        lock( this.gate ) {
            if( this.state is HostState.Running or HostState.Cancelling )
                throw new InvalidOperationException("Host is already running.");

            this.isRunning = true;
            this.state = HostState.Running;

            this.pendingCancelReason = string.Empty;

            this.runVersion++;

            this.runCts?.Dispose();
            linkedCts = CancellationTokenSource.CreateLinkedTokenSource(ct);
            this.runCts = linkedCts;
        }

        try {
            // Pass plan + initial artifacts to launcher.
            IRun run = await this.launcher.LaunchAsync(plan, initialArtifacts, options, linkedCts.Token).ConfigureAwait(false);

            int versionSnapshot;
            List<HostEvent> toPublish = new();

            bool shouldApplyPendingReason = false;
            string pendingReasonSnapshot = string.Empty;

            lock( this.gate ) {
                ThrowIfDisposed();

                this.currentRun = run;
                this.lastRunId = run.Id;
                versionSnapshot = this.runVersion;

                if( this.state == HostState.Cancelling && !string.IsNullOrEmpty(this.pendingCancelReason) ) {
                    shouldApplyPendingReason = true;
                    pendingReasonSnapshot = this.pendingCancelReason;
                }

                toPublish.Add(new HostStateChangedEvent(this.state));
            }

            PublishMany(toPublish);

            if( shouldApplyPendingReason )
                run.RequestCancellation(pendingReasonSnapshot);

            _ = ObserveRunCompletionAsync(run, versionSnapshot);

            return run;
        } catch {
            CancellationTokenSource? ctsToDispose;
            List<HostEvent> toPublish = new();

            lock( this.gate ) {
                ctsToDispose = this.runCts;
                this.runCts = null;

                this.isRunning = false;
                this.state = HostState.Idle;
                this.currentRun = null;

                this.pendingCancelReason = string.Empty;

                toPublish.Add(new HostStateChangedEvent(this.state));
            }

            ctsToDispose?.Dispose();
            PublishMany(toPublish);

            throw;
        }
    }

    /// <inheritdoc />
    public void Cancel(string reason = "") {
        ThrowIfDisposed();

        string safeReason = TextConstraints.NormalizeOptional(reason, TextConstraints.CANCEL_REASON_MAX_LENGTH);

        if( string.IsNullOrWhiteSpace(safeReason) )
            safeReason = HOST_DEFAULT_CANCEL_REASON;

        IRun? runToSignal = null;
        CancellationTokenSource? ctsToCancel = null;
        List<HostEvent> toPublish = new();

        lock( this.gate ) {
            if( this.disposed )
                throw new ObjectDisposedException(nameof(Host));

            if( this.state is HostState.Idle or HostState.Completed or HostState.Cancelled or HostState.Faulted )
                return;

            this.pendingCancelReason = safeReason;

            if( this.state == HostState.Running ) {
                this.state = HostState.Cancelling;
                toPublish.Add(new HostStateChangedEvent(this.state));
            }

            runToSignal = this.currentRun;
            ctsToCancel = this.runCts;
        }

        LogCancellationRequested(safeReason, runToSignal?.Id ?? this.lastRunId);
        PublishMany(toPublish);

        runToSignal?.RequestCancellation(safeReason);
        ctsToCancel?.Cancel();
    }

    private async Task ObserveRunCompletionAsync(IRun run, int versionSnapshot) {
        try {
            RunOutcome outcome = await run.Completion.ConfigureAwait(false);

            HostState terminal = DetermineTerminalState(outcome);
            HostFaultedEvent? terminalFault = terminal == HostState.Faulted ? CreateOutcomeFaultEvent(outcome) : null;

            CancellationTokenSource? ctsToDispose = null;
            List<HostEvent> toPublish = new();

            lock( this.gate ) {
                if( this.disposed )
                    return;

                if( this.runVersion != versionSnapshot )
                    return;

                if( !ReferenceEquals(this.currentRun, run) )
                    return;

                if( terminal == HostState.Cancelled && this.state == HostState.Running ) {
                    this.state = HostState.Cancelling;
                    toPublish.Add(new HostStateChangedEvent(this.state));
                }

                this.isRunning = false;
                this.state = terminal;
                this.currentRun = null;

                ctsToDispose = this.runCts;
                this.runCts = null;

                toPublish.Add(new HostStateChangedEvent(this.state));

                if( terminal == HostState.Faulted && terminalFault is not null )
                    toPublish.Add(terminalFault);
            }

            ctsToDispose?.Dispose();
            PublishMany(toPublish);
        } catch( Exception ex ) {
            CancellationTokenSource? ctsToDispose = null;
            List<HostEvent> toPublish = new();

            lock( this.gate ) {
                if( this.disposed )
                    return;

                if( this.runVersion != versionSnapshot )
                    return;

                if( !ReferenceEquals(this.currentRun, run) )
                    return;

                this.isRunning = false;
                this.state = HostState.Faulted;
                this.currentRun = null;

                ctsToDispose = this.runCts;
                this.runCts = null;

                toPublish.Add(new HostStateChangedEvent(this.state));
                toPublish.Add(new HostFaultedEvent(ex, this.options.PublicFaultInfoPolicy.Create(ex, "host")));
            }

            ctsToDispose?.Dispose();
            PublishMany(toPublish);
        }
    }

    private HostState DetermineTerminalState(RunOutcome outcome) {
        if( outcome is null ) throw new ArgumentNullException(nameof(outcome));

        if( outcome.Kind == RunOutcomeKind.Cancelled )
            return HostState.Cancelled;

        if( outcome.Kind == RunOutcomeKind.Faulted )
            return HostState.Faulted;

        if( this.options.FaultOnFailedOutcome && outcome.Kind == RunOutcomeKind.Failed )
            return HostState.Faulted;

        return HostState.Completed;
    }

    private static HostFaultedEvent CreateOutcomeFaultEvent(RunOutcome outcome) {
        if( outcome is null ) throw new ArgumentNullException(nameof(outcome));

        if( outcome.Kind == RunOutcomeKind.Faulted && outcome.FaultInfo is not null ) {
            return new HostFaultedEvent(outcome.FaultInfo);
        }

        if( outcome.Kind == RunOutcomeKind.Failed ) {
            return new HostFaultedEvent(new PublicFaultInfo(
                "FailedOutcomeAsFaulted",
                "The run completed with a failed outcome and was treated as faulted by host policy.",
                null,
                "host",
                DateTimeOffset.UtcNow));
        }

        return new HostFaultedEvent(new PublicFaultInfo(
            "OutcomeTreatedAsFaulted",
            "The run completed with an outcome that was treated as faulted by host policy.",
            null,
            "host",
            DateTimeOffset.UtcNow));
    }

    private void Publish(HostEvent e) {
        LogHostEvent(e);
        this.events.OnNext(e);
    }

    private void PublishMany(IEnumerable<HostEvent> eventsToPublish) {
        foreach( HostEvent e in eventsToPublish )
            Publish(e);
    }

    private void ThrowIfDisposed() {
        if( this.disposed )
            throw new ObjectDisposedException(nameof(Host));
    }

    private void LogStartRequested(SequentialPlan plan, int initialArtifactCount) {
        BeltRunnerLogger.Write(logger, LogLevel.Info, "Host start requested.", logEvent => {
            BeltRunnerLogger.SetProperty(logEvent, "planType", nameof(SequentialPlan));
            BeltRunnerLogger.SetProperty(logEvent, "initialArtifactCount", initialArtifactCount);
            BeltRunnerLogger.SetProperty(logEvent, "eventType", "HostStartRequested");
        });
    }

    private void LogCancellationRequested(string reason, Guid? runId) {
        BeltRunnerLogger.Write(logger, LogLevel.Warn, "Host cancellation requested.", logEvent => {
            BeltRunnerLogger.SetProperty(logEvent, "runId", runId);
            BeltRunnerLogger.SetProperty(logEvent, "cancelReason", reason);
            BeltRunnerLogger.SetProperty(logEvent, "eventType", "HostCancellationRequested");
        });
    }

    private void LogHostEvent(HostEvent hostEvent) {
        if( hostEvent is not HostStateChangedEvent stateChanged ) {
            return;
        }

        BeltRunnerLogger.Write(logger, LogLevel.Info, "Host state changed.", logEvent => {
            BeltRunnerLogger.SetProperty(logEvent, "runId", this.lastRunId);
            BeltRunnerLogger.SetProperty(logEvent, "hostState", stateChanged.NewState.ToString());
            BeltRunnerLogger.SetProperty(logEvent, "eventType", stateChanged.GetType().Name);
        });
    }

    /// <summary>
    /// Disposes the host and requests cancellation for the active run (if any).
    /// </summary>
    /// <remarks>
    /// <para>
    /// This method is best-effort and must not throw.
    /// It attempts to request run cancellation and cancel the internal token source.
    /// </para>
    /// <para>
    /// Empty catch blocks are intentional here:
    /// Dispose should be resilient even if the underlying run implementation throws during cancellation.
    /// If you need diagnostics, attach logging at the run or reporter level rather than throwing from Dispose.
    /// </para>
    /// </remarks>
    public void Dispose() {
        IRun? runToCancel = null;
        CancellationTokenSource? ctsToCancel = null;

        lock( this.gate ) {
            if( this.disposed )
                return;

            runToCancel = this.currentRun;
            ctsToCancel = this.runCts;

            this.currentRun = null;
            this.runCts = null;

            this.isRunning = false;
            this.state = HostState.Idle;

            this.disposed = true;
        }

        if( runToCancel is not null ) {
            try {
                // Best-effort: do not throw from Dispose.
                runToCancel.RequestCancellation(HOST_DISPOSED_CANCEL_REASON);
            } catch {
                // Intentionally ignored. Dispose must not throw.
            }
        }

        if( ctsToCancel is not null ) {
            try {
                // Best-effort: do not throw from Dispose.
                ctsToCancel.Cancel();
            } catch {
                // Intentionally ignored. Dispose must not throw.
            }

            ctsToCancel.Dispose();
        }

        this.events.OnCompleted();
        this.eventsSubject.Dispose();
    }
}

