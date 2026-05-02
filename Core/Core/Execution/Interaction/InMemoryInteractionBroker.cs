using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reactive;
using System.Reactive.Disposables;
using System.Reactive.Linq;
using System.Reactive.Subjects;
using System.Threading;
using System.Threading.Tasks;

namespace BeltRunner.Core.Execution.Interaction;

/// <summary>
/// In-memory interaction broker implementation.
/// Provides replayable request and active-request streams and supports awaiting responses.
/// </summary>
public sealed class InMemoryInteractionBroker : IInteractionBroker {
    private const string DISPOSED_MESSAGE = "Interaction broker was disposed.";
    private const string MAX_PENDING_REQUESTS_REACHED_MESSAGE = "Maximum number of pending interaction requests has been reached.";
    private const string REJECTED_ASK_MESSAGE = "Interaction was rejected for a request started with AskAsync.";
    private const string REQUEST_NULL_MESSAGE = "Request is null.";
    private const string RESPONSE_TYPE_MISMATCH_MESSAGE = "Response type mismatch.";
    private const string REQUEST_TYPE_MISMATCH_MESSAGE = "Request response type mismatch.";

    private readonly object gate = new();
    private bool disposed;

    private readonly Subject<StoredRequest> requestsSubject = new();
    private readonly ISubject<StoredRequest> requests;
    private readonly CircularBuffer<StoredRequest> requestLog = new();
    private readonly IReadOnlyList<IInteractionRequest> requestLogView;
    private long lastSequence;
    private readonly IObservable<IInteractionRequest> requestsObservable;

    private readonly Dictionary<Guid, PendingRequest> pending = new();
    private readonly BehaviorSubject<IReadOnlyList<IInteractionRequest>> activeRequestsSubject = new(Array.Empty<IInteractionRequest>());
    private readonly ISubject<IReadOnlyList<IInteractionRequest>> activeRequestsSink;

    /// <summary>
    /// Initializes a new instance of the <see cref="InMemoryInteractionBroker"/> class.
    /// </summary>
    public InMemoryInteractionBroker() {
        this.requests = Subject.Synchronize(this.requestsSubject);
        this.requestLogView = new RequestLogView(this.requestLog, this.gate);
        this.requestsObservable = CreateReplayableRequestsObservable();
        this.activeRequestsSink = Subject.Synchronize(this.activeRequestsSubject);
    }

    /// <summary>
    /// Gets or sets the maximum number of retained entries in <see cref="RequestLog"/>.
    /// </summary>
    /// <remarks>
    /// Set this value to limit in-memory request retention and replay size for late subscribers.
    /// The default is <c>64</c>.
    /// </remarks>
    /// <exception cref="ArgumentOutOfRangeException">
    /// Thrown when the assigned value is less than or equal to zero.
    /// </exception>
    public int? RequestLogMaxRetainedCount {
        get {
            lock( this.gate ) {
                return this.requestLogMaxRetainedCount;
            }
        }
        set {
            if( value.HasValue && value.Value <= 0 ) {
                throw new ArgumentOutOfRangeException(nameof(value), "Interaction request log max retained count must be greater than zero.");
            }

            lock( this.gate ) {
                this.requestLogMaxRetainedCount = value;
                this.requestLog.SetCapacity(value);
            }
        }
    }
    private int? requestLogMaxRetainedCount = 64;

    /// <summary>
    /// Gets or sets the maximum number of simultaneously pending interaction requests.
    /// </summary>
    /// <remarks>
    /// Set this value to cap the size of <see cref="ActiveRequests"/> and <see cref="ActiveRequestsChanges"/>.
    /// The default is <c>10</c>.
    /// </remarks>
    /// <exception cref="ArgumentOutOfRangeException">
    /// Thrown when the assigned value is less than or equal to zero.
    /// </exception>
    public int MaxPendingRequestCount {
        get {
            lock( this.gate ) {
                return this.maxPendingRequestCount;
            }
        }
        set {
            if( value <= 0 ) {
                throw new ArgumentOutOfRangeException(nameof(value), "Interaction max pending request count must be greater than zero.");
            }

            lock( this.gate ) {
                this.maxPendingRequestCount = value;
            }
        }
    }
    private int maxPendingRequestCount = 10;

    /// <inheritdoc />
    public IObservable<IInteractionRequest> Requests => this.requestsObservable;

