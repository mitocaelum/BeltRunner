using System.Reflection;
using BeltRunner.Core.Execution;
using BeltRunner.Core.Execution.Event;
using BeltRunner.Core.Execution.Interaction;
using BeltRunner.Core.Phase;
using BeltRunner.Core.Plan;
using BeltRunner.Core.Plan.Artifacts;
using BeltRunner.Core.TEST.Testing;
using BeltRunner.Core.Units;

namespace BeltRunner.Core.TEST.Execution;

/// <summary>
/// Verifies how <see cref="Run"/> materializes runtime state into snapshots.
/// </summary>
/// <remarks>
/// <para>Purpose: Protect the observable snapshot model that consumers use to render run progress and runtime state.</para>
/// <para>Why this matters: Snapshots are a high-level integration surface, so subtle drift in status, unit counts, or interaction tracking can break tooling without obvious compiler errors.</para>
/// <para>Expected result: The snapshot graph stays synchronized with attached phases, unit telemetry, interaction state, and seeded artifacts.</para>
/// </remarks>
[TestFixture]
[TestOf(typeof(Run))]
public sealed class RunSnapshotTests {
    /// <summary>
    /// Verifies that the latest snapshot replays the most recent unit telemetry state.
    /// </summary>
    /// <remarks>
    /// <para>Purpose: Confirm that telemetry updates are reflected in the replayable snapshot stream.</para>
    /// <para>Why this matters: Consumers often subscribe after execution has started and still need an accurate current state.</para>
    /// <para>Expected result: The latest replayed snapshot shows the running run, running phase, updated unit progress, and matching runtime unit state.</para>
    /// </remarks>
    [Test]
    public void Snapshots_Replay_Latest_UnitTelemetryState() {
        InMemoryInteractionBroker broker = new();
        Run run = new(broker);
        SequentialPlan plan = CreatePlan();
        PhaseKey phaseKey = new("phase/a");
        RuntimeTestUnit unit = new("Unit A");
        NoOpPhase phase = new();

        phase.Units.AddAndLock(unit);

        run.InitializeRuntimeState(plan.Steps);
        run.AttachPhase(phaseKey, phase);
        run.Publish(new RunStartedEvent(run.Id));
        run.Publish(new PhaseStartedEvent(run.Id, phaseKey, 0));
        run.SetPhaseTotalUnits(phaseKey, 1);
        run.SetUnitStatus(phaseKey, unit.Id, UnitStatus.Running);
        run.SetUnitProgress(phaseKey, unit.Id, 0.61);

        using ObservableRecorder<IRunSnapshot> recorder = new(run.SnapshotStream);

        Assert.That(recorder.Items, Is.Not.Empty);

        IRunSnapshot snapshot = recorder.Items[^1];
        IPhaseSnapshot phaseSnapshot = snapshot.Phases[0];
        IUnitSnapshot unitSnapshot = phaseSnapshot.Units[0];
        TestNarrative.ObserveMany(
            $"snapshotStatus={snapshot.Status}",
            $"currentPhaseKey={snapshot.CurrentPhaseKey}",
            $"phaseStatus={phaseSnapshot.Status}",
            $"unitName={unitSnapshot.Name}",
            $"unitStatus={unitSnapshot.Status}",
            $"unitRatio={unitSnapshot.Ratio:0.####}");

        Assert.Multiple(() => {
            Assert.That(snapshot.Status, Is.EqualTo(RunStatus.Running));
            Assert.That(snapshot.CurrentPhaseKey, Is.EqualTo(phaseKey));
            Assert.That(phaseSnapshot.Status, Is.EqualTo(PhaseStatus.Running));
            Assert.That(phaseSnapshot.TotalUnits, Is.EqualTo(1));
            Assert.That(unitSnapshot.Id, Is.EqualTo(unit.Id));
            Assert.That(unitSnapshot.Name, Is.EqualTo("Unit A"));
            Assert.That(unitSnapshot.Status, Is.EqualTo(UnitStatus.Running));
            Assert.That(unitSnapshot.Ratio, Is.EqualTo(0.61).Within(0.0001));
            Assert.That(unit.Status, Is.EqualTo(UnitStatus.Running));
            Assert.That(unit.CurrentPhaseKey, Is.EqualTo(phaseKey));
        });
    }

