using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using BeltRunner.Core.Execution;
using BeltRunner.Core.Execution.Event;
using BeltRunner.Core.Execution.Interaction;
using BeltRunner.Core.Execution.Outcome;
using BeltRunner.Core.Logging;
using BeltRunner.Core.Plan;
using BeltRunner.Core.Plan.Artifacts;
using NLog;

namespace BeltRunner.Core.Host;

internal sealed class Launcher {
    private const string EXECUTOR_DID_NOT_SETTLE_MESSAGE = "Sequential plan execution returned without settling the run.";
    private static readonly Logger logger = BeltRunnerLogger.GetLogger<Launcher>();
    private static readonly DefaultSequentialPlanExecutor executor = new();

    private readonly Func<IInteractionBroker> createInteraction;
    private readonly LauncherConfiguration configuration;

    /// <summary>
    /// Initializes a new <see cref="Launcher"/> that creates runs with interaction disabled.
    /// </summary>
    /// <remarks>
    /// The created runs still expose a non-null <see cref="IRun.Interaction"/> broker.
    /// Calls to <see cref="IInteractionRequester.AskAsync{TResponse}(IInteractionRequest{TResponse}, CancellationToken)"/>
    /// and <see cref="IInteractionRequester.TryAskAsync{TResponse}(IInteractionRequest{TResponse}, CancellationToken)"/>
    /// fail fast because interaction is not enabled for that run.
    /// </remarks>
    internal Launcher() : this(CreateDisabledInteractionBroker) {
    }

    /// <summary>
    /// Initializes a new <see cref="Launcher"/> that creates runs with the specified interaction broker factory.
    /// </summary>
    /// <param name="createInteraction">Factory used to create a run-scoped interaction broker for each launched run.</param>
    /// <param name="configuration">Optional launch-time configuration for run creation.</param>
    internal Launcher(Func<IInteractionBroker> createInteraction, LauncherConfiguration? configuration = null) {
        this.createInteraction = createInteraction ?? throw new ArgumentNullException(nameof(createInteraction));
        this.configuration = configuration ?? new LauncherConfiguration();
        ValidateConfiguration(this.configuration);
    }

    public Task<IRun> LaunchAsync(SequentialPlan plan, CancellationToken ct = default) {
        return LaunchAsync(plan, Array.Empty<IProducedArtifact>(), ct);
    }

    public Task<IRun> LaunchAsync(SequentialPlan plan, IReadOnlyList<IProducedArtifact> initialArtifacts, CancellationToken ct = default) {
        return LaunchAsync(plan, initialArtifacts, new RunLaunchOptions(), ct);
    }

    public Task<IRun> LaunchAsync(SequentialPlan plan, RunLaunchOptions options, CancellationToken ct = default) {
        return LaunchAsync(plan, Array.Empty<IProducedArtifact>(), options, ct);
    }

    public async Task<IRun> LaunchAsync(SequentialPlan plan, IReadOnlyList<IProducedArtifact> initialArtifacts, RunLaunchOptions options, CancellationToken ct = default) {
        if( plan is null ) throw new ArgumentNullException(nameof(plan));
        if( initialArtifacts is null ) throw new ArgumentNullException(nameof(initialArtifacts));
        if( options is null ) throw new ArgumentNullException(nameof(options));

        IInteractionBroker interaction;
        try {
            interaction = this.createInteraction();
        } catch( Exception ex ) {
            LogLaunchFailure(runId: null, stage: "CreateInteractionBroker", ex);
            throw;
        }

        if( interaction is null ) {
            InvalidOperationException ex = new("Interaction broker factory returned null.");
            LogLaunchFailure(runId: null, stage: "CreateInteractionBroker", ex);
            throw ex;
        }

        if( interaction is InMemoryInteractionBroker inMemoryInteractionBroker ) {
            inMemoryInteractionBroker.RequestLogMaxRetainedCount = this.configuration.InteractionRequestLogMaxRetainedCount;
            inMemoryInteractionBroker.MaxPendingRequestCount = this.configuration.InteractionMaxPendingRequestCount;
        }

        RunConfiguration runConfiguration = new() {
            PublicFaultInfoPolicy = this.configuration.PublicFaultInfoPolicy,
            CancellationToken = ct,
            LifecycleCallbacks = options.LifecycleCallbacks,
            EventLogMaxRetainedCount = this.configuration.RunEventLogMaxRetainedCount,
            RunDiagnosticsMaxRetainedCount = this.configuration.RunDiagnosticsMaxRetainedCount,
            DiagnosticMode = this.configuration.DiagnosticMode,
            SnapshotPublishCoalescingInterval = this.configuration.SnapshotPublishCoalescingInterval
        };

        Run run = new(interaction, runConfiguration);

        try {
            executor.Preflight(plan, initialArtifacts);
            run.SeedInitialArtifacts(initialArtifacts);
            run.InitializeRuntimeState(plan.Steps);
            await run.InvokeBeforeExecutionStartAsync().ConfigureAwait(false);
            run.ActivateExecution();
        } catch( Exception ex ) {
            LogLaunchFailure(run.Id, "PreExecutionStart", ex);
            run.Dispose();
            throw;
        }

        _ = Task.Run(async () => {
            try {
                await executor.ExecuteAsync(plan, run, run.CancellationToken).ConfigureAwait(false);

                if( !run.Completion.IsCompleted ) {
                    InvalidOperationException ex = new(EXECUTOR_DID_NOT_SETTLE_MESSAGE);
                    LogExecutorContractViolation(run.Id, ex);
                    run.Publish(new RunFaultedEvent(run.Id, ex, run.CreateRunFaultInfo(ex)));
                    run.TryFault(ex);
                }
            } catch( OperationCanceledException ) when( run.CancellationToken.IsCancellationRequested || run.IsCancellationRequested ) {
                run.Publish(new RunCancelledEvent(run.Id, run.CancelReason));
                run.TryComplete(RunOutcome.Cancelled(run.CancelReason));
            } catch( Exception ex ) {
                run.Publish(new RunFaultedEvent(run.Id, ex, run.CreateRunFaultInfo(ex)));
                run.TryFault(ex);
            }
        }, CancellationToken.None);

        return run;
    }

