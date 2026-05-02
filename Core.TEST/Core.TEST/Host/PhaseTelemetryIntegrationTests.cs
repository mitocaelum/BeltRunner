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
/// Verifies end-to-end telemetry propagation from a phase into runtime snapshots and unit state.
/// </summary>
/// <remarks>
/// <para>Purpose: Protect the integration boundary between <see cref="IPhaseContext"/>, <see cref="IPhaseTelemetry"/>, and snapshot projection.</para>
/// <para>Why this matters: Telemetry is only useful if runtime state, diagnostics, and unit status all agree after execution completes.</para>
/// <para>Expected result: Telemetry updates populate run diagnostics, finalize unit state, and preserve the phase key observed during execution.</para>
/// </remarks>
[TestFixture]
[TestOf(typeof(IPhaseContext))]
[TestOf(typeof(IPhaseTelemetry))]
[TestOf(typeof(IPhaseProgressTracker))]
[TestOf(typeof(ITrackedUnitScope))]
[TestOf(typeof(PhaseTelemetryExtensions))]
public sealed class PhaseTelemetryIntegrationTests {
    /// <summary>
    /// Verifies that phase telemetry updates run diagnostics and runtime unit state during host execution.
    /// </summary>
    /// <remarks>
    /// <para>Purpose: Confirm that telemetry emitted from a phase reaches all runtime representations that a consumer would inspect.</para>
    /// <para>Why this matters: A broken connection between telemetry and snapshots would make debugging and progress reporting misleading.</para>
    /// <para>Expected result: The completed run exposes the expected phase key, completed unit state, full progress, and warning diagnostic in the run log.</para>
    /// </remarks>
    [Test]
    public async Task Host_StartAsync_PhaseTelemetry_UpdatesSnapshotDiagnostics_AndRuntimeUnitState() {
        using BeltRunnerHost host = new(new HostOptions {
            InteractionBrokerFactory = static () => new InMemoryInteractionBroker()
        });

        TelemetryPhaseFactory factory = new("phase/a");
        SequentialPlan plan = new SequentialPlanBuilder()
            .Add(factory, "Phase A")
            .Build();
        PhaseKey expectedKey = new("phase/a");

        using IRun run = await host.StartAsync(plan, CancellationToken.None);
        await run.Completion.WaitAsync(TestTimeouts.Default);

        IPhaseSnapshot phaseSnapshot = run.Snapshot.Phases[0];
        IUnitSnapshot unitSnapshot = phaseSnapshot.Units[0];
        IDiagnosticEntry diagnostic = run.DiagnosticLog[0];

        Assert.That(factory.ObservedUnit, Is.Not.Null);
        TestNarrative.ObserveMany(
            $"observedPhaseKey={factory.ObservedPhaseKey}",
            $"unitStatus={factory.ObservedUnit!.Status}",
            $"phaseStatus={phaseSnapshot.Status}",
            $"phaseRatio={phaseSnapshot.Ratio:0.####}",
            $"unitRatio={unitSnapshot.Ratio:0.####}",
            $"diagnosticSeverity={diagnostic.Severity}",
            $"diagnosticMessage={diagnostic.Message}");

        Assert.Multiple(() => {
            Assert.That(factory.ObservedPhaseKey, Is.EqualTo(expectedKey));
            Assert.That(factory.ObservedUnit!.CurrentPhaseKey, Is.EqualTo(expectedKey));
            Assert.That(factory.ObservedUnit.Status, Is.EqualTo(UnitStatus.Succeeded));
            Assert.That(phaseSnapshot.PhaseKey, Is.EqualTo(expectedKey));
            Assert.That(phaseSnapshot.Status, Is.EqualTo(PhaseStatus.Completed));
            Assert.That(phaseSnapshot.TotalUnits, Is.EqualTo(1));
            Assert.That(phaseSnapshot.ProcessedUnits, Is.EqualTo(1));
            Assert.That(phaseSnapshot.Ratio, Is.EqualTo(1.0).Within(0.0001));
            Assert.That(unitSnapshot.Name, Is.EqualTo("Telemetry Unit"));
            Assert.That(unitSnapshot.Status, Is.EqualTo(UnitStatus.Succeeded));
            Assert.That(unitSnapshot.Ratio, Is.EqualTo(1.0).Within(0.0001));
            Assert.That(diagnostic.Severity, Is.EqualTo(DiagnosticSeverity.Warning));
            Assert.That(diagnostic.Message, Is.EqualTo("Halfway there"));
            Assert.That(diagnostic.PhaseKey, Is.EqualTo(expectedKey));
            Assert.That(diagnostic.UnitId, Is.EqualTo(factory.ObservedUnit.Id));
        });
    }

