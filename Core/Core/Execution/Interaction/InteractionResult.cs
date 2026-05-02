using System;

namespace BeltRunner.Core.Execution.Interaction;

/// <summary>
/// Represents the completion result of an interaction that can either return a response or be rejected.
/// </summary>
/// <typeparam name="TResponse">Response type.</typeparam>
public sealed class InteractionResult<TResponse> {
    private const string REJECTED_RESPONSE_MESSAGE = "Response is not available because the interaction was rejected.";

    private readonly TResponse response;

    private InteractionResult(bool isAccepted, TResponse response, string reason) {
        IsAccepted = isAccepted;
        this.response = response;
        Reason = reason ?? string.Empty;
    }

    /// <summary>
    /// Gets a value indicating whether the interaction completed with a response.
    /// </summary>
    public bool IsAccepted { get; }

    /// <summary>
    /// Gets the response value.
    /// </summary>
    /// <exception cref="InvalidOperationException">
    /// Thrown when the interaction completed as rejected.
    /// </exception>
    public TResponse Response {
        get {
            if( !IsAccepted )
                throw new InvalidOperationException(REJECTED_RESPONSE_MESSAGE);

            return this.response;
        }
    }

    /// <summary>
    /// Gets the rejection reason text.
    /// </summary>
    public string Reason { get; }

    /// <summary>
    /// Creates an accepted interaction result.
    /// </summary>
    /// <param name="response">Accepted response value.</param>
    /// <returns>An accepted result.</returns>
    public static InteractionResult<TResponse> Accepted(TResponse response) {
        return new InteractionResult<TResponse>(true, response, string.Empty);
    }

    /// <summary>
    /// Creates a rejected interaction result.
    /// </summary>
    /// <param name="reason">Optional human-readable rejection reason.</param>
    /// <returns>A rejected result.</returns>
    public static InteractionResult<TResponse> Rejected(string reason = "") {
        return new InteractionResult<TResponse>(false, default!, reason ?? string.Empty);
    }
}
