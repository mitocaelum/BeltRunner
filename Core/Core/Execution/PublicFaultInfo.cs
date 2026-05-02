using System;

namespace BeltRunner.Core.Execution;

/// <summary>
/// Represents a sanitized fault summary that is safe to expose on public runtime surfaces.
/// </summary>
/// <remarks>
/// This model intentionally excludes raw exception messages, stack traces, exception data,
/// and inner exception graphs.
/// </remarks>
public sealed class PublicFaultInfo {
    /// <summary>
    /// Initializes a new instance of the <see cref="PublicFaultInfo"/> class.
    /// </summary>
    /// <param name="faultKind">A stable fault category identifier.</param>
    /// <param name="publicMessage">A sanitized message that is safe to expose publicly.</param>
    /// <param name="errorCode">An optional application-defined error code.</param>
    /// <param name="origin">An optional safe origin identifier for the fault.</param>
    /// <param name="occurredAt">The UTC timestamp when the fault summary was created.</param>
    public PublicFaultInfo(string faultKind, string publicMessage, string? errorCode, string? origin, DateTimeOffset occurredAt) {
        FaultKind = TextConstraints.NormalizeRequired(faultKind, TextConstraints.FAULT_KIND_MAX_LENGTH, nameof(faultKind));
        PublicMessage = TextConstraints.NormalizeRequired(publicMessage, TextConstraints.PUBLIC_FAULT_MESSAGE_MAX_LENGTH, nameof(publicMessage));
        ErrorCode = TextConstraints.NormalizeNullable(errorCode, TextConstraints.ERROR_CODE_MAX_LENGTH);
        Origin = TextConstraints.NormalizeNullable(origin, TextConstraints.FAULT_ORIGIN_MAX_LENGTH);
        OccurredAt = occurredAt;
    }

    /// <summary>
    /// Gets the stable fault category identifier.
    /// </summary>
    public string FaultKind { get; }

    /// <summary>
    /// Gets the sanitized message that is safe to expose publicly.
    /// </summary>
    public string PublicMessage { get; }

    /// <summary>
    /// Gets the optional application-defined error code.
    /// </summary>
    public string? ErrorCode { get; }

    /// <summary>
    /// Gets the optional safe origin identifier for the fault.
    /// </summary>
    public string? Origin { get; }

    /// <summary>
    /// Gets the UTC timestamp when the fault summary was created.
    /// </summary>
    public DateTimeOffset OccurredAt { get; }
}
