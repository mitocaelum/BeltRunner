using BeltRunner.Core.Execution;
using BeltRunner.Core.Execution.Event;
using BeltRunner.Core.Execution.Interaction;
using BeltRunner.Core.Execution.Outcome;
using BeltRunner.Core.Host;
using BeltRunner.Core.Phase;
using BeltRunner.Core.Plan;
using BeltRunner.Core.TEST.Testing;
using BeltRunner.Core.Units;
using NLog;
using NLog.Config;
using NLog.Targets;
using BeltRunnerHost = BeltRunner.Core.Host.Host;

namespace BeltRunner.Core.TEST.Host;

/// <summary>
/// Verifies that BeltRunner emits internal framework logs through NLog without owning the application's configuration lifecycle.
/// </summary>
/// <remarks>
/// <para>Purpose: Protect the contract that BeltRunner logs only when the application provides matching NLog rules.</para>
/// <para>Why this matters: Framework logging must be useful when configured, silent when not configured, and must not duplicate phase fault exceptions.</para>
/// <para>Expected result: Matching NLog rules capture run and telemetry logs, missing or non-matching rules capture nothing, and a phase fault is logged only once.</para>
/// </remarks>
[TestFixture]
[NonParallelizable]
[TestOf(typeof(BeltRunnerHost))]
[TestOf(typeof(Run))]
public sealed class NLogIntegrationTests {
    private LoggingConfiguration? originalConfiguration;
    private bool originalThrowExceptions;

    /// <summary>
    /// Captures the current global NLog state before each test and resets the active configuration.
    /// </summary>
    [SetUp]
    public void SetUp() {
        this.originalConfiguration = LogManager.Configuration;
        this.originalThrowExceptions = LogManager.ThrowExceptions;
        LogManager.ThrowExceptions = true;
        LogManager.Configuration = null;
    }

    /// <summary>
    /// Restores the global NLog state after each test.
    /// </summary>
    [TearDown]
    public void TearDown() {
        LogManager.Configuration = this.originalConfiguration;
        LogManager.ThrowExceptions = this.originalThrowExceptions;
    }

    /// <summary>
    /// Verifies that BeltRunner completes normally when no NLog configuration is active.
    /// </summary>
    /// <remarks>
    /// <para>Purpose: Define BeltRunner behavior when the application has not provided any NLog configuration.</para>
    /// <para>Why this matters: Framework logging should not fail or attach to detached targets when logging is not configured.</para>
    /// <para>Expected result: The run succeeds, NLog remains unconfigured, and the detached memory target receives no log entries.</para>
    /// </remarks>
    [Test]
    public async Task Host_StartAsync_WithoutNLogConfiguration_CompletesWithoutWritingToDetachedTarget() {
        MemoryTarget detachedTarget = CreateTarget("detached");
        LogManager.Configuration = null;

        using BeltRunnerHost host = CreateHost();
        using IRun run = await host.StartAsync(CreatePlan((_, _) => new PhaseOutcome()), CancellationToken.None);

        RunOutcome outcome = await run.Completion.WaitAsync(TestTimeouts.Default);
        TestNarrative.ObserveMany(
            $"outcomeKind={outcome.Kind}",
            $"configurationIsNull={LogManager.Configuration is null}",
            $"detachedLogCount={detachedTarget.Logs.Count}");

        Assert.Multiple(() => {
            Assert.That(outcome.Kind, Is.EqualTo(RunOutcomeKind.Succeeded));
            Assert.That(LogManager.Configuration, Is.Null);
            Assert.That(detachedTarget.Logs, Is.Empty);
        });
    }

