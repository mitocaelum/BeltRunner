using System;
using System.Collections.Generic;
namespace BeltRunner.Core.Execution.Interaction;

/// <summary>
/// Run-scope interaction broker for request/response style user interventions.
/// Phases can publish requests and await responses without referencing any UI framework.
/// Some implementations can represent interaction-disabled runs.
/// </summary>
public interface IInteractionBroker : IInteractionRequester, IDisposable {
    /// <summary>
    /// Stream of interaction requests.
    /// This stream should be replayable so late subscribers can observe past requests.
    /// </summary>
    IObservable<IInteractionRequest> Requests { get; }

    /// <summary>
    /// Retained request log kept after completion.
    /// </summary>
    IReadOnlyList<IInteractionRequest> RequestLog { get; }

    /// <summary>
    /// Gets the currently active interaction requests.
    /// </summary>
    IReadOnlyList<IInteractionRequest> ActiveRequests { get; }

    /// <summary>
    /// Gets a replayable stream of active interaction request snapshots.
    /// </summary>
    IObservable<IReadOnlyList<IInteractionRequest>> ActiveRequestsChanges { get; }

    /// <summary>
    /// Attempts to respond to a previously published request.
    /// </summary>
    /// <typeparam name="TResponse">Response type.</typeparam>
    /// <param name="requestId">Target request id.</param>
    /// <param name="response">Response value.</param>
    /// <returns><c>true</c> if the request was pending and is now completed; otherwise <c>false</c>.</returns>
    bool TryRespond<TResponse>(Guid requestId, TResponse response);

    /// <summary>
    /// Attempts to reject a previously published request.
    /// </summary>
    /// <param name="requestId">Target request id.</param>
    /// <param name="reason">Optional human-readable reason.</param>
    /// <returns><c>true</c> if the request was pending and is now rejected; otherwise <c>false</c>.</returns>
    bool TryReject(Guid requestId, string reason = "");
}
