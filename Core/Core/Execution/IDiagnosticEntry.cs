using System;
using BeltRunner.Core.Phase;

namespace BeltRunner.Core.Execution;

/// <summary>
/// Represents one retained diagnostic entry.
/// </summary>
public interface IDiagnosticEntry {
    /// <summary>
    /// Gets the diagnostic identifier.
    /// </summary>
    Guid Id { get; }

    /// <summary>
    /// Gets the time when the diagnostic was recorded.
    /// </summary>
    DateTimeOffset Timestamp { get; }

    /// <summary>
    /// Gets the diagnostic severity.
    /// </summary>
    DiagnosticSeverity Severity { get; }

    /// <summary>
    /// Gets the diagnostic message.
    /// </summary>
    string Message { get; }

    /// <summary>
    /// Gets the sanitized fault summary, if one exists.
    /// </summary>
    /// <remarks>
    /// This property is the public diagnostic surface for exception-related diagnostics.
    /// Raw exception instances are not exposed here.
    /// </remarks>
    PublicFaultInfo? FaultInfo { get; }

    /// <summary>
    /// Gets the related phase key, if one exists.
    /// </summary>
    PhaseKey? PhaseKey { get; }

    /// <summary>
    /// Gets the related unit identifier, if one exists.
    /// </summary>
    Guid? UnitId { get; }
}