    /// <summary>
    /// Verifies that matching BeltRunner logger rules capture run lifecycle events.
    /// </summary>
    /// <remarks>
    /// <para>Purpose: Confirm that matching NLog rules capture framework lifecycle logging.</para>
    /// <para>Why this matters: BeltRunner logging should become visible when the application explicitly opts into the logger namespace.</para>
    /// <para>Expected result: The target contains host and run lifecycle entries for run start, phase transitions, and host state changes.</para>
    /// </remarks>
    [Test]
    public async Task Host_StartAsync_WithBeltRunnerRule_WritesRunLifecycleLogs() {
        MemoryTarget target = ConfigureTarget("BeltRunner.*");

        using BeltRunnerHost host = CreateHost();
        using IRun run = await host.StartAsync(CreatePlan((_, _) => new PhaseOutcome()), CancellationToken.None);

        await run.Completion.WaitAsync(TestTimeouts.Default);
        TestNarrative.ObserveMany(
            $"logCount={target.Logs.Count}",
            $"firstLog={(target.Logs.Count > 0 ? target.Logs[0] : "none")}",
            $"containsRunStarted={target.Logs.Any(log => log.Contains("Run started."))}",
            $"containsHostRunning={target.Logs.Any(log => log.Contains("hostState=Running"))}");

        Assert.Multiple(() => {
            Assert.That(target.Logs.Any(log => log.Contains("BeltRunner.Core.Execution.Run|INFO|Run started.")), Is.True);
            Assert.That(target.Logs.Any(log => log.Contains("BeltRunner.Core.Execution.Run|INFO|Phase started.") && log.Contains("phaseKey=phase/a") && log.Contains("phaseIndex=0")), Is.True);
            Assert.That(target.Logs.Any(log => log.Contains("BeltRunner.Core.Execution.Run|INFO|Phase completed.") && log.Contains("phaseKey=phase/a") && log.Contains("phaseIndex=0") && log.Contains("phaseResult=Succeeded")), Is.True);
            Assert.That(target.Logs.Any(log => log.Contains("BeltRunner.Core.Execution.Run|INFO|Run completed.")), Is.True);
            Assert.That(target.Logs.Any(log => log.Contains("BeltRunner.Core.Host.Host|INFO|Host start requested.")), Is.True);
            Assert.That(target.Logs.Any(log => log.Contains("BeltRunner.Core.Host.Host|INFO|Host state changed.") && log.Contains("hostState=Running")), Is.True);
        });
    }

    /// <summary>
    /// Verifies that telemetry diagnostics are emitted with severity and correlation properties.
    /// </summary>
    /// <remarks>
    /// <para>Purpose: Confirm that telemetry diagnostics flow into NLog with their severity and correlation metadata intact.</para>
    /// <para>Why this matters: Logged diagnostics are only useful if they retain the unit, phase, and exception details needed for investigation.</para>
    /// <para>Expected result: Warning and error telemetry entries are logged with the expected severity, phase key, unit id, and exception details.</para>
    /// </remarks>
    [Test]
    public async Task Host_StartAsync_WithTelemetryDiagnostics_WritesWarningAndErrorLogs() {
        MemoryTarget target = ConfigureTarget("BeltRunner.*");

        InvalidOperationException diagnosticException = new("telemetry failure");
        using BeltRunnerHost host = CreateHost();
        using IRun run = await host.StartAsync(CreatePlan((phase, context) => {
            DiagnosticUnit unit = new("Diagnostic Unit");
            phase.Units.AddAndLock(unit);
            context.Telemetry.Warn("phase warning", unitId: unit.Id);
            context.Telemetry.Error("phase error", diagnosticException, unit.Id);
            return new PhaseOutcome();
        }), CancellationToken.None);

        await run.Completion.WaitAsync(TestTimeouts.Default);
        string warningLog = target.Logs.Single(log => log.Contains("phase warning"));
        string errorLog = target.Logs.Single(log => log.Contains("phase error"));
        TestNarrative.ObserveMany(
            $"warningLog={warningLog}",
            $"errorLog={errorLog}");

        Assert.Multiple(() => {
            Assert.That(target.Logs.Any(log => log.Contains("BeltRunner.Core.Execution.Run|WARN|phase warning") && log.Contains("phaseKey=phase/a") && log.Contains("severity=Warning") && log.Contains("unitId=")), Is.True);
            Assert.That(target.Logs.Any(log => log.Contains("BeltRunner.Core.Execution.Run|ERROR|phase error") && log.Contains("phaseKey=phase/a") && log.Contains("severity=Error") && log.Contains("unitId=") && log.Contains("exceptionType=System.InvalidOperationException") && log.Contains("exceptionMessage=telemetry failure")), Is.True);
        });
    }