    /// <summary>
    /// Verifies that aggregate unit tracking updates processed counts and phase ratio without manual ratio math.
    /// </summary>
    [Test]
    public async Task Host_StartAsync_PhaseProgressTracking_UpdatesProcessedUnits_AndRatio() {
        using BeltRunnerHost host = new(new HostOptions {
            InteractionBrokerFactory = static () => new InMemoryInteractionBroker()
        });

        TrackingPhaseFactory factory = new("phase/tracking");
        SequentialPlan plan = new SequentialPlanBuilder()
            .Add(factory, "Tracking Phase")
            .Build();
        PhaseKey expectedKey = new("phase/tracking");

        using IRun run = await host.StartAsync(plan, CancellationToken.None);
        await run.Completion.WaitAsync(TestTimeouts.Default);

        IPhaseSnapshot phaseSnapshot = run.Snapshot.Phases[0];
        IReadOnlyList<IUnitSnapshot> unitSnapshots = phaseSnapshot.Units;

        TestNarrative.ObserveMany(
            $"phaseKey={phaseSnapshot.PhaseKey}",
            $"processedUnits={phaseSnapshot.ProcessedUnits}",
            $"totalUnits={phaseSnapshot.TotalUnits}",
            $"phaseRatio={phaseSnapshot.Ratio:0.####}",
            $"unit0={unitSnapshots[0].Status}:{unitSnapshots[0].Ratio:0.####}",
            $"unit1={unitSnapshots[1].Status}:{unitSnapshots[1].Ratio:0.####}");

        Assert.Multiple(() => {
            Assert.That(phaseSnapshot.PhaseKey, Is.EqualTo(expectedKey));
            Assert.That(phaseSnapshot.TotalUnits, Is.EqualTo(2));
            Assert.That(phaseSnapshot.ProcessedUnits, Is.EqualTo(2));
            Assert.That(phaseSnapshot.Ratio, Is.EqualTo(1.0).Within(0.0001));
            Assert.That(unitSnapshots, Has.Count.EqualTo(2));
            Assert.That(unitSnapshots[0].Status, Is.EqualTo(UnitStatus.Succeeded));
            Assert.That(unitSnapshots[1].Status, Is.EqualTo(UnitStatus.Succeeded));
            Assert.That(unitSnapshots[0].Ratio, Is.EqualTo(1.0).Within(0.0001));
            Assert.That(unitSnapshots[1].Ratio, Is.EqualTo(1.0).Within(0.0001));
        });
    }

    private sealed class TelemetryPhaseFactory : PhaseFactoryBase {
        public TelemetryPhaseFactory(string key) : base(key) {
        }

        public PhaseKey? ObservedPhaseKey { get; set; }

        public TelemetryUnit? ObservedUnit { get; set; }

        public override IPhase Create() {
            return new TelemetryPhase(this);
        }
    }

    private sealed class TelemetryPhase : IPhase {
        private readonly TelemetryPhaseFactory factory;

        public TelemetryPhase(TelemetryPhaseFactory factory) {
            this.factory = factory ?? throw new ArgumentNullException(nameof(factory));
        }

        public string Name => "Telemetry";

        public UnitSet Units { get; } = new();

        public Task<IPhaseOutcome> ExecuteAsync(IPhaseContext context, CancellationToken ct = default) {
            TelemetryUnit unit = new("Telemetry Unit");

            this.factory.ObservedPhaseKey = context.Key;
            this.factory.ObservedUnit = unit;

            this.Units.AddAndLock(unit);
            context.Telemetry.SetTotalUnits(1);
            context.Telemetry.StartUnit(unit);
            context.Telemetry.ReportUnitProgress(unit, 0.5);
            context.Telemetry.Warn("Halfway there", unitId: unit.Id);
            context.Telemetry.CompleteUnit(unit);

            return Task.FromResult<IPhaseOutcome>(new PhaseOutcome());
        }
    }

    private sealed class TelemetryUnit : Unit<string> {
        public TelemetryUnit(string name) : base(name, name) {
        }
    }

    private sealed class TrackingPhaseFactory : PhaseFactoryBase {
        public TrackingPhaseFactory(string key) : base(key) {
        }

        public override IPhase Create() {
            return new TrackingPhase();
        }
    }

    private sealed class TrackingPhase : IPhase {
        public string Name => "Tracking";

        public UnitSet Units { get; } = new();

        public Task<IPhaseOutcome> ExecuteAsync(IPhaseContext context, CancellationToken ct = default) {
            TelemetryUnit[] units = [new("Tracked 1"), new("Tracked 2")];

            this.Units.AddRangeAndLock(units);

            using IPhaseProgressTracker tracker = context.Telemetry.BeginPhaseProgressTracking(units.Length);
            using( ITrackedUnitScope first = tracker.BeginUnit(units[0]) ) {
                first.Complete();
            }

            using( ITrackedUnitScope second = tracker.BeginUnit(units[1]) ) {
                second.Complete();
            }

            return Task.FromResult<IPhaseOutcome>(new PhaseOutcome());
        }
    }
}
