using System;
using BeltRunner.Core.Phase;

namespace BeltRunner.Core.Execution;

/// <summary>
/// Default immutable implementation of <see cref="IDiagnosticEntry"/>.
/// </summary>
public sealed class DiagnosticEntry : IDiagnosticEntry {
    /// <summary>
    /// Initializes a new instance of the <see cref="DiagnosticEntry"/> class.
    /// </summary>
    public DiagnosticEntry(Guid id, DateTimeOffset timestamp, DiagnosticSeverity severity, string message, PublicFaultInfo? faultInfo, PhaseKey? phaseKey, Guid? unitId) {
        Id = id;
        Timestamp = timestamp;
        Severity = severity;
        Message = TextConstraints.NormalizeRequired(message, TextConstraints.DIAGNOSTIC_MESSAGE_MAX_LENGTH, nameof(message));
        FaultInfo = faultInfo;
        PhaseKey = phaseKey;
        UnitId = unitId;
    }

    /// <inheritdoc />
    public Guid Id { get; }

    /// <inheritdoc />
    public DateTimeOffset Timestamp { get; }

    /// <inheritdoc />
    public DiagnosticSeverity Severity { get; }

    /// <inheritdoc />
    public string Message { get; }

    /// <inheritdoc />
    public PublicFaultInfo? FaultInfo { get; }

    /// <inheritdoc />
    public PhaseKey? PhaseKey { get; }

    /// <inheritdoc />
    public Guid? UnitId { get; }
}