    /// <summary>
    /// Verifies that a phase fault is logged once even though the run also faults with the same exception.
    /// </summary>
    /// <remarks>
    /// <para>Purpose: Confirm that raw exception information is kept in NLog without duplicating fault entries across host and run loggers.</para>
    /// <para>Why this matters: Duplicate fault logging creates noisy error streams and makes single-fault incidents look worse than they are.</para>
    /// <para>Expected result: The run exposes sanitized public fault info, and NLog records exactly one error entry with the raw exception details.</para>
    /// </remarks>
    [Test]
    public async Task Host_StartAsync_WhenPhaseThrows_LogsSingleFaultEntry_AndKeepsRawExceptionInNLogOnly() {
        MemoryTarget target = ConfigureTarget("BeltRunner.*");

        using BeltRunnerHost host = CreateHost();
        using IRun run = await host.StartAsync(CreatePlan((_, _) => throw new InvalidOperationException("boom")), CancellationToken.None);

        RunOutcome outcome = await run.Completion.WaitAsync(TestTimeouts.Default);
        string errorLog = target.Logs.Single(log => log.Contains("|ERROR|"));
        TestNarrative.ObserveMany(
            $"outcomeKind={outcome.Kind}",
            $"faultKind={outcome.FaultInfo?.FaultKind}",
            $"errorLogCount={target.Logs.Count(log => log.Contains("|ERROR|"))}",
            $"errorLog={errorLog}");

        Assert.Multiple(() => {
            Assert.That(outcome.Kind, Is.EqualTo(RunOutcomeKind.Faulted));
            Assert.That(outcome.FaultInfo, Is.Not.Null);
            Assert.That(outcome.FaultInfo!.FaultKind, Is.EqualTo(nameof(InvalidOperationException)));
            Assert.That(outcome.FaultInfo.PublicMessage, Is.EqualTo("The run failed with an unhandled exception."));
            Assert.That(outcome.FaultInfo.Origin, Is.EqualTo("run"));
            Assert.That(target.Logs.Count(log => log.Contains("|ERROR|")), Is.EqualTo(1));
            Assert.That(target.Logs.Single(log => log.Contains("|ERROR|")), Does.Contain("Phase faulted."));
            Assert.That(target.Logs.Single(log => log.Contains("|ERROR|")), Does.Contain("exceptionMessage=boom"));
            Assert.That(target.Logs.Single(log => log.Contains("|ERROR|")), Does.Contain("exceptionType=System.InvalidOperationException"));
            Assert.That(target.Logs.Any(log => log.Contains("Run faulted.")), Is.False);
            Assert.That(target.Logs.Any(log => log.Contains("BeltRunner.Core.Host.Host|ERROR|")), Is.False);
        });
    }

    /// <summary>
    /// Verifies that host cancellation reasons are sanitized before they reach public surfaces and NLog properties.
    /// </summary>
    /// <remarks>
    /// <para>Purpose: Confirm that cancellation reason sanitization is consistent across the public run surface and NLog event properties.</para>
    /// <para>Why this matters: Cancellation text can contain unsafe control characters that should not leak into logs or user-facing output.</para>
    /// <para>Expected result: The cancelled outcome, run surface, run event, and host cancellation log all contain the same sanitized reason text.</para>
    /// </remarks>
    [Test]
    public async Task Host_Cancel_WithControlCharacters_SanitizesCancellationReason_ForLogsAndRunSurface() {
        MemoryTarget target = ConfigureTarget("BeltRunner.*");

        using BeltRunnerHost host = CreateHost();
        using IRun run = await host.StartAsync(CreatePlan((_, _) => {
            host.Cancel("line1\r\nline2\t\u001bline3");
            return new PhaseOutcome();
        }), CancellationToken.None);

        RunOutcome outcome = await run.Completion.WaitAsync(TestTimeouts.Default);
        RunCancelledEvent cancelledEvent = run.EventLog.OfType<RunCancelledEvent>().Single();
        string hostCancellationLog = target.Logs.Single(log => log.Contains("BeltRunner.Core.Host.Host|WARN|Host cancellation requested."));
        TestNarrative.ObserveMany(
            $"cancelledReason={outcome.CancellationReason}",
            $"runCancelReason={run.CancelReason}",
            $"eventReason={cancelledEvent.Reason}",
            $"hostCancellationLog={hostCancellationLog}");

        Assert.Multiple(() => {
            Assert.That(outcome.Kind, Is.EqualTo(RunOutcomeKind.Cancelled));
            Assert.That(outcome.CancellationReason, Is.EqualTo("line1  line2  line3"));
            Assert.That(run.CancelReason, Is.EqualTo("line1  line2  line3"));
            Assert.That(cancelledEvent.Reason, Is.EqualTo("line1  line2  line3"));
            Assert.That(hostCancellationLog, Does.Contain("cancelReason=line1  line2  line3"));
            Assert.That(hostCancellationLog, Does.Not.Contain("\r"));
            Assert.That(hostCancellationLog, Does.Not.Contain("\n"));
            Assert.That(hostCancellationLog, Does.Not.Contain("\t"));
            Assert.That(hostCancellationLog.IndexOf('\u001b'), Is.EqualTo(-1));
        });
    }