    /// <inheritdoc />
    public IReadOnlyList<IInteractionRequest> RequestLog => this.requestLogView;

    /// <inheritdoc />
    public IReadOnlyList<IInteractionRequest> ActiveRequests {
        get {
            lock( this.gate ) {
                return this.pending.Values.Select(x => x.Request).ToArray();
            }
        }
    }

    /// <inheritdoc />
    public IObservable<IReadOnlyList<IInteractionRequest>> ActiveRequestsChanges => this.activeRequestsSubject;

    /// <inheritdoc />
    public async Task<TResponse> AskAsync<TResponse>(IInteractionRequest<TResponse> request, CancellationToken ct = default) {
        PendingRequestCompletion completion = await AwaitCompletionAsync(request, ct).ConfigureAwait(false);

        if( completion.Kind == PendingRequestCompletionKind.Rejected ) {
            throw new InvalidOperationException(
                string.IsNullOrEmpty(completion.Reason)
                    ? REJECTED_ASK_MESSAGE
                    : $"{REJECTED_ASK_MESSAGE} reason=\"{completion.Reason}\"");
        }

        object boxed = completion.Response;

        if( boxed is TResponse typed )
            return typed;

        throw new InvalidOperationException($"{RESPONSE_TYPE_MISMATCH_MESSAGE} expected=\"{typeof(TResponse).FullName}\" actual=\"{boxed?.GetType().FullName ?? "null"}\"");
    }

    /// <inheritdoc />
    public async Task<InteractionResult<TResponse>> TryAskAsync<TResponse>(IInteractionRequest<TResponse> request, CancellationToken ct = default) {
        PendingRequestCompletion completion = await AwaitCompletionAsync(request, ct).ConfigureAwait(false);

        if( completion.Kind == PendingRequestCompletionKind.Rejected )
            return InteractionResult<TResponse>.Rejected(completion.Reason);

        object boxed = completion.Response;

        if( boxed is TResponse typed )
            return InteractionResult<TResponse>.Accepted(typed);

        throw new InvalidOperationException($"{RESPONSE_TYPE_MISMATCH_MESSAGE} expected=\"{typeof(TResponse).FullName}\" actual=\"{boxed?.GetType().FullName ?? "null"}\"");
    }

    private async Task<PendingRequestCompletion> AwaitCompletionAsync<TResponse>(IInteractionRequest<TResponse> request, CancellationToken ct) {
        if( request is null )
            throw new ArgumentNullException(nameof(request), REQUEST_NULL_MESSAGE);

        if( request.ResponseType != typeof(TResponse) )
            throw new ArgumentException(REQUEST_TYPE_MISMATCH_MESSAGE, nameof(request));

        PendingRequest pendingRequest;
        StoredRequest stored;
        IReadOnlyList<IInteractionRequest> activeSnapshot;

        lock( this.gate ) {
            ThrowIfDisposed();

            if( this.pending.ContainsKey(request.RequestId) )
                throw new InvalidOperationException($"A request with the same RequestId is already pending. requestId=\"{request.RequestId}\"");

            if( this.pending.Count >= this.maxPendingRequestCount )
                throw new InvalidOperationException($"{MAX_PENDING_REQUESTS_REACHED_MESSAGE} limit=\"{this.maxPendingRequestCount}\"");

            TaskCompletionSource<PendingRequestCompletion> tcs = new(TaskCreationOptions.RunContinuationsAsynchronously);

            CancellationTokenRegistration reg = default;
            if( ct.CanBeCanceled ) {
                reg = ct.Register(() => tcs.TrySetCanceled(ct));
            }

            pendingRequest = new PendingRequest(request, tcs, reg);
            this.pending.Add(request.RequestId, pendingRequest);

            long seq = ++this.lastSequence;
            stored = new StoredRequest(seq, request);
            this.requestLog.Add(stored);
            activeSnapshot = BuildActiveRequestsSnapshot_NoLock();
        }

        this.requests.OnNext(stored);
        this.activeRequestsSink.OnNext(activeSnapshot);

        try {
            return await pendingRequest.Completion.Task.ConfigureAwait(false);
        } finally {
            PendingRequest? removed = null;
            IReadOnlyList<IInteractionRequest>? snapshot = null;

            lock( this.gate ) {
                if( this.pending.TryGetValue(request.RequestId, out PendingRequest existing) && ReferenceEquals(existing, pendingRequest) ) {
                    this.pending.Remove(request.RequestId);
                    removed = existing;
                    snapshot = BuildActiveRequestsSnapshot_NoLock();
                }
            }

            removed?.DisposeRegistrationOnce();
            if( snapshot is not null ) {
                this.activeRequestsSink.OnNext(snapshot);
            }
        }
    }

