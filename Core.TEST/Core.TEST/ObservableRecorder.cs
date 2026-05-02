namespace BeltRunner.Core.TEST;

/// <summary>
/// Represents the terminal state observed from a subscribed observable sequence.
/// </summary>
/// <remarks>
/// <para>Purpose: Give tests a simple value that distinguishes normal completion from terminal failure.</para>
/// <para>Why this matters: Many lifecycle tests assert not only that a stream ended, but also how it ended.</para>
/// <para>Expected result: Consumers can use this enum to verify whether an observable completed successfully or faulted.</para>
/// </remarks>
internal enum TerminalSignal {
    Completed,
    Error
}

/// <summary>
/// Provides shared timeout values for asynchronous tests in this project.
/// </summary>
/// <remarks>
/// <para>Purpose: Centralize timeout selection so that asynchronous test behavior stays consistent.</para>
/// <para>Why this matters: Duplicated timeout literals make test tuning harder and invite inconsistent expectations.</para>
/// <para>Expected result: Tests use the same default wait budget unless a scenario explicitly needs a different value.</para>
/// </remarks>
internal static class TestTimeouts {
    /// <summary>
    /// Gets the default timeout used when a test waits for an asynchronous result.
    /// </summary>
    public static readonly TimeSpan Default = TimeSpan.FromSeconds(3);
}

/// <summary>
/// Records values and terminal signals from an observable source for test assertions.
/// </summary>
/// <remarks>
/// <para>Purpose: Reduce repetitive observer plumbing in tests that verify event and snapshot streams.</para>
/// <para>Why this matters: A dedicated recorder keeps the tests focused on behavior instead of subscription mechanics.</para>
/// <para>Expected result: Tests can inspect emitted items, terminal notifications, and terminal errors after subscribing.</para>
/// </remarks>
internal sealed class ObservableRecorder<T> : IObserver<T>, IDisposable {
    private readonly TaskCompletionSource<TerminalSignal> terminalTcs = new(TaskCreationOptions.RunContinuationsAsynchronously);
    private readonly List<T> items = new();
    private readonly IDisposable subscription;
    private int terminalCount;

    /// <summary>
    /// Initializes a new instance of the <see cref="ObservableRecorder{T}"/> class and subscribes to the provided source.
    /// </summary>
    /// <param name="source">The observable sequence to record.</param>
    public ObservableRecorder(IObservable<T> source) {
        if( source is null ) throw new ArgumentNullException(nameof(source));
        this.subscription = source.Subscribe(this);
    }

    /// <summary>
    /// Gets the values received from the observable source in subscription order.
    /// </summary>
    public IReadOnlyList<T> Items => this.items;

    /// <summary>
    /// Gets a value indicating whether the source completed successfully.
    /// </summary>
    public bool IsCompleted { get; private set; }

    /// <summary>
    /// Gets the terminal error observed from the source, if one was reported.
    /// </summary>
    public Exception? Error { get; private set; }

    /// <summary>
    /// Gets the number of terminal notifications that have been observed.
    /// </summary>
    public int TerminalCount => Volatile.Read(ref this.terminalCount);

    /// <summary>
    /// Records the next value emitted by the observable source.
    /// </summary>
    /// <param name="value">The value emitted by the source.</param>
    public void OnNext(T value) {
        this.items.Add(value);
    }

    /// <summary>
    /// Records the terminal error emitted by the observable source.
    /// </summary>
    /// <param name="error">The terminal error reported by the source.</param>
    public void OnError(Exception error) {
        this.Error = error ?? throw new ArgumentNullException(nameof(error));
        Interlocked.Increment(ref this.terminalCount);
        this.terminalTcs.TrySetResult(TerminalSignal.Error);
    }

    /// <summary>
    /// Records successful completion of the observable source.
    /// </summary>
    public void OnCompleted() {
        this.IsCompleted = true;
        Interlocked.Increment(ref this.terminalCount);
        this.terminalTcs.TrySetResult(TerminalSignal.Completed);
    }

    /// <summary>
    /// Waits until the source produces a terminal signal or the timeout expires.
    /// </summary>
    /// <param name="timeout">The maximum amount of time to wait.</param>
    /// <returns>A task that completes with the observed terminal signal.</returns>
    public Task<TerminalSignal> WaitForTerminalAsync(TimeSpan timeout) {
        return this.terminalTcs.Task.WaitAsync(timeout);
    }

    /// <summary>
    /// Disposes the underlying subscription.
    /// </summary>
    public void Dispose() {
        this.subscription.Dispose();
    }
}
