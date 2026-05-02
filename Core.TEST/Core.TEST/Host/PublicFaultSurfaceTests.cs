using BeltRunner.Core.Execution;
using BeltRunner.Core.Execution.Event;
using BeltRunner.Core.Execution.Interaction;
using BeltRunner.Core.Execution.Outcome;
using BeltRunner.Core.Host;
using BeltRunner.Core.Phase;
using BeltRunner.Core.Plan;
using BeltRunner.Core.TEST.Testing;

namespace BeltRunner.Core.TEST.Host;

/// <summary>
/// Verifies that public fault surfaces expose sanitized fault summaries instead of raw exception objects.
/// </summary>
/// <remarks>
/// <para>Purpose: Protect public runtime contracts from leaking raw exception instances.</para>
/// <para>Why this matters: Stack traces, exception data, and framework-specific exception graphs can reveal internal details.</para>
/// <para>Expected result: Public diagnostics and fault events expose <see cref="PublicFaultInfo"/> values, while completion reports a faulted <see cref="RunOutcome"/>.</para>
/// </remarks>
[TestFixture]
[TestOf(typeof(IDiagnosticEntry))]
[TestOf(typeof(PhaseFaultedEvent))]
[TestOf(typeof(RunFaultedEvent))]
[TestOf(typeof(HostFaultedEvent))]
[TestOf(typeof(IPhaseContext))]
public sealed class PublicFaultSurfaceTests {
    /// <summary>
    /// Verifies that the public fault projection types no longer expose raw exception details.
    /// </summary>
    /// <remarks>
    /// <para>Purpose: Lock down the shape of the public fault contract exposed to callers.</para>
    /// <para>Why this matters: Public surfaces should not leak framework exception objects or internal details.</para>
    /// <para>Expected result: The public fault surfaces expose <see cref="PublicFaultInfo"/> and do not expose raw exception properties.</para>
    /// </remarks>
    [Test]
    public void PublicFaultTypes_DoNotExposeRawExceptionDetails() {
        TestNarrative.ObserveMany(
            $"diagnosticExceptionProperty={(typeof(IDiagnosticEntry).GetProperty("Exception") is null ? "missing" : "present")}",
            $"phaseFaultInfoType={typeof(PhaseFaultedEvent).GetProperty("FaultInfo")?.PropertyType?.Name}",
            $"runFaultInfoType={typeof(RunFaultedEvent).GetProperty("FaultInfo")?.PropertyType?.Name}",
            $"hostFaultInfoType={typeof(HostFaultedEvent).GetProperty("FaultInfo")?.PropertyType?.Name}");
        Assert.Multiple(() => {
            Assert.That(typeof(IDiagnosticEntry).GetProperty("Exception"), Is.Null);
            Assert.That(typeof(PhaseFaultedEvent).GetProperty("Exception"), Is.Null);
            Assert.That(typeof(RunFaultedEvent).GetProperty("Exception"), Is.Null);
            Assert.That(typeof(HostFaultedEvent).GetProperty("Exception"), Is.Null);
            Assert.That(typeof(IDiagnosticEntry).GetProperty("FaultInfo")?.PropertyType, Is.EqualTo(typeof(PublicFaultInfo)));
            Assert.That(typeof(PhaseFaultedEvent).GetProperty("FaultInfo")?.PropertyType, Is.EqualTo(typeof(PublicFaultInfo)));
            Assert.That(typeof(RunFaultedEvent).GetProperty("FaultInfo")?.PropertyType, Is.EqualTo(typeof(PublicFaultInfo)));
            Assert.That(typeof(HostFaultedEvent).GetProperty("FaultInfo")?.PropertyType, Is.EqualTo(typeof(PublicFaultInfo)));
        });
    }

