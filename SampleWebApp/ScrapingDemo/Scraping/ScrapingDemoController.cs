using BeltRunner.Core.Execution;
using BeltRunner.Core.Execution.Interaction;
using BeltRunner.Core.Execution.Outcome;
using BeltRunner.Core.Host;
using BeltRunner.Core.Plan;
using BeltRunner.Core.Plan.Artifacts;
using BeltRunner.SampleWebApp.ScrapingDemo.Phases;
using BeltRunnerHost = BeltRunner.Core.Host.IHost;

namespace BeltRunner.SampleWebApp.ScrapingDemo.Scraping;

/// <summary>
/// Coordinates workflow execution, interaction handling, and UI state updates for the sample screen.
/// </summary>
internal sealed class ScrapingDemoController : IDisposable {
    private readonly BeltRunnerHost host;
    private readonly InMemoryLogStore logStore;
    private readonly ScrapingDemoState state;
    private readonly object gate = new();

    private IDisposable? snapshotSubscription;
    private IDisposable? interactionSubscription;
    private IDisposable? diagnosticSubscription;
    private IInteractionBroker? interactionBroker;
    private IRun? currentRun;

    /// <summary>
    /// Initializes a new instance of the <see cref="ScrapingDemoController"/> class.
    /// </summary>
    /// <param name="host">The BeltRunner host used to execute plans.</param>
    /// <param name="logStore">The in-memory log store used by the sample UI.</param>
    /// <param name="state">The shared state object updated throughout the run.</param>
    public ScrapingDemoController(BeltRunnerHost host, InMemoryLogStore logStore, ScrapingDemoState state) {
        this.host = host;
        this.logStore = logStore;
        this.state = state;
    }

    /// <summary>
    /// Starts a new demo workflow if one is not already running.
    /// </summary>
    /// <param name="sourceUrl">The source URL entered by the operator.</param>
    /// <param name="injectAuthenticationChallenge">A value indicating whether Phase 1 should simulate a web authentication challenge.</param>
    /// <param name="injectMinorWarning">A value indicating whether the run should inject a recoverable anomaly.</param>
    /// <returns>A task that represents the asynchronous start operation.</returns>
    public async Task InvokeHost(string sourceUrl, bool injectAuthenticationChallenge, bool injectMinorWarning) {
        if( this.state.IsRunning )
            return;

        string normalizedSourceUrl = string.IsNullOrWhiteSpace(sourceUrl) ? "https://example.invalid/start" : sourceUrl.Trim();
        this.state.Reset();

        DisposeCurrentRun();

        try {
            //  Ref52
            IRun run = await this.host.StartSequentialAsync(
                ConfigurePlan,
                builder => ConfigureInitialArtifacts(builder, normalizedSourceUrl, injectAuthenticationChallenge, injectMinorWarning),
                CreateRunLaunchOptions()).ConfigureAwait(false);

            _ = ObserveCompletionAsync(run);
        } catch( Exception ex ) {
            this.state.FailRun(ex.Message);
        }
    }

    /// <summary>
    /// Sends a Boolean response to the active interaction request, if one exists.
    /// </summary>
    /// <param name="accepted">A value indicating whether the operator accepted the request.</param>
    /// <returns>A task that represents the asynchronous response operation.</returns>
    public Task RespondAsync(bool accepted) {
        Guid? requestId = this.state.DialogRequestId;
        if( !requestId.HasValue )
            return Task.CompletedTask;

        IInteractionBroker? broker;
        lock( this.gate ) {
            broker = this.interactionBroker;
        }

        if( broker is null ) {
            this.state.ClearInteraction();
            return Task.CompletedTask;
        }

        if( broker.TryRespond(requestId.Value, accepted) ) {
            this.state.ClearInteraction();
        }

        return Task.CompletedTask;
    }

    /// <summary>
    /// Sends authentication credentials to the active interaction request, if one exists.
    /// </summary>
    /// <param name="userName">The user name entered by the operator.</param>
    /// <param name="password">The password entered by the operator.</param>
    /// <returns>A task that represents the asynchronous response operation.</returns>
    public Task SubmitCredentialsAsync(string userName, string password) {
        Guid? requestId = this.state.DialogRequestId;
        if( !requestId.HasValue )
            return Task.CompletedTask;

        IInteractionBroker? broker;
        lock( this.gate ) {
            broker = this.interactionBroker;
        }

        if( broker is null ) {
            this.state.ClearInteraction();
            return Task.CompletedTask;
        }

        //  Ref56
        if( broker.TryRespond(requestId.Value, (userName ?? string.Empty, password ?? string.Empty)) ) {
            this.state.ClearInteraction();
        }

        return Task.CompletedTask;
    }