    /// <summary>
    /// Verifies that a phase snapshot includes mixed runtime unit types from the phase-owned unit collection.
    /// </summary>
    /// <remarks>
    /// <para>Purpose: Protect snapshot population when a phase exposes more than one concrete unit implementation.</para>
    /// <para>Why this matters: Snapshot projection must depend on the unit contract, not on a single concrete unit type.</para>
    /// <para>Expected result: The phase snapshot includes both units and associates each unit with the attached phase key.</para>
    /// </remarks>
    [Test]
    public void Snapshots_Include_MixedUnitTypes_FromPhaseOwnedCollection() {
        InMemoryInteractionBroker broker = new();
        Run run = new(broker);
        SequentialPlan plan = CreatePlan();
        PhaseKey phaseKey = new("phase/a");
        MixedPhase phase = new();
        RuntimeTestUnit primary = new("Primary");
        SecondaryRuntimeUnit secondary = new("Secondary");

        phase.Units.Add(primary);
        phase.Units.Add(secondary);
        phase.Units.Lock();

        run.InitializeRuntimeState(plan.Steps);
        run.AttachPhase(phaseKey, phase);
        run.Publish(new RunStartedEvent(run.Id));
        run.Publish(new PhaseStartedEvent(run.Id, phaseKey, 0));

        IPhaseSnapshot snapshot = run.Snapshot.Phases[0];
        TestNarrative.ObserveMany(
            $"unitCount={snapshot.Units.Count}",
            $"unitNames={string.Join(", ", snapshot.Units.Select(unit => unit.Name))}",
            $"primaryPhaseKey={primary.CurrentPhaseKey}",
            $"secondaryPhaseKey={secondary.CurrentPhaseKey}");

        Assert.Multiple(() => {
            Assert.That(snapshot.Units, Has.Count.EqualTo(2));
            Assert.That(snapshot.Units.Select(unit => unit.Name), Is.EquivalentTo(new[] { "Primary", "Secondary" }));
            Assert.That(primary.CurrentPhaseKey, Is.EqualTo(phaseKey));
            Assert.That(secondary.CurrentPhaseKey, Is.EqualTo(phaseKey));
        });
    }

    /// <summary>
    /// Verifies that attaching a phase updates the snapshot immediately.
    /// </summary>
    /// <remarks>
    /// <para>Purpose: Define the attachment contract for newly created runtime state.</para>
    /// <para>Why this matters: Snapshot consumers should not have to wait for later execution events to discover attached units.</para>
    /// <para>Expected result: The snapshot contains the attached phase and its unit as soon as the phase is attached.</para>
    /// </remarks>
    [Test]
    public void Snapshot_Reflects_AttachedPhase_Immediately() {
        InMemoryInteractionBroker broker = new();
        Run run = new(broker);
        SequentialPlan plan = CreatePlan();
        PhaseKey phaseKey = new("phase/a");
        NoOpPhase phase = new();
        RuntimeTestUnit unit = new("Attached");

        phase.Units.Add(unit);

        run.InitializeRuntimeState(plan.Steps);
        run.AttachPhase(phaseKey, phase);

        IPhaseSnapshot snapshot = run.Snapshot.Phases[0];
        TestNarrative.ObserveMany(
            $"phaseKey={snapshot.PhaseKey}",
            $"unitCount={snapshot.Units.Count}",
            $"firstUnitName={snapshot.Units[0].Name}");

        Assert.Multiple(() => {
            Assert.That(snapshot.PhaseKey, Is.EqualTo(phaseKey));
            Assert.That(snapshot.Units, Has.Count.EqualTo(1));
            Assert.That(snapshot.Units[0].Id, Is.EqualTo(unit.Id));
            Assert.That(snapshot.Units[0].Name, Is.EqualTo("Attached"));
        });
    }

