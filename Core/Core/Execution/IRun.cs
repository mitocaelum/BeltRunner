using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using BeltRunner.Core.Execution.Event;
using BeltRunner.Core.Execution.Interaction;
using BeltRunner.Core.Execution.Outcome;
using BeltRunner.Core.Plan.Artifacts;

namespace BeltRunner.Core.Execution;

/// <summary>
/// Represents a single run instance for one plan execution.
/// </summary>
/// <remarks>
/// <para>
/// <see cref="IRun"/> is the run-scope contract for observing lifecycle signals,
/// reading retained diagnostics and artifacts, requesting cancellation, and awaiting completion.
/// </para>
/// <para>
/// Implementations should preserve terminal information after completion so callers
/// can inspect outcomes and state snapshots without timing-sensitive subscriptions.
/// </para>
/// <para>
/// Error handling is exposed through both <see cref="Completion"/> and <see cref="EventStream"/>.
/// A faulted execution completes <see cref="Completion"/> with a <see cref="RunOutcome"/>
/// whose <see cref="RunOutcome.Kind"/> is <see cref="RunOutcomeKind.Faulted"/>.
/// <see cref="EventStream"/> never faults and instead publishes a fault event before completing.
/// </para>
/// <para>
/// Public runtime surfaces such as <see cref="EventStream"/>, <see cref="DiagnosticStream"/>,
/// and <see cref="Completion"/> expose sanitized fault information only. Raw exception instances,
/// stack traces, and unsanitized exception messages are intentionally excluded from these contracts.
/// </para>
/// <para>
/// BeltRunner writes raw exceptions to NLog for trusted diagnostics when fault-related events or diagnostics
/// are emitted. BeltRunner does not register NLog rules automatically, so these entries are produced only
/// when the application configures NLog to capture BeltRunner loggers.
/// </para>
/// </remarks>
public interface IRun : IDisposable {
    /// <summary>
    /// Gets the stable correlation id of this run.
    /// </summary>
    Guid Id { get; }

    /// <summary>
    /// Gets the current run status as a convenience shortcut that mirrors <see cref="IRunSnapshot.Status"/> on <see cref="Snapshot"/>.
    /// </summary>
    /// <remarks>
    /// The authoritative current-state representation is <see cref="Snapshot"/>.
    /// Use this property when only the top-level run status is needed.
    /// </remarks>
    RunStatus Status { get; }

    /// <summary>
    /// Gets the latest immutable run snapshot.
    /// </summary>
    /// <remarks>
    /// This snapshot is the authoritative current-state representation for the run.
    /// <see cref="Status"/> exposes the same run status as a convenience shortcut.
    /// </remarks>
    IRunSnapshot Snapshot { get; }

    /// <summary>
    /// Gets a replayable stream of immutable run snapshots.
    /// </summary>
    IObservable<IRunSnapshot> SnapshotStream { get; }

    /// <summary>
    /// Gets the interaction broker used for operator-facing request/response interactions.
    /// </summary>
    IInteractionBroker Interaction { get; }

    /// <summary>
    /// Gets the currently pending interaction requests for this run.
    /// </summary>
    IReadOnlyList<IInteractionSnapshot> ActiveInteractions { get; }

    /// <summary>
    /// Gets the read-only artifact view for this run.
    /// </summary>
    IArtifactReader Artifacts { get; }

    /// <summary>
    /// Gets the run lifecycle event stream.
    /// </summary>
    /// <remarks>
    /// This stream never faults. Run and phase failures are published as regular events such as
    /// <see cref="RunFaultedEvent"/> and <see cref="PhaseFaultedEvent"/>, whose fault payloads are sanitized
    /// <see cref="PublicFaultInfo"/> instances rather than raw exceptions.
    /// </remarks>
    IObservable<RunEvent> EventStream { get; }

    /// <summary>
    /// Gets the replayable diagnostic stream for this run.
    /// </summary>
    /// <remarks>
    /// Diagnostics exposed by this stream are safe for public runtime consumption. When a diagnostic is associated
    /// with an exception, <see cref="IDiagnosticEntry.FaultInfo"/> contains sanitized fault information only.
    /// </remarks>
    IObservable<IDiagnosticEntry> DiagnosticStream { get; }

    /// <summary>
    /// Gets the retained event history in publication order.
    /// </summary>
    IReadOnlyList<RunEvent> EventLog { get; }

    /// <summary>
    /// Gets the retained diagnostic history in publication order.
    /// </summary>
    IReadOnlyList<IDiagnosticEntry> DiagnosticLog { get; }

    /// <summary>
    /// Gets a task that completes when the run reaches a terminal state.
    /// </summary>
    /// <remarks>
    /// This task always completes successfully with a <see cref="RunOutcome"/>.
    /// Faulted runs are represented by a <see cref="RunOutcome"/> whose
    /// <see cref="RunOutcome.Kind"/> is <see cref="RunOutcomeKind.Faulted"/> instead of faulting the task.
    /// The fault payload is sanitized and is intended for public runtime inspection rather than raw exception access.
    /// </remarks>
    Task<RunOutcome> Completion { get; }

    /// <summary>
    /// Gets the run-scoped cancellation token propagated to phases.
    /// </summary>
    CancellationToken CancellationToken { get; }

    /// <summary>
    /// Gets whether cancellation has been requested.
    /// </summary>
    bool IsCancellationRequested { get; }

    /// <summary>
    /// Gets the cancellation reason text, if one was recorded.
    /// </summary>
    string? CancelReason { get; }

    /// <summary>
    /// Requests cooperative cancellation of this run.
    /// </summary>
    /// <param name="reason">Optional human-readable cancellation reason. <see langword="null"/> indicates that no explicit reason was provided.</param>
    void RequestCancellation(string? reason = null);
}
