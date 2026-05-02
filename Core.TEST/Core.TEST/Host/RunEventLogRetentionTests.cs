using BeltRunner.Core.Execution;
using BeltRunner.Core.Execution.Event;
using BeltRunner.Core.Execution.Interaction;
using BeltRunner.Core.Host;
using BeltRunner.Core.Phase;
using BeltRunner.Core.Plan;
using BeltRunner.Core.TEST.Testing;

namespace BeltRunner.Core.TEST.Host;

/// <summary>
/// Verifies configurable retention behavior for the run event log.
/// </summary>
/// <remarks>
/// <para>Purpose: Confirm that the host can cap in-memory run history without changing the default behavior.</para>
/// <para>Why this matters: Unbounded run history makes late-subscriber replay and retained event logs vulnerable to memory growth.</para>
/// <para>Expected result: Null keeps all events, a positive limit keeps only the newest events, and invalid limits are rejected.</para>
/// </remarks>
[TestFixture]
[TestOf(typeof(HostOptions))]
[TestOf(typeof(Run))]
public sealed class RunEventLogRetentionTests {
    /// <summary>
    /// Verifies that the default host configuration keeps the full run event history.
    /// </summary>
    [Test]
    public async Task Host_StartAsync_WithoutRetentionLimit_RetainsFullEventLog() {
        using BeltRunner.Core.Host.Host host = CreateHost(new HostOptions {
            InteractionBrokerFactory = static () => new InMemoryInteractionBroker()
        });

        using IRun run = await host.StartAsync(CreatePlan(2), CancellationToken.None);
        await run.Completion.WaitAsync(TestTimeouts.Default);
        Type[] retainedTypes = run.EventLog.Select(x => x.GetType()).ToArray();
        TestNarrative.Observe($"retainedEventTypes={string.Join(", ", retainedTypes.Select(x => x.Name))}");

        Assert.That(retainedTypes, Is.EqualTo(new[] {
            typeof(RunStartedEvent),
            typeof(PhaseStartedEvent),
            typeof(PhaseCompletedEvent),
            typeof(PhaseStartedEvent),
            typeof(PhaseCompletedEvent),
            typeof(RunCompletedEvent)
        }));
    }

    /// <summary>
    /// Verifies that a configured retention limit keeps only the newest event log entries and replay events.
    /// </summary>
    [Test]
    public async Task Host_StartAsync_WithRetentionLimit_RetainsNewestEventsOnly() {
        using BeltRunner.Core.Host.Host host = CreateHost(new HostOptions {
            InteractionBrokerFactory = static () => new InMemoryInteractionBroker(),
            RunEventLogMaxRetainedCount = 3
        });

        using IRun run = await host.StartAsync(CreatePlan(2), CancellationToken.None);
        await run.Completion.WaitAsync(TestTimeouts.Default);

        using ObservableRecorder<RunEvent> recorder = new(run.EventStream);
        TerminalSignal terminal = await recorder.WaitForTerminalAsync(TestTimeouts.Default);

        Type[] retainedTypes = run.EventLog.Select(x => x.GetType()).ToArray();
        Type[] replayedTypes = recorder.Items.Select(x => x.GetType()).ToArray();
        TestNarrative.ObserveMany(
            $"terminal={terminal}",
            $"retainedEventTypes={string.Join(", ", retainedTypes.Select(x => x.Name))}",
            $"replayedEventTypes={string.Join(", ", replayedTypes.Select(x => x.Name))}");

        Assert.Multiple(() => {
            Assert.That(terminal, Is.EqualTo(TerminalSignal.Completed));
            Assert.That(retainedTypes, Is.EqualTo(new[] {
                typeof(PhaseStartedEvent),
                typeof(PhaseCompletedEvent),
                typeof(RunCompletedEvent)
            }));
            Assert.That(replayedTypes, Is.EqualTo(retainedTypes));
        });
    }

    /// <summary>
    /// Verifies that non-positive retention limits are rejected.
    /// </summary>
    [Test]
    public void HostOptions_RunEventLogMaxRetainedCount_WithNonPositiveValue_Throws() {
        HostOptions options = new();

        ArgumentOutOfRangeException ex = Assert.Throws<ArgumentOutOfRangeException>(() => options.RunEventLogMaxRetainedCount = 0)!;
        TestNarrative.Observe($"setter rejected non-positive value with paramName={ex.ParamName}");

        Assert.That(ex.ParamName, Is.EqualTo("value"));
    }

    private static BeltRunner.Core.Host.Host CreateHost(HostOptions options) {
        return new BeltRunner.Core.Host.Host(options);
    }

    private static SequentialPlan CreatePlan(int phaseCount) {
        SequentialPlanBuilder builder = new();
        foreach( int index in Enumerable.Range(0, phaseCount) ) {
            builder.Add(new CompletedPhaseFactory($"phase/{index}"), $"Phase {index}");
        }

        return builder.Build();
    }

    private sealed class CompletedPhaseFactory : PhaseFactoryBase {
        public CompletedPhaseFactory(string key) : base(key) {
        }

        public override IPhase Create() {
            return new CompletedPhase();
        }
    }

    private sealed class CompletedPhase : IPhase {
        public string Name => "Completed";

        public UnitSet Units { get; } = new();

        public Task<IPhaseOutcome> ExecuteAsync(IPhaseContext context, CancellationToken ct = default) {
            return Task.FromResult<IPhaseOutcome>(new PhaseOutcome());
        }
    }
}
