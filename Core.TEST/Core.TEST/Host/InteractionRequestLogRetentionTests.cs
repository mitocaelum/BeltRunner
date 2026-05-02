using BeltRunner.Core.Execution;
using BeltRunner.Core.Execution.Interaction;
using BeltRunner.Core.Host;
using BeltRunner.Core.Phase;
using BeltRunner.Core.Plan;
using BeltRunner.Core.TEST.Testing;
using BeltRunnerHost = BeltRunner.Core.Host.Host;

namespace BeltRunner.Core.TEST.Host;

/// <summary>
/// Verifies configurable retention behavior for the interaction request log.
/// </summary>
/// <remarks>
/// <para>Purpose: Confirm that host options can cap in-memory interaction request history and active pending requests without changing the default behavior.</para>
/// <para>Why this matters: Unbounded retained or pending requests make replay, active-request notifications, and runtime interaction state vulnerable to memory growth.</para>
/// <para>Expected result: Positive limits are applied to the in-memory broker, pending overflow is rejected, and invalid limits are rejected.</para>
/// </remarks>
[TestFixture]
[TestOf(typeof(HostOptions))]
[TestOf(typeof(InMemoryInteractionBroker))]
[TestOf(typeof(BeltRunnerHost))]
public sealed class InteractionRequestLogRetentionTests {
    /// <summary>
    /// Verifies that the default host configuration keeps the full interaction request history.
    /// </summary>
    /// <remarks>
    /// <para>Purpose: Define the default request-log retention behavior when no explicit cap is configured.</para>
    /// <para>Why this matters: Request history should remain fully replayable unless the host is explicitly configured to trim it.</para>
    /// <para>Expected result: The broker retains every interaction request title in order.</para>
    /// </remarks>
    [Test]
    public async Task Host_StartAsync_WithoutInteractionRequestRetentionLimit_RetainsFullRequestLog() {
        using BeltRunnerHost host = CreateHost(new HostOptions {
            InteractionBrokerFactory = static () => new InMemoryInteractionBroker()
        });

        using IRun run = await host.StartAsync(CreatePlan(4), CancellationToken.None);
        InMemoryInteractionBroker broker = (InMemoryInteractionBroker)run.Interaction;

        await ResolveRequestsAsync(broker, 4);
        await run.Completion.WaitAsync(TestTimeouts.Default);
        string[] retainedTitles = broker.RequestLog.Select(x => x.Title).ToArray();
        TestNarrative.Observe($"retainedRequestTitles={string.Join(", ", retainedTitles)}");

        Assert.That(
            retainedTitles,
            Is.EqualTo(new[] {"Approve 0", "Approve 1", "Approve 2", "Approve 3"}));
    }

    /// <summary>
    /// Verifies that a configured retention limit keeps only the newest request log entries and replay requests.
    /// </summary>
    /// <remarks>
    /// <para>Purpose: Confirm that request-log retention limits trim older entries while preserving replay for the newest requests.</para>
    /// <para>Why this matters: Interaction-heavy runs should not retain unbounded request history in memory.</para>
    /// <para>Expected result: The broker and replay stream expose only the newest configured request titles.</para>
    /// </remarks>
    [Test]
    public async Task Host_StartAsync_WithInteractionRequestRetentionLimit_RetainsNewestRequestsOnly() {
        using BeltRunnerHost host = CreateHost(new HostOptions {
            InteractionBrokerFactory = static () => new InMemoryInteractionBroker(),
            InteractionRequestLogMaxRetainedCount = 3
        });

        using IRun run = await host.StartAsync(CreatePlan(5), CancellationToken.None);
        InMemoryInteractionBroker broker = (InMemoryInteractionBroker)run.Interaction;

        await ResolveRequestsAsync(broker, 5);
        await run.Completion.WaitAsync(TestTimeouts.Default);

        using ObservableRecorder<IInteractionRequest> recorder = new(broker.Requests);
        await WaitUntilAsync(() => recorder.Items.Count == 3);

        string[] retainedTitles = broker.RequestLog.Select(x => x.Title).ToArray();
        string[] replayedTitles = recorder.Items.Select(x => x.Title).ToArray();
        TestNarrative.ObserveMany(
            $"requestLogMaxRetainedCount={broker.RequestLogMaxRetainedCount}",
            $"retainedRequestTitles={string.Join(", ", retainedTitles)}",
            $"replayedRequestTitles={string.Join(", ", replayedTitles)}");

        Assert.Multiple(() => {
            Assert.That(broker.RequestLogMaxRetainedCount, Is.EqualTo(3));
            Assert.That(retainedTitles, Is.EqualTo(new[] {"Approve 2", "Approve 3", "Approve 4"}));
            Assert.That(replayedTitles, Is.EqualTo(retainedTitles));
        });
    }

