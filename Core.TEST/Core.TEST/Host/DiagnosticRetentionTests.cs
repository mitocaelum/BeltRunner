using BeltRunner.Core.Execution;
using BeltRunner.Core.Execution.Interaction;
using BeltRunner.Core.Host;
using BeltRunner.Core.Phase;
using BeltRunner.Core.Plan;
using BeltRunner.Core.TEST.Testing;
using BeltRunner.Core.Units;
using BeltRunnerHost = BeltRunner.Core.Host.Host;

namespace BeltRunner.Core.TEST.Host;

/// <summary>
/// Verifies configurable retention behavior for run diagnostics.
/// </summary>
/// <remarks>
/// <para>Purpose: Confirm that the host can cap diagnostic retention without changing the default behavior.</para>
/// <para>Why this matters: Unbounded diagnostics make retained runtime history grow over time and amplify replay costs during telemetry-heavy runs.</para>
/// <para>Expected result: Null keeps all diagnostics, positive limits keep only the newest diagnostics, mode filtering works, and invalid limits are rejected.</para>
/// </remarks>
[TestFixture]
[TestOf(typeof(HostOptions))]
[TestOf(typeof(BeltRunnerHost))]
public sealed class DiagnosticRetentionTests {
    /// <summary>
    /// Verifies that the default host configuration keeps the full diagnostic history in the run log.
    /// </summary>
    /// <remarks>
    /// <para>Purpose: Define the default retention behavior when no diagnostic cap is configured.</para>
    /// <para>Why this matters: Users should not lose diagnostic history unless they opt into a retention bound.</para>
    /// <para>Expected result: The run retains every emitted diagnostic entry in order.</para>
    /// </remarks>
    [Test]
    public async Task Host_StartAsync_WithoutDiagnosticRetentionLimits_RetainsFullDiagnosticHistory() {
        using BeltRunnerHost host = new(new HostOptions {
            InteractionBrokerFactory = static () => new InMemoryInteractionBroker()
        });

        using IRun run = await host.StartAsync(CreatePlan(4), CancellationToken.None);
        await run.Completion.WaitAsync(TestTimeouts.Default);
        string[] retained = run.DiagnosticLog.Select(x => x.Message).ToArray();
        TestNarrative.Observe($"retainedDiagnostics={string.Join(", ", retained)}");

        Assert.That(retained, Is.EqualTo(new[] {
            "diag-0",
            "diag-1",
            "diag-2",
            "diag-3"
        }));
    }

    /// <summary>
    /// Verifies that configured retention limits keep only the newest diagnostics in the run log.
    /// </summary>
    /// <remarks>
    /// <para>Purpose: Confirm that retention limits trim older diagnostics while preserving the newest entries.</para>
    /// <para>Why this matters: Diagnostic bursts should not make retained in-memory history grow without bound.</para>
    /// <para>Expected result: Only the newest configured diagnostic messages remain in the log.</para>
    /// </remarks>
    [Test]
    public async Task Host_StartAsync_WithDiagnosticRetentionLimits_RetainsNewestDiagnosticsOnly() {
        using BeltRunnerHost host = new(new HostOptions {
            InteractionBrokerFactory = static () => new InMemoryInteractionBroker(),
            RunDiagnosticsMaxRetainedCount = 3
        });

        using IRun run = await host.StartAsync(CreatePlan(5), CancellationToken.None);
        await run.Completion.WaitAsync(TestTimeouts.Default);
        string[] retained = run.DiagnosticLog.Select(x => x.Message).ToArray();
        TestNarrative.Observe($"retainedDiagnostics={string.Join(", ", retained)}");

        Assert.That(retained, Is.EqualTo(new[] {
            "diag-2",
            "diag-3",
            "diag-4"
        }));
    }

    /// <summary>
    /// Verifies that diagnostics can be disabled entirely.
    /// </summary>
    /// <remarks>
    /// <para>Purpose: Define the observable behavior when diagnostic mode is disabled.</para>
    /// <para>Why this matters: Consumers may disable diagnostics for quieter runs or lower retention pressure.</para>
    /// <para>Expected result: The run emits no retained diagnostics.</para>
    /// </remarks>
    [Test]
    public async Task Host_StartAsync_WithDiagnosticModeDisabled_EmitsNoDiagnostics() {
        using BeltRunnerHost host = new(new HostOptions {
            InteractionBrokerFactory = static () => new InMemoryInteractionBroker(),
            DiagnosticMode = DiagnosticMode.Disabled
        });

        using IRun run = await host.StartAsync(CreateMixedDiagnosticPlan(), CancellationToken.None);
        await run.Completion.WaitAsync(TestTimeouts.Default);
        TestNarrative.Observe($"diagnosticCount={run.DiagnosticLog.Count}");

        Assert.That(run.DiagnosticLog, Is.Empty);
    }