    private static IInteractionBroker CreateDisabledInteractionBroker() {
        return new DisabledInteractionBroker();
    }

    private static void ValidateConfiguration(LauncherConfiguration configuration) {
        if( configuration is null ) throw new ArgumentNullException(nameof(configuration));
        if( configuration.RunEventLogMaxRetainedCount.HasValue && configuration.RunEventLogMaxRetainedCount.Value <= 0 )
            throw new ArgumentOutOfRangeException(nameof(configuration.RunEventLogMaxRetainedCount), "Run event log max retained count must be greater than zero.");
        if( configuration.InteractionRequestLogMaxRetainedCount.HasValue && configuration.InteractionRequestLogMaxRetainedCount.Value <= 0 )
            throw new ArgumentOutOfRangeException(nameof(configuration.InteractionRequestLogMaxRetainedCount), "Interaction request log max retained count must be greater than zero.");
        if( configuration.InteractionMaxPendingRequestCount <= 0 )
            throw new ArgumentOutOfRangeException(nameof(configuration.InteractionMaxPendingRequestCount), "Interaction max pending request count must be greater than zero.");
        if( configuration.RunDiagnosticsMaxRetainedCount.HasValue && configuration.RunDiagnosticsMaxRetainedCount.Value <= 0 )
            throw new ArgumentOutOfRangeException(nameof(configuration.RunDiagnosticsMaxRetainedCount), "Run diagnostics max retained count must be greater than zero.");
        if( configuration.SnapshotPublishCoalescingInterval.HasValue && configuration.SnapshotPublishCoalescingInterval.Value <= TimeSpan.Zero )
            throw new ArgumentOutOfRangeException(nameof(configuration.SnapshotPublishCoalescingInterval), "Snapshot publish coalescing interval must be greater than zero.");
    }

    private static void LogLaunchFailure(Guid? runId, string stage, Exception exception) {
        BeltRunnerLogger.Write(logger, LogLevel.Error, "Run launch failed before execution started.", logEvent => {
            BeltRunnerLogger.SetProperty(logEvent, "runId", runId);
            BeltRunnerLogger.SetProperty(logEvent, "launchStage", stage);
            BeltRunnerLogger.SetProperty(logEvent, "planType", nameof(SequentialPlan));
            BeltRunnerLogger.SetProperty(logEvent, "eventType", "RunLaunchFailure");
        }, exception);
    }

    private static void LogExecutorContractViolation(Guid runId, Exception exception) {
        BeltRunnerLogger.Write(logger, LogLevel.Warn, EXECUTOR_DID_NOT_SETTLE_MESSAGE, logEvent => {
            BeltRunnerLogger.SetProperty(logEvent, "runId", runId);
            BeltRunnerLogger.SetProperty(logEvent, "launchStage", "ExecutorCompletionContract");
            BeltRunnerLogger.SetProperty(logEvent, "planType", nameof(SequentialPlan));
            BeltRunnerLogger.SetProperty(logEvent, "eventType", "ExecutorContractViolation");
        }, exception);
    }
}
