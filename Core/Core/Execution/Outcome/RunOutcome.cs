using System;
using BeltRunner.Core.Execution;

namespace BeltRunner.Core.Execution.Outcome;

/// <summary>
/// Represents the settled terminal outcome of a run.
/// </summary>
/// <remarks>
/// This type is the public completion contract for all terminal run states.
/// Callers should typically branch on <see cref="Kind"/> and then read common details
/// such as <see cref="Summary"/>, <see cref="CancellationReason"/>, or <see cref="FaultInfo"/>.
/// </remarks>
public sealed class RunOutcome {
    private RunOutcome(RunOutcomeKind kind, string summary, string? cancellationReason, PublicFaultInfo? faultInfo) {
        if( !Enum.IsDefined(typeof(RunOutcomeKind), kind) ) {
            throw new ArgumentOutOfRangeException(nameof(kind), "Outcome kind is invalid.");
        }

        if( kind == RunOutcomeKind.Cancelled ) {
            cancellationReason = TextConstraints.NormalizeNullable(cancellationReason, TextConstraints.CANCEL_REASON_MAX_LENGTH);
        } else if( cancellationReason is not null ) {
            throw new ArgumentException("Cancellation reason is supported only for cancelled outcomes.", nameof(cancellationReason));
        }

        if( kind == RunOutcomeKind.Faulted ) {
            FaultInfo = faultInfo ?? throw new ArgumentNullException(nameof(faultInfo));
        } else if( faultInfo is not null ) {
            throw new ArgumentException("Fault information is supported only for faulted outcomes.", nameof(faultInfo));
        }

        Kind = kind;
        Summary = summary ?? string.Empty;
        CancellationReason = cancellationReason;
    }

    /// <summary>
    /// Gets the terminal kind of the run.
    /// </summary>
    public RunOutcomeKind Kind { get; }

    /// <summary>
    /// Gets the optional human-readable outcome summary.
    /// </summary>
    public string Summary { get; }

    /// <summary>
    /// Gets the human-readable cancellation reason when <see cref="Kind"/> is <see cref="RunOutcomeKind.Cancelled"/>.
    /// </summary>
    public string? CancellationReason { get; }

    /// <summary>
    /// Gets the sanitized fault summary when <see cref="Kind"/> is <see cref="RunOutcomeKind.Faulted"/>.
    /// </summary>
    public PublicFaultInfo? FaultInfo { get; }

    /// <summary>
    /// Gets a value indicating whether the run completed successfully or partially successfully.
    /// </summary>
    public bool IsSuccessful => Kind is RunOutcomeKind.Succeeded or RunOutcomeKind.PartiallySucceeded;

    /// <summary>
    /// Gets a value indicating whether the run completed without warnings.
    /// </summary>
    public bool IsSucceeded => Kind == RunOutcomeKind.Succeeded;

    /// <summary>
    /// Gets a value indicating whether the run completed with warnings.
    /// </summary>
    public bool IsPartiallySucceeded => Kind == RunOutcomeKind.PartiallySucceeded;

    /// <summary>
    /// Gets a value indicating whether the run completed as a non-exception failure.
    /// </summary>
    public bool IsFailed => Kind == RunOutcomeKind.Failed;

    /// <summary>
    /// Gets a value indicating whether the run was cancelled.
    /// </summary>
    public bool IsCancelled => Kind == RunOutcomeKind.Cancelled;

    /// <summary>
    /// Gets a value indicating whether the run faulted because of an unhandled exception.
    /// </summary>
    public bool IsFaulted => Kind == RunOutcomeKind.Faulted;

    /// <summary>
    /// Creates a successful outcome.
    /// </summary>
    /// <param name="summary">An optional human-readable summary.</param>
    /// <returns>A successful run outcome.</returns>
    public static RunOutcome Succeeded(string summary = "") {
        return new RunOutcome(RunOutcomeKind.Succeeded, summary, null, null);
    }

    /// <summary>
    /// Creates a partially successful outcome.
    /// </summary>
    /// <param name="summary">An optional human-readable summary.</param>
    /// <returns>A partially successful run outcome.</returns>
    public static RunOutcome PartiallySucceeded(string summary = "") {
        return new RunOutcome(RunOutcomeKind.PartiallySucceeded, summary, null, null);
    }

    /// <summary>
    /// Creates a failed outcome.
    /// </summary>
    /// <param name="summary">An optional human-readable summary.</param>
    /// <returns>A failed run outcome.</returns>
    public static RunOutcome Failed(string summary = "") {
        return new RunOutcome(RunOutcomeKind.Failed, summary, null, null);
    }

    /// <summary>
    /// Creates a cancelled outcome.
    /// </summary>
    /// <param name="reason">The human-readable cancellation reason, if one is available.</param>
    /// <param name="summary">An optional human-readable summary.</param>
    /// <returns>A cancelled run outcome.</returns>
    public static RunOutcome Cancelled(string? reason, string summary = "") {
        string? safeReason = TextConstraints.NormalizeNullable(reason, TextConstraints.CANCEL_REASON_MAX_LENGTH);
        string safeSummary = string.IsNullOrEmpty(summary) ? safeReason ?? string.Empty : summary;
        return new RunOutcome(RunOutcomeKind.Cancelled, safeSummary, safeReason, null);
    }

    /// <summary>
    /// Creates a faulted outcome.
    /// </summary>
    /// <param name="faultInfo">The sanitized fault summary for the faulted run.</param>
    /// <param name="summary">An optional human-readable summary.</param>
    /// <returns>A faulted run outcome.</returns>
    public static RunOutcome Faulted(PublicFaultInfo faultInfo, string summary = "") {
        if( faultInfo is null ) {
            throw new ArgumentNullException(nameof(faultInfo));
        }

        string safeSummary = string.IsNullOrEmpty(summary) ? faultInfo.PublicMessage : summary;
        return new RunOutcome(RunOutcomeKind.Faulted, safeSummary, null, faultInfo);
    }
}
