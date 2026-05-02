# RunTerminalPolicyTests

- Source: `Execution/RunTerminalPolicyTests.cs`
- Namespace: `BeltRunner.TEST.Core.Execution`
- Generated from XML documentation comments.

## RunTerminalPolicyTests

- Kind: Type
- Signature: `public sealed class RunTerminalPolicyTests {`

### Summary

Verifies terminal behavior for `Run` when disposal is initiated by the owner.

### Remarks

Purpose: Protect the shutdown contract for event streams, snapshot streams, and completion state.



Why this matters: Disposal often happens in cleanup paths, and those paths must settle the run consistently without leaking faults.



Expected result: Disposing a run produces a stable cancelled outcome, emits one terminal notification per stream, and tolerates callback failures.

## Dispose_BeforeSettlement_CompletesAsCancelledOutcome

- Kind: Member
- Signature: `public async Task Dispose_BeforeSettlement_CompletesAsCancelledOutcome() {`

### Summary

Verifies that disposing an unsettled run completes it with a cancelled outcome.

### Remarks

Purpose: Define how a run settles when ownership ends before normal completion.



Why this matters: Cleanup logic needs a deterministic outcome instead of a hanging task or an unobserved fault.



Expected result: The completion task finishes successfully with `CancelledOutcome`, and cancellation state is visible on the run.

## CancelReason_BeforeCancellation_IsNull

- Kind: Member
- Signature: `public void CancelReason_BeforeCancellation_IsNull() {`

### Summary

Verifies that a newly created run does not expose a cancellation reason before cancellation is requested.

## RequestCancellation_WithoutReason_LeavesCancelReasonNull

- Kind: Member
- Signature: `public void RequestCancellation_WithoutReason_LeavesCancelReasonNull() {`

### Summary

Verifies that requesting cancellation without a reason keeps `Run.CancelReason` unset.

## RequestCancellation_WithExplicitEmptyReason_PreservesEmptyString

- Kind: Member
- Signature: `public void RequestCancellation_WithExplicitEmptyReason_PreservesEmptyString() {`

### Summary

Verifies that an explicitly empty cancellation reason is preserved distinctly from an unset reason.

## Events_Stream_Completes_OnDispose_WithoutError

- Kind: Member
- Signature: `public async Task Events_Stream_Completes_OnDispose_WithoutError() {`

### Summary

Verifies that the event stream completes once without reporting an error during disposal.

### Remarks

Purpose: Protect the terminal semantics of `Run.EventStream` during shutdown.



Why this matters: Observers must be able to treat disposal as a clean end-of-stream condition.



Expected result: The recorder observes a single completed terminal signal, no error, and no duplicate terminal notifications.

## Snapshots_Stream_Completes_OnDispose_WithoutError

- Kind: Member
- Signature: `public async Task Snapshots_Stream_Completes_OnDispose_WithoutError() {`

### Summary

Verifies that the snapshot stream completes once without reporting an error during disposal.

### Remarks

Purpose: Ensure that snapshot subscribers receive a clean terminal signal when the run is disposed.



Why this matters: Tooling that renders snapshots should not have to special-case disposal as a stream fault.



Expected result: The snapshot recorder completes successfully, reports no error, and retains emitted items for inspection.

## Dispose_CalledTwice_EmitsSingleTerminalSignal_AndCancelledCompletion

- Kind: Member
- Signature: `public async Task Dispose_CalledTwice_EmitsSingleTerminalSignal_AndCancelledCompletion() {`

### Summary

Verifies that repeated disposal still produces only one terminal notification and one cancelled completion outcome.

### Remarks

Purpose: Define disposal idempotency for terminal signaling.



Why this matters: Cleanup code may call `IDisposable.Dispose` more than once, and the run should remain stable.



Expected result: The stream completes exactly once and the run still settles as a cancelled outcome without faulting completion.

## Dispose_Invokes_BeforeRunDisposeAsync_Once_BeforeTerminalSignals

- Kind: Member
- Signature: `public async Task Dispose_Invokes_BeforeRunDisposeAsync_Once_BeforeTerminalSignals() {`

### Summary

Verifies that the disposal lifecycle callback runs before completion and stream terminal signals are published.

### Remarks

Purpose: Protect callback timing for integrations that need access to a still-live run during disposal.



Why this matters: If callbacks run too late, cleanup extensions cannot inspect state before it is finalized.



Expected result: `BeforeRunDisposeAsync` is invoked exactly once while completion and events are still unsettled.

## Dispose_WhenBeforeRunDisposeAsyncThrows_SwallowsAndContinues

- Kind: Member
- Signature: `public async Task Dispose_WhenBeforeRunDisposeAsyncThrows_SwallowsAndContinues() {`

### Summary

Verifies that disposal continues even when the disposal callback throws.

### Remarks

Purpose: Define the fault-tolerance policy for disposal callbacks.



Why this matters: Cleanup hooks must not be able to strand the run in an incomplete state.



Expected result: Disposal does not throw to the caller, terminal signals still complete normally, and the run still settles as cancelled.