    /// <summary>
    /// Verifies that phase context exposes only the narrowed interaction and cancellation surface.
    /// </summary>
    /// <remarks>
    /// <para>Purpose: Confirm that phase implementations receive only the intended execution surface.</para>
    /// <para>Why this matters: Narrow public context contracts reduce accidental coupling to host internals.</para>
    /// <para>Expected result: <see cref="IPhaseContext"/> does not expose a run object and only exposes the narrowed interaction and cancellation members.</para>
    /// </remarks>
    [Test]
    public void IPhaseContext_ExposesNarrowedExecutionSurface() {
        TestNarrative.ObserveMany(
            $"runProperty={(typeof(IPhaseContext).GetProperty("Run") is null ? "missing" : "present")}",
            $"cancellationTokenType={typeof(IPhaseContext).GetProperty("CancellationToken")?.PropertyType?.Name}",
            $"interactionType={typeof(IPhaseContext).GetProperty("Interaction")?.PropertyType?.Name}");
        Assert.Multiple(() => {
            Assert.That(typeof(IPhaseContext).GetProperty("Run"), Is.Null);
            Assert.That(typeof(IPhaseContext).GetProperty("CancellationToken")?.PropertyType, Is.EqualTo(typeof(CancellationToken)));
            Assert.That(typeof(IPhaseContext).GetProperty("Interaction")?.PropertyType, Is.EqualTo(typeof(IInteractionRequester)));
        });
    }

    /// <summary>
    /// Verifies that fault diagnostics and events expose sanitized fault summaries after a phase throws.
    /// </summary>
    /// <remarks>
    /// <para>Purpose: Confirm that runtime fault reporting projects sanitized public fault information after execution failures.</para>
    /// <para>Why this matters: Consumers need actionable fault summaries without seeing raw exception internals.</para>
    /// <para>Expected result: Completion, diagnostics, run events, and host fault events all expose the expected <see cref="PublicFaultInfo"/> values.</para>
    /// </remarks>
    [Test]
    public async Task Host_StartAsync_WhenPhaseThrows_ExposesSanitizedPublicFaultInfo() {
        using BeltRunner.Core.Host.Host host = new(new HostOptions {
            InteractionBrokerFactory = static () => new InMemoryInteractionBroker()
        });
        using ObservableRecorder<HostFaultedEvent> faultRecorder = new(host.Faults);

        SequentialPlan plan = CreatePlan((_, _) => throw new InvalidOperationException("boom"));

        using IRun run = await host.StartAsync(plan, CancellationToken.None);
        RunOutcome completionOutcome = await run.Completion.WaitAsync(TestTimeouts.Default);
        await WaitUntilAsync(() => faultRecorder.Items.Count == 1);
        HostFaultedEvent hostFault = faultRecorder.Items.Single();
        PhaseFaultedEvent phaseFault = run.EventLog.OfType<PhaseFaultedEvent>().Single();
        RunFaultedEvent runFault = run.EventLog.OfType<RunFaultedEvent>().Single();
        IDiagnosticEntry diagnostic = run.DiagnosticLog.Single();
        TestNarrative.ObserveMany(
            $"completionOutcomeKind={completionOutcome.Kind}",
            $"hostFaultKind={hostFault.FaultInfo.FaultKind}",
            $"phaseFaultMessage={phaseFault.FaultInfo.PublicMessage}",
            $"diagnosticMessage={diagnostic.Message}");

        Assert.Multiple(() => {
            Assert.That(completionOutcome.Kind, Is.EqualTo(RunOutcomeKind.Faulted));
            Assert.That(completionOutcome.FaultInfo, Is.Not.Null);
            AssertPublicFaultInfo(completionOutcome.FaultInfo!, "InvalidOperationException", "The run failed with an unhandled exception.", "run");
            AssertPublicFaultInfo(phaseFault.FaultInfo, "InvalidOperationException", "The phase failed with an unhandled exception.", "phase:phase/a");
            AssertPublicFaultInfo(runFault.FaultInfo, "InvalidOperationException", "The run failed with an unhandled exception.", "run");
            AssertPublicFaultInfo(hostFault.FaultInfo, "InvalidOperationException", "The run failed with an unhandled exception.", "run");
            Assert.That(diagnostic.FaultInfo, Is.Not.Null);
            AssertPublicFaultInfo(diagnostic.FaultInfo!, "InvalidOperationException", "The phase failed with an unhandled exception.", "phase:phase/a");
            Assert.That(diagnostic.Message, Is.EqualTo("The phase failed with an unhandled exception."));
        });
    }

