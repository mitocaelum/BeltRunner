namespace BeltRunner.Core.Host;

/// <summary>
/// Represents the lifecycle state of a host.
/// This is a host-level state, not a phase-level state.
/// </summary>
/// <remarks>
/// <para>
/// These states describe the host lifecycle around a single active run.
/// In particular, a cancellation-driven completion must always go through <see cref="Cancelling"/>
/// before reaching <see cref="Cancelled"/>.
/// </para>
/// <para>
/// The terminal states (<see cref="Cancelled"/>, <see cref="Completed"/>, <see cref="Faulted"/>) represent the final
/// outcome of the most recent run and are typically retained until a new run starts.
/// </para>
/// </remarks>
public enum HostState {
    /// <summary>
    /// No run is active.
    /// The host is ready to start a new run.
    ///
    /// Next states:
    /// <list type="bullet">
    /// <item><description><see cref="Running"/> (when a new run starts)</description></item>
    /// </list>
    /// </summary>
    Idle = 0,

    /// <summary>
    /// A run is currently executing.
    ///
    /// Next states:
    /// <list type="bullet">
    /// <item><description><see cref="Cancelling"/> (when cancellation is requested)</description></item>
    /// <item><description><see cref="Completed"/> (when the run completes successfully)</description></item>
    /// <item><description><see cref="Faulted"/> (when the run terminates due to an unhandled exception, or when the host treats the run as faulted based on policy)</description></item>
    /// </list>
    ///
    /// Note:
    /// <para>
    /// A cancellation-driven completion must always go through <see cref="Cancelling"/>.
    /// Therefore, <see cref="Cancelled"/> is not a direct next state from <see cref="Running"/>.
    /// </para>
    /// </summary>
    Running,

    /// <summary>
    /// Cancellation has been requested and shutdown is in progress.
    /// The run has not fully completed yet.
    ///
    /// Next states:
    /// <list type="bullet">
    /// <item><description><see cref="Cancelled"/> (when the run finishes due to cancellation)</description></item>
    /// <item><description><see cref="Completed"/> (if the run finishes successfully despite cancellation timing)</description></item>
    /// <item><description><see cref="Faulted"/> (if the run terminates due to an unhandled exception during shutdown, or when the host treats the run as faulted based on policy)</description></item>
    /// </list>
    ///
    /// Note:
    /// <para>
    /// This state is entered immediately when cancellation is requested (by the host or a token),
    /// regardless of whether phases promptly honor the cancellation token.
    /// </para>
    /// </summary>
    Cancelling,

    /// <summary>
    /// The run has completed due to cancellation and the final outcome is cancelled.
    ///
    /// Next states:
    /// <list type="bullet">
    /// <item><description><see cref="Running"/> (when a new run starts)</description></item>
    /// </list>
    ///
    /// Note:
    /// <para>
    /// This is a terminal state for a run and is typically retained until a new run starts.
    /// </para>
    /// </summary>
    Cancelled,

    /// <summary>
    /// The run has completed successfully (as a host-level completion state).
    ///
    /// Next states:
    /// <list type="bullet">
    /// <item><description><see cref="Running"/> (when a new run starts)</description></item>
    /// </list>
    ///
    /// Note:
    /// <para>
    /// This is a terminal state for a run and is typically retained until a new run starts.
    /// </para>
    /// </summary>
    Completed,

    /// <summary>
    /// The run has terminated due to an unhandled fault (exception), or was treated as faulted by host policy.
    ///
    /// Next states:
    /// <list type="bullet">
    /// <item><description><see cref="Running"/> (when a new run starts)</description></item>
    /// </list>
    ///
    /// Note:
    /// <para>
    /// This is a terminal state for a run and is typically retained until a new run starts.
    /// </para>
    /// </summary>
    Faulted
}