    /// <summary>
    /// Rejects the active interaction request, if one exists.
    /// </summary>
    /// <param name="reason">The rejection reason shown in diagnostics.</param>
    /// <returns>A task that represents the asynchronous rejection operation.</returns>
    public Task RejectAsync(string reason) {
        Guid? requestId = this.state.DialogRequestId;
        if( !requestId.HasValue )
            return Task.CompletedTask;

        IInteractionBroker? broker;
        lock( this.gate ) {
            broker = this.interactionBroker;
        }

        if( broker is null ) {
            this.state.ClearInteraction();
            return Task.CompletedTask;
        }

        //  Ref57
        if( broker.TryReject(requestId.Value, reason ?? string.Empty) ) {
            this.state.ClearInteraction();
        }

        return Task.CompletedTask;
    }

    /// <summary>
    /// Opens the log dialog with the latest in-memory log snapshot.
    /// </summary>
    public void ShowLogs() {
        this.state.OpenLogDialog(this.logStore.GetSnapshot());
    }

    /// <summary>
    /// Closes the log dialog.
    /// </summary>
    public void CloseLogs() {
        this.state.CloseLogDialog();
    }

    /// <summary>
    /// Releases the host and any active subscriptions.
    /// </summary>
    public void Dispose() {
        DisposeCurrentRun();
        this.host.Dispose();
    }

    private async Task ObserveCompletionAsync(IRun run) {
        try {
            //  RefID: RefCompletion
            RunOutcome outcome = await run.Completion.ConfigureAwait(false);
            ApplyResult(run);
            ApplyRunOutcome(outcome);
        } catch( OperationCanceledException ex ) {
            this.state.StopRun(string.IsNullOrWhiteSpace(ex.Message) ? "The run was stopped by the operator." : ex.Message);
        } catch( Exception ex ) {
            this.state.FailRun(ex.Message);
        } finally {
            run.Dispose();
        }
    }

    private void ApplyResult(IRun run) {
        if( !run.Artifacts.TryGet(ScrapingArtifacts.StatisticsSummary, out IReadOnlyList<(string Name, string Value, string Description)> items) ) {
            return;
        }

        int targetPageCount = run.Artifacts.TryGet(ScrapingArtifacts.TargetPages, out IReadOnlyList<string> targetPages)
            ? targetPages.Count
            : 0;

        int vectorCount = run.Artifacts.TryGet(ScrapingArtifacts.PageVectors, out IReadOnlyList<double[]> vectors)
            ? vectors.Count
            : 0;

        DateTimeOffset generatedAt = DateTimeOffset.Now;
        this.state.SetResult(generatedAt, targetPageCount, vectorCount, MapResultItems(items));
    }

    private void DisposeCurrentRun() {
        IRun? run;

        lock( this.gate ) {
            run = this.currentRun;
        }

        run?.Dispose();
    }

    private static void ConfigurePlan(SequentialPlanBuilder builder) {
        builder
            .Add(new DiscoverTargetsPhaseFactory())
            .Add(new ScrapePageContentPhaseFactory())
            .Add(new BuildLanguageStatisticsPhaseFactory());
    }

    private static void ConfigureInitialArtifacts(
        ArtifactSeedBuilder builder,
        string sourceUrl,
        bool injectAuthenticationChallenge,
        bool injectMinorWarning) {

        builder
            .Add(ScrapingArtifacts.SourceUrl, sourceUrl)
            .Add(ScrapingArtifacts.InjectAuthenticationChallenge, injectAuthenticationChallenge)
            .Add(ScrapingArtifacts.InjectMinorWarning, injectMinorWarning);
    }

    private RunLaunchOptions CreateRunLaunchOptions() {
        return new RunLaunchOptions {
            LifecycleCallbacks = new RunLifecycleCallbacks {
                BeforeExecutionStartAsync = run => {
                    // Hook the UI subscription before execution so the first snapshot is never missed.
                    AttachRunState(run);
                    return default;
                },
                BeforeRunDisposeAsync = run => {
                    DetachRunState(run);
                    return default;
                }
            }
        };
    }

