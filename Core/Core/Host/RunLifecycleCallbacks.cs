using System;
using System.Threading.Tasks;
using BeltRunner.Core.Execution;

namespace BeltRunner.Core.Host;

/// <summary>
/// Defines optional run-scoped lifecycle callbacks that are invoked by the launcher.
/// </summary>
/// <remarks>
/// <para>
/// Instances are supplied per run start request.
/// They are not shared as host-wide behavior.
/// </para>
/// <para>
/// <see cref="IRun.Completion"/> remains the primary terminal API.
/// The callbacks in this type are supplemental hooks for run-scoped integration work such as observer wiring,
/// metrics emission, or cleanup of external resources tied to a single run.
/// </para>
/// </remarks>
public sealed class RunLifecycleCallbacks {
    /// <summary>
    /// Gets or sets the callback that runs after the <see cref="IRun"/> is created and initialized,
    /// but before execution starts and before the first run event is published.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Use this callback to attach observers to <see cref="IRun.EventStream"/> or
    /// <see cref="IRun.SnapshotStream"/>, or to perform other run-scoped preparation work.
    /// </para>
    /// <para>
    /// If this callback throws, the run is not started and the enclosing start operation fails.
    /// </para>
    /// </remarks>
    public Func<IRun, ValueTask>? BeforeExecutionStartAsync { get; set; }

    /// <summary>
    /// Gets or sets the callback that runs after the run outcome is settled.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <see cref="IRun.Completion"/> remains the authoritative completion contract.
    /// Use this callback only for supplemental run-scoped work that should happen once a terminal
    /// <see cref="BeltRunner.Core.Execution.Outcome.RunOutcome"/> is available.
    /// </para>
    /// <para>
    /// This callback is invoked after the run outcome is fixed and <see cref="IRun.Completion"/> has completed.
    /// It runs before <see cref="BeforeRunDisposeAsync"/> if disposal begins after settlement.
    /// </para>
    /// <para>
    /// This callback is invoked at most once per run.
    /// </para>
    /// <para>
    /// If this callback throws, the exception is swallowed and the already-settled run outcome is not changed.
    /// </para>
    /// </remarks>
    public Func<IRun, ValueTask>? OnCompletedAsync { get; set; }

    /// <summary>
    /// Gets or sets the callback that runs immediately before <see cref="IDisposable.Dispose"/> begins the run's internal teardown.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Use this callback to detach run-scoped subscriptions, clear external references,
    /// or perform other cleanup that should happen while the run object is still intact.
    /// </para>
    /// <para>
    /// When <see cref="OnCompletedAsync"/> is configured, it runs before this callback.
    /// </para>
    /// <para>
    /// This callback is invoked at most once per run.
    /// </para>
    /// <para>
    /// <see cref="IDisposable.Dispose"/> remains best-effort and does not throw.
    /// If this callback throws, the exception is swallowed and disposal continues.
    /// </para>
    /// <para>
    /// Because <see cref="IDisposable.Dispose"/> is synchronous, implementations should complete promptly
    /// and avoid long-running asynchronous work even though the callback type is asynchronous.
    /// </para>
    /// </remarks>
    public Func<IRun, ValueTask>? BeforeRunDisposeAsync { get; set; }
}