    /// <summary>
    /// Verifies that the errors-only mode filters out information and warning diagnostics.
    /// </summary>
    /// <remarks>
    /// <para>Purpose: Confirm that diagnostic mode filtering preserves only error-level entries.</para>
    /// <para>Why this matters: Error-only mode should reduce noise without hiding failures.</para>
    /// <para>Expected result: Only the error diagnostic message is retained.</para>
    /// </remarks>
    [Test]
    public async Task Host_StartAsync_WithDiagnosticModeErrorsOnly_RetainsOnlyErrors() {
        using BeltRunnerHost host = new(new HostOptions {
            InteractionBrokerFactory = static () => new InMemoryInteractionBroker(),
            DiagnosticMode = DiagnosticMode.ErrorsOnly
        });

        using IRun run = await host.StartAsync(CreateMixedDiagnosticPlan(), CancellationToken.None);
        await run.Completion.WaitAsync(TestTimeouts.Default);
        string[] retained = run.DiagnosticLog.Select(x => x.Message).ToArray();
        TestNarrative.Observe($"retainedDiagnostics={string.Join(", ", retained)}");

        Assert.That(retained, Is.EqualTo(new[] {
            "diag-error"
        }));
    }

    /// <summary>
    /// Verifies that non-positive diagnostic retention limits are rejected.
    /// </summary>
    /// <remarks>
    /// <para>Purpose: Protect configuration validation for diagnostic retention limits.</para>
    /// <para>Why this matters: Zero or negative limits are ambiguous and should fail fast.</para>
    /// <para>Expected result: Assigning a non-positive retention count throws <see cref="ArgumentOutOfRangeException"/> for the <c>value</c> parameter.</para>
    /// </remarks>
    [Test]
    public void HostOptions_DiagnosticRetentionLimits_WithNonPositiveValue_Throws() {
        HostOptions options = new();

        ArgumentOutOfRangeException runEx = Assert.Throws<ArgumentOutOfRangeException>(() => options.RunDiagnosticsMaxRetainedCount = 0)!;
        TestNarrative.Observe($"setter rejected non-positive value with paramName={runEx.ParamName}");

        Assert.That(runEx.ParamName, Is.EqualTo("value"));
    }

    private static SequentialPlan CreatePlan(int diagnosticCount) {
        return new SequentialPlanBuilder()
            .Add(new DiagnosticPhaseFactory("phase/a", diagnosticCount), "Phase A")
            .Build();
    }

    private static SequentialPlan CreateMixedDiagnosticPlan() {
        return new SequentialPlanBuilder()
            .Add(new MixedDiagnosticPhaseFactory("phase/a"), "Phase A")
            .Build();
    }

    private sealed class DiagnosticPhaseFactory : PhaseFactoryBase {
        private readonly int diagnosticCount;

        public DiagnosticPhaseFactory(string key, int diagnosticCount) : base(key) {
            this.diagnosticCount = diagnosticCount;
        }

        public override IPhase Create() {
            return new DiagnosticPhase(this.diagnosticCount);
        }
    }

    private sealed class MixedDiagnosticPhaseFactory : PhaseFactoryBase {
        public MixedDiagnosticPhaseFactory(string key) : base(key) {
        }

        public override IPhase Create() {
            return new MixedDiagnosticPhase();
        }
    }

    private sealed class DiagnosticPhase : IPhase {
        private readonly int diagnosticCount;

        public DiagnosticPhase(int diagnosticCount) {
            this.diagnosticCount = diagnosticCount;
        }

        public string Name => "Diagnostics";

        public UnitSet Units { get; } = new();

        public Task<IPhaseOutcome> ExecuteAsync(IPhaseContext context, CancellationToken ct = default) {
            DiagnosticUnit unit = new("Diagnostic Unit");
            this.Units.AddAndLock(unit);

            for( int i = 0; i < this.diagnosticCount; i++ ) {
                context.Telemetry.Warn($"diag-{i}", unitId: unit.Id);
            }

            return Task.FromResult<IPhaseOutcome>(new PhaseOutcome());
        }
    }

    private sealed class MixedDiagnosticPhase : IPhase {
        public string Name => "Diagnostics";

        public UnitSet Units { get; } = new();

        public Task<IPhaseOutcome> ExecuteAsync(IPhaseContext context, CancellationToken ct = default) {
            DiagnosticUnit unit = new("Diagnostic Unit");
            this.Units.AddAndLock(unit);
            context.Telemetry.Info("diag-info", unit.Id);
            context.Telemetry.Warn("diag-warn", unitId: unit.Id);
            context.Telemetry.Error("diag-error", new InvalidOperationException("boom"), unit.Id);
            return Task.FromResult<IPhaseOutcome>(new PhaseOutcome());
        }
    }

    private sealed class DiagnosticUnit : Unit<string> {
        public DiagnosticUnit(string name) : base(name, name) {
        }
    }
}