    private void ApplyRunOutcome(RunOutcome outcome) {
        switch( outcome.Kind ) {
            case RunOutcomeKind.Cancelled:
                this.state.StopRun(string.IsNullOrWhiteSpace(outcome.CancellationReason) ? "The run was stopped by the operator." : outcome.CancellationReason);
                return;
            case RunOutcomeKind.Faulted:
                string faultMessage = string.IsNullOrWhiteSpace(outcome.FaultInfo?.PublicMessage) ? "The run failed." : outcome.FaultInfo.PublicMessage;
                if( IsOperatorInitiatedStop(faultMessage) ) {
                    this.state.StopRun(faultMessage);
                    return;
                }

                this.state.FailRun(faultMessage);
                return;
            case RunOutcomeKind.Failed:
                string summary = string.IsNullOrWhiteSpace(outcome.Summary) ? "The run failed." : outcome.Summary;
                if( IsOperatorInitiatedStop(summary) ) {
                    this.state.StopRun(summary);
                    return;
                }

                this.state.FailRun(summary);
                return;
            case RunOutcomeKind.PartiallySucceeded:
                this.state.CompleteRun(string.IsNullOrWhiteSpace(outcome.Summary) ? "The run completed with warnings." : outcome.Summary);
                return;
            default:
                this.state.CompleteRun("The run completed successfully.");
                return;
        }
    }

    private static bool IsOperatorInitiatedStop(string message) {
        if( string.IsNullOrWhiteSpace(message) ) {
            return false;
        }

        return message.Contains("operator", StringComparison.OrdinalIgnoreCase)
            && (message.Contains("stop", StringComparison.OrdinalIgnoreCase)
                || message.Contains("cancel", StringComparison.OrdinalIgnoreCase));
    }

    private static IReadOnlyList<ResultItemState> MapResultItems(IReadOnlyList<(string Name, string Value, string Description)> items) {
        ResultItemState[] mapped = new ResultItemState[items.Count];

        for( int i = 0; i < items.Count; i++ ) {
            (string name, string value, string description) = items[i];
            mapped[i] = new ResultItemState(name, value, description);
        }

        return mapped;
    }

    private void AttachRunState(IRun run) {
        if( run is null ) {
            throw new ArgumentNullException(nameof(run));
        }

        IDisposable? previousSnapshotSubscription;
        IDisposable? previousInteractionSubscription;
        IDisposable? previousDiagnosticSubscription;

        lock( this.gate ) {
            previousSnapshotSubscription = this.snapshotSubscription;
            previousInteractionSubscription = this.interactionSubscription;
            previousDiagnosticSubscription = this.diagnosticSubscription;
            this.currentRun = run;
            this.interactionBroker = run.Interaction;
            this.snapshotSubscription = run.SnapshotStream.Subscribe(new DelegateObserver<IRunSnapshot>(this.state.ApplySnapshot));
            //  Ref55
            this.interactionSubscription = run.Interaction.ActiveRequestsChanges.Subscribe(
                new DelegateObserver<IReadOnlyList<IInteractionRequest>>(_ => this.state.ApplyActiveInteractions(run.ActiveInteractions)));
            this.diagnosticSubscription = run.DiagnosticStream.Subscribe(
                new DelegateObserver<IDiagnosticEntry>(_ => this.state.ApplyDiagnostics(run.DiagnosticLog)));
        }

        this.state.ApplySnapshot(run.Snapshot);
        this.state.ApplyActiveInteractions(run.ActiveInteractions);
        this.state.ApplyDiagnostics(run.DiagnosticLog);

        previousSnapshotSubscription?.Dispose();
        previousInteractionSubscription?.Dispose();
        previousDiagnosticSubscription?.Dispose();
    }

    private void DetachRunState(IRun expectedRun) {
        if( expectedRun is null ) {
            throw new ArgumentNullException(nameof(expectedRun));
        }

        IDisposable? snapshotSubscription;
        IDisposable? interactionSubscription;
        IDisposable? diagnosticSubscription;

        lock( this.gate ) {
            if( !ReferenceEquals(this.currentRun, expectedRun) ) {
                return;
            }

            snapshotSubscription = this.snapshotSubscription;
            interactionSubscription = this.interactionSubscription;
            diagnosticSubscription = this.diagnosticSubscription;
            this.snapshotSubscription = null;
            this.interactionSubscription = null;
            this.diagnosticSubscription = null;
            this.interactionBroker = null;
            this.currentRun = null;
        }

        snapshotSubscription?.Dispose();
        interactionSubscription?.Dispose();
        diagnosticSubscription?.Dispose();
    }

    private sealed class DelegateObserver<T> : IObserver<T> {
        private readonly Action<T> onNext;

        public DelegateObserver(Action<T> onNext) {
            this.onNext = onNext;
        }

        public void OnCompleted() {
        }

        public void OnError(Exception error) {
        }

        public void OnNext(T value) {
            this.onNext(value);
        }
    }
}
