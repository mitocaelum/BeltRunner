using System.Threading;
using System.Threading.Tasks;

namespace BeltRunner.Core.Execution.Interaction;

/// <summary>
/// Publishes interaction requests and awaits responses.
/// </summary>
public interface IInteractionRequester {
    /// <summary>
    /// Publishes an interaction request and awaits the response.
    /// </summary>
    /// <typeparam name="TResponse">Expected response type.</typeparam>
    /// <param name="request">Request to publish.</param>
    /// <param name="ct">Cancellation token for this await.</param>
    /// <returns>A task that completes with the response value.</returns>
    Task<TResponse> AskAsync<TResponse>(IInteractionRequest<TResponse> request, CancellationToken ct = default);

    /// <summary>
    /// Publishes an interaction request and awaits either a response or a rejection outcome.
    /// </summary>
    /// <typeparam name="TResponse">Expected response type.</typeparam>
    /// <param name="request">Request to publish.</param>
    /// <param name="ct">Cancellation token for this await.</param>
    /// <returns>A task that completes with an accepted or rejected interaction result.</returns>
    Task<InteractionResult<TResponse>> TryAskAsync<TResponse>(IInteractionRequest<TResponse> request, CancellationToken ct = default);
}
