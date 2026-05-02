// File: BeltRunner.Core/Execution/InteractionRequest.cs

using System;
using BeltRunner.Core.Phase;

namespace BeltRunner.Core.Execution.Interaction;

/// <summary>
/// Default typed request implementation.
/// Phases can instantiate this and pass it to <see cref="IInteractionRequester.AskAsync{TResponse}"/>
/// or <see cref="IInteractionRequester.TryAskAsync{TResponse}"/>.
/// </summary>
/// <typeparam name="TResponse">Expected response type.</typeparam>
public sealed class InteractionRequest<TResponse> : IInteractionRequest<TResponse> {
    private const string KIND_REQUIRED_MESSAGE = "Kind is required.";
    private const string INVALID_KIND_MESSAGE = "Kind cannot contain control characters.";
    private const string KIND_TOO_LONG_MESSAGE = "Kind cannot exceed 120 characters.";

    /// <summary>
    /// Initializes a new instance of the <see cref="InteractionRequest{TResponse}"/> class.
    /// </summary>
    /// <param name="kind">The application-defined interaction kind.</param>
    /// <param name="phaseKey">The key of the phase that issued the request.</param>
    /// <param name="title">An optional short title suitable for display.</param>
    /// <param name="message">An optional detailed message associated with the request.</param>
    /// <param name="requestId">An optional request identifier. When omitted, a new identifier is generated.</param>
    /// <param name="timestamp">An optional timestamp. When omitted, the current UTC time is used.</param>
    public InteractionRequest(string kind, PhaseKey phaseKey, string title = "", string message = "", Guid? requestId = null, DateTimeOffset? timestamp = null) {
        if( string.IsNullOrWhiteSpace(kind) )
            throw new ArgumentException(KIND_REQUIRED_MESSAGE, nameof(kind));

        if( kind.Length > TextConstraints.INTERACTION_KIND_MAX_LENGTH )
            throw new ArgumentException(KIND_TOO_LONG_MESSAGE, nameof(kind));

        if( TextConstraints.ContainsControlCharacters(kind) )
            throw new ArgumentException(INVALID_KIND_MESSAGE, nameof(kind));

        if( phaseKey is null )
            throw new ArgumentNullException(nameof(phaseKey));

        RequestId = requestId ?? Guid.NewGuid();
        Kind = TextConstraints.NormalizeRequired(kind, TextConstraints.INTERACTION_KIND_MAX_LENGTH, nameof(kind));
        PhaseKey = phaseKey;

        Title = TextConstraints.NormalizeOptional(title, TextConstraints.INTERACTION_TITLE_MAX_LENGTH);
        Message = TextConstraints.NormalizeOptional(message, TextConstraints.INTERACTION_MESSAGE_MAX_LENGTH);

        Timestamp = timestamp ?? DateTimeOffset.UtcNow;
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
    public Type ResponseType => typeof(TResponse);

    /// <inheritdoc />
    public DateTimeOffset Timestamp { get; }
}