    /// <inheritdoc />
    public bool TryRespond<TResponse>(Guid requestId, TResponse response) {
        PendingRequest? pendingRequest;
        IReadOnlyList<IInteractionRequest>? snapshot;

        lock( this.gate ) {
            if( this.disposed )
                return false;

            if( !this.pending.TryGetValue(requestId, out pendingRequest) )
                return false;

            if( response is null ) {
                if( pendingRequest.ResponseType.IsValueType )
                    return false;
            } else {
                Type actual = response.GetType();
                if( !pendingRequest.ResponseType.IsAssignableFrom(actual) )
                    return false;
            }

            this.pending.Remove(requestId);
            snapshot = BuildActiveRequestsSnapshot_NoLock();
        }

        pendingRequest.DisposeRegistrationOnce();
        pendingRequest.Completion.TrySetResult(PendingRequestCompletion.Accepted(response!));
        this.activeRequestsSink.OnNext(snapshot);
        return true;
    }

    /// <inheritdoc />
    public bool TryReject(Guid requestId, string reason = "") {
        PendingRequest? pendingRequest;
        IReadOnlyList<IInteractionRequest>? snapshot;

        lock( this.gate ) {
            if( this.disposed )
                return false;

            if( !this.pending.TryGetValue(requestId, out pendingRequest) )
                return false;

            this.pending.Remove(requestId);
            snapshot = BuildActiveRequestsSnapshot_NoLock();
        }

        string safeReason = TextConstraints.NormalizeOptional(reason, TextConstraints.REJECTION_REASON_MAX_LENGTH);
        pendingRequest.DisposeRegistrationOnce();
        pendingRequest.Completion.TrySetResult(PendingRequestCompletion.Rejected(safeReason));
        this.activeRequestsSink.OnNext(snapshot);
        return true;
    }

    /// <summary>
    /// Releases resources owned by the broker and completes its observable streams.
    /// </summary>
    /// <remarks>
    /// Pending requests are completed with <see cref="ObjectDisposedException"/> so callers are not left waiting
    /// after disposal.
    /// </remarks>
    public void Dispose() {
        List<PendingRequest> toCancel;

        lock( this.gate ) {
            if( this.disposed )
                return;

            this.disposed = true;
            toCancel = this.pending.Values.ToList();
            this.pending.Clear();
        }

        for( int i = 0; i < toCancel.Count; i++ ) {
            try {
                toCancel[i].DisposeRegistrationOnce();
                toCancel[i].Completion.TrySetException(new ObjectDisposedException(nameof(InMemoryInteractionBroker), DISPOSED_MESSAGE));
            } catch {
            }
        }

        try {
            this.activeRequestsSink.OnNext(Array.Empty<IInteractionRequest>());
            this.activeRequestsSink.OnCompleted();
        } catch {
        }

        try {
            this.requests.OnCompleted();
        } catch {
        }

        this.activeRequestsSubject.Dispose();
        this.requestsSubject.Dispose();
    }

    private void ThrowIfDisposed() {
        if( this.disposed )
            throw new ObjectDisposedException(nameof(InMemoryInteractionBroker), DISPOSED_MESSAGE);
    }

    private IReadOnlyList<IInteractionRequest> BuildActiveRequestsSnapshot_NoLock() {
        return this.pending.Values.Select(x => x.Request).ToArray();
    }

