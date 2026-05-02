# ObservableRecorder

- Source: `ObservableRecorder.cs`
- Namespace: `BeltRunner.TEST.Core`
- Generated from XML documentation comments.

## TerminalSignal

- Kind: Type
- Signature: `internal enum TerminalSignal {`

### Summary

Represents the terminal state observed from a subscribed observable sequence.

### Remarks

Purpose: Give tests a simple value that distinguishes normal completion from terminal failure.



Why this matters: Many lifecycle tests assert not only that a stream ended, but also how it ended.



Expected result: Consumers can use this enum to verify whether an observable completed successfully or faulted.

## TestTimeouts

- Kind: Type
- Signature: `internal static class TestTimeouts {`

### Summary

Provides shared timeout values for asynchronous tests in this project.

### Remarks

Purpose: Centralize timeout selection so that asynchronous test behavior stays consistent.



Why this matters: Duplicated timeout literals make test tuning harder and invite inconsistent expectations.



Expected result: Tests use the same default wait budget unless a scenario explicitly needs a different value.

## FromSeconds

- Kind: Member
- Signature: `public static readonly TimeSpan Default = TimeSpan.FromSeconds(3);`

### Summary

Gets the default timeout used when a test waits for an asynchronous result.

## ObservableRecorder<T>

- Kind: Type
- Signature: `internal sealed class ObservableRecorder<T> : IObserver<T>, IDisposable {`

### Summary

Records values and terminal signals from an observable source for test assertions.

### Remarks

Purpose: Reduce repetitive observer plumbing in tests that verify event and snapshot streams.



Why this matters: A dedicated recorder keeps the tests focused on behavior instead of subscription mechanics.



Expected result: Tests can inspect emitted items, terminal notifications, and terminal errors after subscribing.

## ObservableRecorder

- Kind: Member
- Signature: `public ObservableRecorder(IObservable<T> source) {`

### Summary

Initializes a new instance of the `ObservableRecorder{T}` class and subscribes to the provided source.

### Parameters

- `source`: The observable sequence to record.

## public IReadOnlyList<T> Items => this.items;

- Kind: Member
- Signature: `public IReadOnlyList<T> Items => this.items;`

### Summary

Gets the values received from the observable source in subscription order.

## public bool IsCompleted { get; private set; }

- Kind: Member
- Signature: `public bool IsCompleted { get; private set; }`

### Summary

Gets a value indicating whether the source completed successfully.

## public Exception? Error { get; private set; }

- Kind: Member
- Signature: `public Exception? Error { get; private set; }`

### Summary

Gets the terminal error observed from the source, if one was reported.

## Read

- Kind: Member
- Signature: `public int TerminalCount => Volatile.Read(ref this.terminalCount);`

### Summary

Gets the number of terminal notifications that have been observed.

## OnNext

- Kind: Member
- Signature: `public void OnNext(T value) {`

### Summary

Records the next value emitted by the observable source.

### Value

The value emitted by the source.

### Parameters

- `value`: The value emitted by the source.

## OnError

- Kind: Member
- Signature: `public void OnError(Exception error) {`

### Summary

Records the terminal error emitted by the observable source.

### Parameters

- `error`: The terminal error reported by the source.

## OnCompleted

- Kind: Member
- Signature: `public void OnCompleted() {`

### Summary

Records successful completion of the observable source.

## WaitForTerminalAsync

- Kind: Member
- Signature: `public Task<TerminalSignal> WaitForTerminalAsync(TimeSpan timeout) {`

### Summary

Waits until the source produces a terminal signal or the timeout expires.

### Expected Result

A task that completes with the observed terminal signal.

### Parameters

- `timeout`: The maximum amount of time to wait.

## Dispose

- Kind: Member
- Signature: `public void Dispose() {`

### Summary

Disposes the underlying subscription.