    /// <summary>
    /// Verifies that the default host configuration applies the default pending request cap to the in-memory broker.
    /// </summary>
    /// <remarks>
    /// <para>Purpose: Define the default pending-request cap applied by host startup.</para>
    /// <para>Why this matters: The in-memory broker should have a predictable safety limit even when callers do not configure one explicitly.</para>
    /// <para>Expected result: The broker applies the default pending request limit of 10.</para>
    /// </remarks>
    [Test]
    public async Task Host_StartAsync_WithoutExplicitPendingLimit_AppliesDefaultPendingLimit() {
        using BeltRunnerHost host = CreateHost(new HostOptions {
            InteractionBrokerFactory = static () => new InMemoryInteractionBroker()
        });

        using IRun run = await host.StartAsync(CreatePlan(0), CancellationToken.None);
        InMemoryInteractionBroker broker = (InMemoryInteractionBroker)run.Interaction;
        await run.Completion.WaitAsync(TestTimeouts.Default);
        TestNarrative.Observe($"maxPendingRequestCount={broker.MaxPendingRequestCount}");

        Assert.That(broker.MaxPendingRequestCount, Is.EqualTo(10));
    }

    /// <summary>
    /// Verifies that a configured pending request limit is applied to the in-memory broker.
    /// </summary>
    /// <remarks>
    /// <para>Purpose: Confirm that explicit pending-request caps flow into the in-memory interaction broker.</para>
    /// <para>Why this matters: Host options should control runtime interaction backpressure consistently.</para>
    /// <para>Expected result: The broker applies the configured pending request limit.</para>
    /// </remarks>
    [Test]
    public async Task Host_StartAsync_WithPendingRequestLimit_AppliesLimitToInMemoryBroker() {
        using BeltRunnerHost host = CreateHost(new HostOptions {
            InteractionBrokerFactory = static () => new InMemoryInteractionBroker(),
            InteractionMaxPendingRequestCount = 3
        });

        using IRun run = await host.StartAsync(CreatePlan(0), CancellationToken.None);
        InMemoryInteractionBroker broker = (InMemoryInteractionBroker)run.Interaction;
        await run.Completion.WaitAsync(TestTimeouts.Default);
        TestNarrative.Observe($"maxPendingRequestCount={broker.MaxPendingRequestCount}");

        Assert.That(broker.MaxPendingRequestCount, Is.EqualTo(3));
    }

    /// <summary>
    /// Verifies that non-positive pending interaction limits are rejected.
    /// </summary>
    /// <remarks>
    /// <para>Purpose: Protect configuration validation for the pending-request limit.</para>
    /// <para>Why this matters: Zero or negative pending limits would make broker capacity semantics invalid.</para>
    /// <para>Expected result: Assigning a non-positive pending limit throws <see cref="ArgumentOutOfRangeException"/> for the <c>value</c> parameter.</para>
    /// </remarks>
    [Test]
    public void HostOptions_InteractionMaxPendingRequestCount_WithNonPositiveValue_Throws() {
        HostOptions options = new();

        ArgumentOutOfRangeException ex = Assert.Throws<ArgumentOutOfRangeException>(() => options.InteractionMaxPendingRequestCount = 0)!;
        TestNarrative.Observe($"setter rejected non-positive value with paramName={ex.ParamName}");

        Assert.That(ex.ParamName, Is.EqualTo("value"));
    }

    /// <summary>
    /// Verifies that non-positive interaction request retention limits are rejected.
    /// </summary>
    /// <remarks>
    /// <para>Purpose: Protect configuration validation for retained request history.</para>
    /// <para>Why this matters: Zero or negative history limits are ambiguous and should fail fast.</para>
    /// <para>Expected result: Assigning a non-positive request-log retention count throws <see cref="ArgumentOutOfRangeException"/> for the <c>value</c> parameter.</para>
    /// </remarks>
    [Test]
    public void HostOptions_InteractionRequestLogMaxRetainedCount_WithNonPositiveValue_Throws() {
        HostOptions options = new();

        ArgumentOutOfRangeException ex = Assert.Throws<ArgumentOutOfRangeException>(() => options.InteractionRequestLogMaxRetainedCount = 0)!;
        TestNarrative.Observe($"setter rejected non-positive value with paramName={ex.ParamName}");

        Assert.That(ex.ParamName, Is.EqualTo("value"));
    }