    /// <summary>
    /// Verifies that the run event stream completes instead of faulting when execution fails.
    /// </summary>
    /// <remarks>
    /// <para>Purpose: Protect event-stream terminal semantics when execution faults.</para>
    /// <para>Why this matters: Observers should see a completed stream with fault events instead of an observable transport failure.</para>
    /// <para>Expected result: The event stream completes successfully, reports no observable error, and contains a single <see cref="RunFaultedEvent"/>.</para>
    /// </remarks>
    [Test]
    public async Task Host_StartAsync_WhenPhaseThrows_EventStreamCompletesWithoutObservableError() {
        using BeltRunner.Core.Host.Host host = new(new HostOptions {
            InteractionBrokerFactory = static () => new InMemoryInteractionBroker()
        });

        SequentialPlan plan = CreatePlan((_, _) => throw new InvalidOperationException("boom"));

        using IRun run = await host.StartAsync(plan, CancellationToken.None);
        using ObservableRecorder<RunEvent> recorder = new(run.EventStream);

        await run.Completion.WaitAsync(TestTimeouts.Default);
        TerminalSignal terminal = await recorder.WaitForTerminalAsync(TestTimeouts.Default);
        int runFaultedEventCount = recorder.Items.OfType<RunFaultedEvent>().Count();
        TestNarrative.ObserveMany(
            $"terminal={terminal}",
            $"error={(recorder.Error is null ? "null" : recorder.Error.GetType().Name)}",
            $"runFaultedEventCount={runFaultedEventCount}");

        Assert.Multiple(() => {
            Assert.That(terminal, Is.EqualTo(TerminalSignal.Completed));
            Assert.That(recorder.Error, Is.Null);
            Assert.That(runFaultedEventCount, Is.EqualTo(1));
        });
    }

    private static void AssertPublicFaultInfo(PublicFaultInfo info, string expectedKind, string expectedMessage, string expectedOrigin) {
        Assert.Multiple(() => {
            Assert.That(info.FaultKind, Is.EqualTo(expectedKind));
            Assert.That(info.PublicMessage, Is.EqualTo(expectedMessage));
            Assert.That(info.Origin, Is.EqualTo(expectedOrigin));
            Assert.That(info.ErrorCode, Is.Null);
            Assert.That(info.OccurredAt, Is.Not.EqualTo(default(DateTimeOffset)));
        });
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

    private static SequentialPlan CreatePlan(Func<ThrowingPhase, IPhaseContext, IPhaseOutcome> onExecute) {
        ThrowingPhaseFactory factory = new("phase/a", onExecute);
        return new SequentialPlanBuilder()
            .Add(factory, "Phase A")
            .Build();
    }

    private sealed class ThrowingPhaseFactory : PhaseFactoryBase {
        private readonly Func<ThrowingPhase, IPhaseContext, IPhaseOutcome> onExecute;

        public ThrowingPhaseFactory(string key, Func<ThrowingPhase, IPhaseContext, IPhaseOutcome> onExecute) : base(key) {
            this.onExecute = onExecute ?? throw new ArgumentNullException(nameof(onExecute));
        }

        public override IPhase Create() {
            return new ThrowingPhase(this.onExecute);
        }
    }

    private sealed class ThrowingPhase : IPhase {
        private readonly Func<ThrowingPhase, IPhaseContext, IPhaseOutcome> onExecute;

        public ThrowingPhase(Func<ThrowingPhase, IPhaseContext, IPhaseOutcome> onExecute) {
            this.onExecute = onExecute ?? throw new ArgumentNullException(nameof(onExecute));
        }

        public string Name => "Throwing";

        public UnitSet Units { get; } = new();

        public Task<IPhaseOutcome> ExecuteAsync(IPhaseContext context, CancellationToken ct = default) {
            return Task.FromResult(this.onExecute(this, context));
        }
    }
}