    private IObservable<IInteractionRequest> CreateReplayableRequestsObservable() {
        return Observable.Create<IInteractionRequest>(observer => {
            if( observer is null ) throw new ArgumentNullException(nameof(observer));

            IObserver<IInteractionRequest> sink = Observer.Synchronize(observer);

            StoredRequest[] snapshot;
            long cutoff;
            bool completed;

            object bufferGate = new();
            bool replaying = true;
            Queue<StoredRequest> buffer = new();
            bool bufferedCompleted = false;
            Exception? bufferedError = null;

            IDisposable? liveSubscription = null;

            lock( this.gate ) {
                snapshot = this.requestLog.ToArray();
                cutoff = this.lastSequence;
                completed = this.disposed;

                if( !completed ) {
                    liveSubscription = this.requests.Where(x => x.Sequence > cutoff)
                                           .Subscribe(x => {
                                               lock( bufferGate ) {
                                                   if( replaying ) buffer.Enqueue(x);
                                                   else sink.OnNext(x.Request);
                                               }
                                           }, ex => {
                                               lock( bufferGate ) {
                                                   if( replaying ) bufferedError = ex;
                                                   else sink.OnError(ex);
                                               }
                                           }, () => {
                                               lock( bufferGate ) {
                                                   if( replaying ) bufferedCompleted = true;
                                                   else sink.OnCompleted();
                                               }
                                           });
                }
            }

            for( int i = 0; i < snapshot.Length; i++ )
                sink.OnNext(snapshot[i].Request);

            lock( bufferGate ) {
                replaying = false;

                while( buffer.Count > 0 )
                    sink.OnNext(buffer.Dequeue().Request);

                if( bufferedError is not null ) {
                    sink.OnError(bufferedError);
                } else if( bufferedCompleted ) {
                    sink.OnCompleted();
                }
            }

            if( completed ) {
                liveSubscription?.Dispose();
                sink.OnCompleted();
                return Disposable.Empty;
            }

            return Disposable.Create(() => liveSubscription?.Dispose());
        });
    }

    private sealed class PendingRequest {
        private int registrationDisposed;
        private CancellationTokenRegistration registration;

        public PendingRequest(IInteractionRequest request, TaskCompletionSource<PendingRequestCompletion> completion, CancellationTokenRegistration registration) {
            Request = request ?? throw new ArgumentNullException(nameof(request));
            Completion = completion ?? throw new ArgumentNullException(nameof(completion));
            ResponseType = request.ResponseType;
            this.registration = registration;
        }

        public IInteractionRequest Request { get; }
        public Type ResponseType { get; }
        public TaskCompletionSource<PendingRequestCompletion> Completion { get; }

        public void DisposeRegistrationOnce() {
            if( Interlocked.Exchange(ref this.registrationDisposed, 1) != 0 )
                return;

            try {
                this.registration.Dispose();
            } catch {
            }
        }
    }

    private enum PendingRequestCompletionKind {
        Accepted,
        Rejected,
    }

    private readonly struct PendingRequestCompletion {
        private PendingRequestCompletion(PendingRequestCompletionKind kind, object response, string reason) {
            Kind = kind;
            Response = response;
            Reason = reason ?? string.Empty;
        }

        public PendingRequestCompletionKind Kind { get; }
        public object Response { get; }
        public string Reason { get; }

        public static PendingRequestCompletion Accepted(object response) {
            return new PendingRequestCompletion(PendingRequestCompletionKind.Accepted, response, string.Empty);
        }

        public static PendingRequestCompletion Rejected(string reason) {
            return new PendingRequestCompletion(PendingRequestCompletionKind.Rejected, null!, reason ?? string.Empty);
        }
    }

    private readonly struct StoredRequest {
        public StoredRequest(long sequence, IInteractionRequest request) {
            Sequence = sequence;
            Request = request ?? throw new ArgumentNullException(nameof(request));
        }

        public long Sequence { get; }
        public IInteractionRequest Request { get; }
    }

    private sealed class RequestLogView : IReadOnlyList<IInteractionRequest> {
        private readonly CircularBuffer<StoredRequest> source;
        private readonly object gate;

        public RequestLogView(CircularBuffer<StoredRequest> source, object gate) {
            this.source = source ?? throw new ArgumentNullException(nameof(source));
            this.gate = gate ?? throw new ArgumentNullException(nameof(gate));
        }

        public int Count {
            get {
                lock( this.gate ) {
                    return this.source.Count;
                }
            }
        }

        public IInteractionRequest this[int index] {
            get {
                lock( this.gate ) {
                    return this.source[index].Request;
                }
            }
        }

        public IEnumerator<IInteractionRequest> GetEnumerator() {
            StoredRequest[] snapshot;
            lock( this.gate ) {
                snapshot = this.source.ToArray();
            }

            for( int i = 0; i < snapshot.Length; i++ )
                yield return snapshot[i].Request;
        }

        IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();
    }
}