    /// <summary>
    /// Verifies that discovering new units during execution increases the total unit count in snapshots.
    /// </summary>
    /// <remarks>
    /// <para>Purpose: Protect dynamic unit discovery behavior in long-running phases.</para>
    /// <para>Why this matters: Progress reporting becomes misleading if total work does not expand when additional units are introduced.</para>
    /// <para>Expected result: Snapshot totals, processed counts, and ratios update when a new unit is added and again when it completes.</para>
    /// </remarks>
    [Test]
    public void Snapshots_Grow_TotalUnits_When_NewUnits_AreDiscovered_DuringExecution() {
        InMemoryInteractionBroker broker = new();
        Run run = new(broker);
        SequentialPlan plan = CreatePlan();
        PhaseKey phaseKey = new("phase/a");
        NoOpPhase phase = new();
        RuntimeTestUnit first = new("First");
        SecondaryRuntimeUnit second = new("Second");

        phase.Units.Add(first);

        run.InitializeRuntimeState(plan.Steps);
        run.AttachPhase(phaseKey, phase);
        run.Publish(new RunStartedEvent(run.Id));
        run.Publish(new PhaseStartedEvent(run.Id, phaseKey, 0));
        run.SetPhaseTotalUnits(phaseKey, 1);
        run.SetUnitProgress(phaseKey, first.Id, 1.0);
        run.SetUnitStatus(phaseKey, first.Id, UnitStatus.Succeeded);

        phase.Units.Add(second);

        IPhaseSnapshot afterDiscovery = run.Snapshot.Phases[0];
        TestNarrative.ObserveMany(
            $"afterDiscovery totalUnits={afterDiscovery.TotalUnits}",
            $"afterDiscovery processedUnits={afterDiscovery.ProcessedUnits}",
            $"afterDiscovery ratio={afterDiscovery.Ratio:0.####}",
            $"afterDiscovery unitCount={afterDiscovery.Units.Count}");

        Assert.Multiple(() => {
            Assert.That(afterDiscovery.Units, Has.Count.EqualTo(2));
            Assert.That(afterDiscovery.TotalUnits, Is.EqualTo(2));
            Assert.That(afterDiscovery.ProcessedUnits, Is.EqualTo(1));
            Assert.That(afterDiscovery.Ratio, Is.EqualTo(0.5).Within(0.0001));
            Assert.That(second.CurrentPhaseKey, Is.EqualTo(phaseKey));
        });

        run.SetUnitProgress(phaseKey, second.Id, 1.0);
        run.SetUnitStatus(phaseKey, second.Id, UnitStatus.Succeeded);

        IPhaseSnapshot completedDiscovery = run.Snapshot.Phases[0];
        TestNarrative.ObserveMany(
            $"completedDiscovery totalUnits={completedDiscovery.TotalUnits}",
            $"completedDiscovery processedUnits={completedDiscovery.ProcessedUnits}",
            $"completedDiscovery ratio={completedDiscovery.Ratio:0.####}");

        Assert.Multiple(() => {
            Assert.That(completedDiscovery.TotalUnits, Is.EqualTo(2));
            Assert.That(completedDiscovery.ProcessedUnits, Is.EqualTo(2));
            Assert.That(completedDiscovery.Ratio, Is.EqualTo(1.0).Within(0.0001));
        });
    }

    /// <summary>
    /// Verifies that <see cref="UnitSet"/> does not expose public removal operations.
    /// </summary>
    /// <remarks>
    /// <para>Purpose: Lock down the intended write model for runtime unit collections.</para>
    /// <para>Why this matters: Allowing arbitrary removal would make snapshot and progress semantics much harder to reason about.</para>
    /// <para>Expected result: Reflection does not find public remove or clear members on <see cref="UnitSet"/>.</para>
    /// </remarks>
    [Test]
    public void UnitSet_PublicApi_DoesNotExposeRemovalOperations() {
        MethodInfo? removeById = typeof(UnitSet).GetMethod("Remove", new[] { typeof(Guid) });
        MethodInfo? removeByUnit = typeof(UnitSet).GetMethod("Remove", new[] { typeof(IUnit) });
        MethodInfo? clear = typeof(UnitSet).GetMethod("Clear", Type.EmptyTypes);
        TestNarrative.ObserveMany(
            $"removeById={(removeById is null ? "missing" : removeById.Name)}",
            $"removeByUnit={(removeByUnit is null ? "missing" : removeByUnit.Name)}",
            $"clear={(clear is null ? "missing" : clear.Name)}");

        Assert.Multiple(() => {
            Assert.That(removeById, Is.Null);
            Assert.That(removeByUnit, Is.Null);
            Assert.That(clear, Is.Null);
        });
    }

