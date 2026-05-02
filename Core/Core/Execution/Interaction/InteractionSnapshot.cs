using System;
using BeltRunner.Core.Phase;

namespace BeltRunner.Core.Execution.Interaction;

/// <summary>
/// Default immutable implementation of <see cref="IInteractionSnapshot"/>.
/// </summary>
public sealed class InteractionSnapshot : IInteractionSnapshot {
    private const string INVALID_KIND_MESSAGE = "Kind cannot contain control characters.";
    private const string KIND_TOO_LONG_MESSAGE = "Kind cannot exceed 120 characters.";

    /// <summary>
    /// Initializes a new instance of the <see cref="InteractionSnapshot"/> class.
    /// </summary>
    public InteractionSnapshot(Guid requestId, string kind, string title, string message, PhaseKey phaseKey, Type responseType, DateTimeOffset createdAt) {
        if( kind is null ) throw new ArgumentNullException(nameof(kind));
        if( kind.Length > TextConstraints.INTERACTION_KIND_MAX_LENGTH ) throw new ArgumentException(KIND_TOO_LONG_MESSAGE, nameof(kind));
        if( TextConstraints.ContainsControlCharacters(kind) ) throw new ArgumentException(INVALID_KIND_MESSAGE, nameof(kind));

        RequestId = requestId;
        Kind = TextConstraints.NormalizeRequired(kind, TextConstraints.INTERACTION_KIND_MAX_LENGTH, nameof(kind));
        Title = TextConstraints.NormalizeOptional(title, TextConstraints.INTERACTION_TITLE_MAX_LENGTH);
        Message = TextConstraints.NormalizeOptional(message, TextConstraints.INTERACTION_MESSAGE_MAX_LENGTH);
        PhaseKey = phaseKey ?? throw new ArgumentNullException(nameof(phaseKey));
        ResponseType = responseType ?? throw new ArgumentNullException(nameof(responseType));
        CreatedAt = createdAt;
    }

    /// <inheritdoc />
    public Guid RequestId { get; }

    /// <inheritdoc />
    public string Kind { get; }

    /// <inheritdoc />
    public string Title { get; }

    /// <inheritdoc />
    public string Message { get; }

    /// <inheritdoc />
    public PhaseKey PhaseKey { get; }

    /// <inheritdoc />
    public Type ResponseType { get; }

    /// <inheritdoc />
    public DateTimeOffset CreatedAt { get; }
}