    /// <summary>
    /// Verifies that non-matching logger rules do not capture BeltRunner logs.
    /// </summary>
    /// <remarks>
    /// <para>Purpose: Confirm that BeltRunner respects the application's logger pattern boundaries.</para>
    /// <para>Why this matters: Framework logs should remain silent when the configured logger rules do not target the BeltRunner namespace.</para>
    /// <para>Expected result: The target receives no entries when the configured rule does not match BeltRunner loggers.</para>
    /// </remarks>
    [Test]
    public async Task Host_StartAsync_WithNonMatchingRule_DoesNotWriteLogs() {
        MemoryTarget target = ConfigureTarget("Microsoft.*");

        using BeltRunnerHost host = CreateHost();
        using IRun run = await host.StartAsync(CreatePlan((_, _) => new PhaseOutcome()), CancellationToken.None);

        await run.Completion.WaitAsync(TestTimeouts.Default);
        TestNarrative.Observe($"logCount={target.Logs.Count}");

        Assert.That(target.Logs, Is.Empty);
    }

    private static BeltRunnerHost CreateHost() {
        return new BeltRunnerHost(new HostOptions {
            InteractionBrokerFactory = static () => new InMemoryInteractionBroker()
        });
    }

    private static SequentialPlan CreatePlan(Func<CallbackPhase, IPhaseContext, IPhaseOutcome> onExecute) {
        CallbackPhaseFactory factory = new("phase/a", onExecute);
        return new SequentialPlanBuilder()
            .Add(factory, "Phase A")
            .Build();
    }

    private static MemoryTarget ConfigureTarget(string loggerPattern) {
        MemoryTarget target = CreateTarget("memory");
        LoggingConfiguration configuration = new();
        configuration.AddTarget(target);
        configuration.LoggingRules.Add(new LoggingRule(loggerPattern, LogLevel.Trace, target));
        LogManager.Configuration = configuration;
        return target;
    }

    private static MemoryTarget CreateTarget(string name) {
        return new MemoryTarget(name) {
            Layout = "${logger}|${level:uppercase=true}|${message}|runId=${event-properties:item=runId}|phaseKey=${event-properties:item=phaseKey}|phaseIndex=${event-properties:item=phaseIndex}|phaseResult=${event-properties:item=phaseResult}|unitId=${event-properties:item=unitId}|cancelReason=${event-properties:item=cancelReason}|severity=${event-properties:item=diagnosticSeverity}|hostState=${event-properties:item=hostState}|eventType=${event-properties:item=eventType}|exceptionType=${exception:format=Type}|exceptionMessage=${exception:format=Message}"
        };
    }

    private sealed class CallbackPhaseFactory : PhaseFactoryBase {
        private readonly Func<CallbackPhase, IPhaseContext, IPhaseOutcome> onExecute;

        public CallbackPhaseFactory(string key, Func<CallbackPhase, IPhaseContext, IPhaseOutcome> onExecute) : base(key) {
            this.onExecute = onExecute ?? throw new ArgumentNullException(nameof(onExecute));
        }

        public override IPhase Create() {
            return new CallbackPhase(this.onExecute);
        }
    }

    private sealed class CallbackPhase : IPhase {
        private readonly Func<CallbackPhase, IPhaseContext, IPhaseOutcome> onExecute;

        public CallbackPhase(Func<CallbackPhase, IPhaseContext, IPhaseOutcome> onExecute) {
            this.onExecute = onExecute ?? throw new ArgumentNullException(nameof(onExecute));
        }

        public string Name => "Callback";

        public UnitSet Units { get; } = new();

        public Task<IPhaseOutcome> ExecuteAsync(IPhaseContext context, CancellationToken ct = default) {
            return Task.FromResult(this.onExecute(this, context));
        }
    }

    private sealed class DiagnosticUnit : Unit<string> {
        public DiagnosticUnit(string name) : base(name, name) {
        }
    }
}
