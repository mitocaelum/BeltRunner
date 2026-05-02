using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using BeltRunner.Core.Execution;
using BeltRunner.Core.Plan;
using BeltRunner.Core.Plan.Artifacts;

namespace BeltRunner.Core.Host;

/// <summary>
/// Controls the lifecycle of a single <see cref="IRun"/> execution.
/// </summary>
/// <remarks>
/// <para>
/// <see cref="IHost"/> is responsible for host-scope concerns only: lifecycle state,
/// cancellation request, and host-scope event publication.
/// Detailed run and phase information is exposed by <see cref="IRun"/>.
/// </para>
/// <para>
/// Initial input is provided as run-scope artifacts at start time.
/// This avoids special treatment for initial parameters and keeps the artifact model consistent.
/// </para>
/// </remarks>
public interface IHost : IDisposable {
    /// <summary>
    /// Gets whether a run is currently active.
    /// </summary>
    /// <remarks>
    /// This is a convenience flag. It is typically <c>true</c> while <see cref="State"/> is
    /// <see cref="HostState.Running"/> or <see cref="HostState.Cancelling"/>.
    /// </remarks>
    bool IsRunning { get; }

    /// <summary>
    /// Gets the current host-level lifecycle state.
    /// </summary>
    HostState State { get; }

    /// <summary>
    /// Gets the aggregate host-scope event stream.
    /// </summary>
    /// <remarks>
    /// This stream includes all host events, including state transitions and host-level faults.
    /// Use <see cref="StateChanges"/> and <see cref="Faults"/> for typed convenience streams.
    /// Fault payloads observed through this stream are sanitized and do not expose raw exceptions.
    /// </remarks>
    IObservable<HostEvent> EventStream { get; }

    /// <summary>
    /// Gets the host-scope state transition stream.
    /// </summary>
    /// <remarks>
    /// This is a typed convenience stream that publishes only <see cref="HostStateChangedEvent"/>.
    /// </remarks>
    IObservable<HostStateChangedEvent> StateChanges { get; }

    /// <summary>
    /// Gets the host-scope fault stream.
    /// </summary>
    /// <remarks>
    /// This is a typed convenience stream that publishes only <see cref="HostFaultedEvent"/>.
    /// The published fault payload is sanitized. Raw exception details are written only to trusted diagnostics
    /// such as NLog, when the application configures NLog to capture BeltRunner loggers.
    /// </remarks>
    IObservable<HostFaultedEvent> Faults { get; }

    /// <summary>
    /// Starts a new run based on the given sequential plan.
    /// </summary>
    /// <remarks>
    /// This overload starts a run with no initial artifacts.
    /// </remarks>
    Task<IRun> StartAsync(SequentialPlan plan, CancellationToken ct = default);

    /// <summary>
    /// Starts a new run based on the given sequential plan and seeds initial run-scope artifacts.
    /// </summary>
    /// <param name="plan">The sequential plan that defines the control flow.</param>
    /// <param name="initialArtifacts">
    /// Initial artifacts to seed into the run-level artifact store before the first phase starts.
    /// Artifact matching is based on artifact key signature: (Name, ValueType).
    /// </param>
    /// <param name="ct">A cancellation token that can cancel the start operation.</param>
    /// <remarks>
    /// <para>
    /// Implementations should validate the plan and the provided artifacts before starting the run.
    /// </para>
    /// <para>
    /// This method returns an already-started run. It does not wait for completion.
    /// Observe <see cref="IRun.EventStream"/> and <see cref="IRun.Completion"/> for progress and termination.
    /// </para>
    /// </remarks>
    Task<IRun> StartAsync(SequentialPlan plan, IReadOnlyList<IProducedArtifact> initialArtifacts, CancellationToken ct = default);

    /// <summary>
    /// Starts a new run based on the given sequential plan and run launch options.
    /// </summary>
    /// <param name="plan">The sequential plan that defines the control flow.</param>
    /// <param name="options">Optional run launch behavior for this request.</param>
    /// <param name="ct">A cancellation token that can cancel the start operation.</param>
    Task<IRun> StartAsync(SequentialPlan plan, RunLaunchOptions options, CancellationToken ct = default);

    /// <summary>
    /// Starts a new run based on the given sequential plan, initial artifacts, and run launch options.
    /// </summary>
    /// <param name="plan">The sequential plan that defines the control flow.</param>
    /// <param name="initialArtifacts">
    /// Initial artifacts to seed into the run-level artifact store before the first phase starts.
    /// Artifact matching is based on artifact key signature: (Name, ValueType).
    /// </param>
    /// <param name="options">Optional run launch behavior for this request.</param>
    /// <param name="ct">A cancellation token that can cancel the start operation.</param>
    /// <remarks>
    /// <para>
    /// Lifecycle callbacks in <paramref name="options"/> are invoked after the run is created and
    /// initialized, but before execution starts and before the first run event is published.
    /// </para>
    /// <para>
    /// <see cref="IRun.Completion"/> remains the primary terminal API.
    /// <see cref="RunLifecycleCallbacks.OnCompletedAsync"/> is a supplemental callback for run-scoped completion work.
    /// </para>
    /// <para>
    /// If a lifecycle callback throws, the run is not started and this method fails.
    /// </para>
    /// </remarks>
    Task<IRun> StartAsync(SequentialPlan plan, IReadOnlyList<IProducedArtifact> initialArtifacts, RunLaunchOptions options, CancellationToken ct = default);

    /// <summary>
    /// Requests cancellation of the current run.
    /// </summary>
    /// <param name="reason">Optional human-readable reason.</param>
    void Cancel(string reason = "");
}
