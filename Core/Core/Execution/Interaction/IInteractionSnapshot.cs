using System;
using BeltRunner.Core.Phase;

namespace BeltRunner.Core.Execution.Interaction;

/// <summary>
/// Represents an immutable snapshot of a pending interaction request.
/// </summary>
public interface IInteractionSnapshot {
    /// <summary>
    /// Gets the request identifier.
    /// </summary>
    Guid RequestId { get; }

    /// <summary>
    /// Gets the interaction kind.
    /// </summary>
    string Kind { get; }

    /// <summary>
    /// Gets the interaction title.
    /// </summary>
    string Title { get; }

    /// <summary>
    /// Gets the interaction message.
    /// </summary>
    string Message { get; }

    /// <summary>
    /// Gets the phase key that issued the interaction.
    /// </summary>
    PhaseKey PhaseKey { get; }

    /// <summary>
    /// Gets the expected response type.
    /// </summary>
    Type ResponseType { get; }

    /// <summary>
    /// Gets the time when the request was created.
    /// </summary>
    DateTimeOffset CreatedAt { get; }
}