    /// <summary>
    /// Verifies that active interaction requests appear on the run surface and are removed after resolution.
    /// </summary>
    /// <remarks>
    /// <para>Purpose: Ensure that interactive runtime state is visible through the run surface.</para>
    /// <para>Why this matters: User interfaces and automation tools need to detect pending prompts without inspecting broker internals.</para>
    /// <para>Expected result: The interaction appears with its metadata while pending and disappears after a response is recorded.</para>
    /// </remarks>
    [Test]
    public async Task Run_Tracks_ActiveInteractions() {
        InMemoryInteractionBroker broker = new();
        Run run = new(broker);
        SequentialPlan plan = CreatePlan();
        PhaseKey phaseKey = new("phase/a");
        InteractionRequest<bool> request = new("confirm", phaseKey, title: "Approve", message: "Continue?");

        run.InitializeRuntimeState(plan.Steps);
        Task<bool> pending = broker.AskAsync(request, CancellationToken.None);

        await WaitUntilAsync(() => run.ActiveInteractions.Count == 1);

        IInteractionSnapshot interaction = run.ActiveInteractions[0];
        TestNarrative.ObserveMany(
            $"pendingInteractionCount={run.ActiveInteractions.Count}",
            $"interactionKind={interaction.Kind}",
            $"interactionTitle={interaction.Title}",
            $"interactionMessage={interaction.Message}");
        Assert.Multiple(() => {
            Assert.That(interaction.RequestId, Is.EqualTo(request.RequestId));
            Assert.That(interaction.Kind, Is.EqualTo("confirm"));
            Assert.That(interaction.Title, Is.EqualTo("Approve"));
            Assert.That(interaction.Message, Is.EqualTo("Continue?"));
            Assert.That(interaction.PhaseKey, Is.EqualTo(phaseKey));
        });

        Assert.That(broker.TryRespond(request.RequestId, true), Is.True);
        bool response = await pending.WaitAsync(TestTimeouts.Default);
        Assert.That(response, Is.True);

        await WaitUntilAsync(() => run.ActiveInteractions.Count == 0);
        TestNarrative.ObserveMany(
            $"response={response}",
            $"remainingInteractionCount={run.ActiveInteractions.Count}");
        Assert.That(run.ActiveInteractions, Is.Empty);
    }

    /// <summary>
    /// Verifies that seeded artifacts are exposed through the run artifact store.
    /// </summary>
    /// <remarks>
    /// <para>Purpose: Protect artifact seeding as part of initial run setup.</para>
    /// <para>Why this matters: Downstream phases and lifecycle hooks rely on seeded values being visible before execution starts.</para>
    /// <para>Expected result: The seeded artifact can be detected and retrieved from <see cref="Run.Artifacts"/>.</para>
    /// </remarks>
    [Test]
    public void Artifacts_Property_Exposes_Seeded_Artifacts() {
        Run run = new(new InMemoryInteractionBroker());
        ArtifactKey<string> key = ArtifactSeeds.Key<string>("seed");

        run.SeedInitialArtifacts([new ProducedArtifact<string>(key, "value")]);
        TestNarrative.ObserveMany(
            $"containsSeed={run.Artifacts.Contains(key)}",
            $"seedValue={run.Artifacts.GetRequired(key)}");

        Assert.Multiple(() => {
            Assert.That(run.Artifacts.Contains(key), Is.True);
            Assert.That(run.Artifacts.GetRequired(key), Is.EqualTo("value"));
        });
    }

    private static SequentialPlan CreatePlan() {
        TestPhaseFactory factory = new("phase/a");
        return new SequentialPlanBuilder()
            .Add(factory, "Phase A")
            .Build();
    }

    private static async Task WaitUntilAsync(Func<bool> condition) {
        DateTimeOffset deadline = DateTimeOffset.UtcNow.Add(TestTimeouts.Default);

        while( DateTimeOffset.UtcNow < deadline ) {
            if( condition() ) {
                return;
            }

            await Task.Delay(20);
        }

        Assert.Fail("Timed out while waiting for the expected state.");
    }

    private sealed class TestPhaseFactory : PhaseFactoryBase {
        public TestPhaseFactory(string key) : base(key) {
        }

        public override IPhase Create() {
            return new NoOpPhase();
        }
    }

    private sealed class NoOpPhase : IPhase {
        public string Name => "No Op";

        public UnitSet Units { get; } = new();

        public Task<IPhaseOutcome> ExecuteAsync(IPhaseContext context, CancellationToken ct = default) {
            return Task.FromResult<IPhaseOutcome>(new PhaseOutcome());
        }
    }

    private sealed class MixedPhase : IPhase {
        public string Name => "Mixed";

        public UnitSet Units { get; } = new();

        public Task<IPhaseOutcome> ExecuteAsync(IPhaseContext context, CancellationToken ct = default) {
            return Task.FromResult<IPhaseOutcome>(new PhaseOutcome());
        }
    }

    private sealed class RuntimeTestUnit : Unit<string> {
        public RuntimeTestUnit(string name) : base(name, name) {
        }
    }

    private sealed class SecondaryRuntimeUnit : Unit<int> {
        public SecondaryRuntimeUnit(string name) : base(1, name) {
        }
    }
}
