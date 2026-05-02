using System;
using System.Collections.Generic;
using System.Reactive.Linq;
using System.Threading;
using System.Threading.Tasks;
using BeltRunner.Core.Execution.Interaction;

namespace BeltRunner.Core.Execution;

/// <summary>
/// Interaction broker that represents interaction-disabled runs.
/// </summary>
/// <remarks>
/// <para>
/// This broker keeps interaction contracts non-null while intentionally preventing
/// request/response flows.
/// </para>
/// <para>
/// <see cref="IInteractionRequester.AskAsync{TResponse}(IInteractionRequest{TResponse}, CancellationToken)"/> and
/// <see cref="IInteractionRequester.TryAskAsync{TResponse}(IInteractionRequest{TResponse}, CancellationToken)"/> fail fast.
/// The request stream, active request set, and request log are always empty.
/// </para>
/// </remarks>
public sealed class DisabledInteractionBroker : IInteractionBroker {
    private const string INTERACTION_DISABLED_MESSAGE = "Interaction is not enabled for this run.";

    private static readonly IReadOnlyList<IInteractionRequest> emptyRequestLog = Array.Empty<IInteractionRequest>();
    private static readonly IObservable<IInteractionRequest> emptyRequests = Observable.Empty<IInteractionRequest>();
    private static readonly IObservable<IReadOnlyList<IInteractionRequest>> emptyActiveRequestsChanges = Observable.Return(emptyRequestLog);

    /// <inheritdoc />
    public IObservable<IInteractionRequest> Requests => emptyRequests;

    /// <inheritdoc />
    public IReadOnlyList<IInteractionRequest> RequestLog => emptyRequestLog;

    /// <inheritdoc />
    public IReadOnlyList<IInteractionRequest> ActiveRequests => emptyRequestLog;

    /// <inheritdoc />
    public IObservable<IReadOnlyList<IInteractionRequest>> ActiveRequestsChanges => emptyActiveRequestsChanges;

    /// <inheritdoc />
    /// <exception cref="InvalidOperationException">
    /// Thrown because interaction is not enabled for this run.
    /// </exception>
    public Task<TResponse> AskAsync<TResponse>(IInteractionRequest<TResponse> request, CancellationToken ct = default) {
        if( request is null )
            throw new ArgumentNullException(nameof(request));

        return Task.FromException<TResponse>(new InvalidOperationException(INTERACTION_DISABLED_MESSAGE));
    }

    /// <inheritdoc />
    /// <exception cref="InvalidOperationException">
    /// Thrown because interaction is not enabled for this run.
    /// </exception>
    public Task<InteractionResult<TResponse>> TryAskAsync<TResponse>(IInteractionRequest<TResponse> request, CancellationToken ct = default) {
        if( request is null )
            throw new ArgumentNullException(nameof(request));

        return Task.FromException<InteractionResult<TResponse>>(new InvalidOperationException(INTERACTION_DISABLED_MESSAGE));
    }

    /// <inheritdoc />
    public bool TryRespond<TResponse>(Guid requestId, TResponse response) {
        return false;
    }

    /// <inheritdoc />
    public bool TryReject(Guid requestId, string reason = "") {
        return false;
    }

    /// <inheritdoc />
    public void Dispose() {
    }
}
