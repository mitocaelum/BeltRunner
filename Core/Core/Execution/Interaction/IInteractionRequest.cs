// File: BeltRunner.Core/Execution/IInteractionRequest.cs

using System;
using BeltRunner.Core.Phase;

namespace BeltRunner.Core.Execution.Interaction;

/// <summary>
/// Untyped interaction request contract for UI routing.
/// </summary>
public interface IInteractionRequest {
    /// <summary>
    /// Correlation id for this interaction request.
    /// </summary>
    Guid RequestId { get; }

    /// <summary>
    /// Routing key that indicates the UI shape.
    /// Example: "confirm", "choice", "text".
    /// </summary>
    string Kind { get; }

    /// <summary>
    /// Optional title shown to the user.
    /// </summary>
    string Title { get; }

    /// <summary>
    /// Main message shown to the user.
    /// </summary>
    string Message { get; }

    /// <summary>
    /// Phase key that issued this request.
    /// </summary>
    PhaseKey PhaseKey { get; }

    /// <summary>
    /// Expected response type.
    /// </summary>
    Type ResponseType { get; }

    /// <summary>
    /// Timestamp for diagnostics.
    /// </summary>
    DateTimeOffset Timestamp { get; }
}

/// <summary>
/// Typed interaction request contract used by phases.
/// </summary>
/// <typeparam name="TResponse">Expected response type.</typeparam>
public interface IInteractionRequest<TResponse> : IInteractionRequest {
}