    /// <summary>
    /// Verifies that the in-memory broker rejects requests that exceed the configured pending limit.
    /// </summary>
    /// <remarks>
    /// <para>Purpose: Confirm that the broker enforces its pending-request cap at runtime.</para>
    /// <para>Why this matters: The cap only matters if excess requests are actively rejected when the limit is reached.</para>
    /// <para>Expected result: The second request throws, the first request remains pending, and the original request can still be completed successfully.</para>
    /// </remarks>
    [Test]
    public async Task InMemoryInteractionBroker_WhenPendingLimitIsReached_RejectsAdditionalPendingRequests() {
        using InMemoryInteractionBroker broker = new() {
            MaxPendingRequestCount = 1
        };

        PhaseKey phaseKey = new("phase/a");
        Task<bool> first = broker.AskAsync(new InteractionRequest<bool>("confirm", phaseKey, "Approve 0", "Continue 0?"), CancellationToken.None);

        await WaitUntilAsync(() => broker.ActiveRequests.Count == 1);

        InvalidOperationException ex = Assert.ThrowsAsync<InvalidOperationException>(async () =>
            await broker.AskAsync(new InteractionRequest<bool>("confirm", phaseKey, "Approve 1", "Continue 1?"), CancellationToken.None))!;
        TestNarrative.ObserveMany(
            $"pendingLimit={broker.MaxPendingRequestCount}",
            $"activeRequestCount={broker.ActiveRequests.Count}",
            $"exceptionMessage={ex.Message}");

        Assert.Multiple(() => {
            Assert.That(ex.Message, Does.Contain("Maximum number of pending interaction requests"));
            Assert.That(ex.Message, Does.Contain("limit=\"1\""));
            Assert.That(broker.ActiveRequests.Count, Is.EqualTo(1));
        });

        IInteractionRequest pendingRequest = broker.ActiveRequests.Single();
        Assert.That(broker.TryRespond(pendingRequest.RequestId, true), Is.True);
        bool response = await first.WaitAsync(TestTimeouts.Default);
        TestNarrative.ObserveMany(
            $"resolvedRequestTitle={pendingRequest.Title}",
            $"response={response}");
        Assert.That(response, Is.True);
    }

    private static BeltRunnerHost CreateHost(HostOptions options) {
        return new BeltRunnerHost(options);
    }

    private static SequentialPlan CreatePlan(int requestCount) {
        return new SequentialPlanBuilder()
            .Add(new InteractionPhaseFactory("phase/a", requestCount), "Phase A")
            .Build();
    }

    private static async Task ResolveRequestsAsync(InMemoryInteractionBroker broker, int requestCount) {
        for( int i = 0; i < requestCount; i++ ) {
            await WaitUntilAsync(() => broker.ActiveRequests.Count == 1);
            IInteractionRequest pendingRequest = broker.ActiveRequests[0];
            Assert.That(broker.TryRespond(pendingRequest.RequestId, true), Is.True);
        }
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

    private sealed class InteractionPhaseFactory : PhaseFactoryBase {
        private readonly int requestCount;

        public InteractionPhaseFactory(string key, int requestCount) : base(key) {
            this.requestCount = requestCount;
        }

        public override IPhase Create() {
            return new InteractionPhase(this.requestCount);
        }
    }

    private sealed class InteractionPhase : IPhase {
        private readonly int requestCount;

        public InteractionPhase(int requestCount) {
            this.requestCount = requestCount;
        }

        public string Name => "Interaction";

        public UnitSet Units { get; } = new();

        public async Task<IPhaseOutcome> ExecuteAsync(IPhaseContext context, CancellationToken ct = default) {
            for( int i = 0; i < this.requestCount; i++ ) {
                InteractionRequest<bool> request = new("confirm", context.Key, $"Approve {i}", $"Continue {i}?");
                await context.Interaction.AskAsync(request, ct);
            }

            return new PhaseOutcome();
        }
    }
}
