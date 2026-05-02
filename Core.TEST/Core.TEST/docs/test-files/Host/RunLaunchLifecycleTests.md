# RunLaunchLifecycleTests

- Source: `Host/RunLaunchLifecycleTests.cs`
- Namespace: `BeltRunner.TEST.Core.Host`
- Generated from XML documentation comments.

## RunLaunchLifecycleTests

- Kind: Type
- Signature: `public sealed class RunLaunchLifecycleTests {`

### Summary

Verifies launch lifecycle behavior for the host entry point.

### Remarks

Purpose: Protect how the host initializes a run, invokes lifecycle hooks, and passes runtime context into phase execution.



Why this matters: Launch sequencing is integration-heavy, and a small ordering bug can leave extensions or seeded artifacts invisible at exactly the wrong time.



Expected result: The host exposes initialized run state before execution begins, fails early when the launch hook fails, and preserves the expected phase key in execution context.

## Host_StartAsync_BeforeExecutionStartAsync_CanObserveInitializedRunBeforeFirstEvent

- Kind: Member
- Signature: `public async Task Host_StartAsync_BeforeExecutionStartAsync_CanObserveInitializedRunBeforeFirstEvent() {`

### Summary

Verifies that the pre-execution lifecycle hook can inspect an initialized run before the first event is published.

### Remarks

Purpose: Define what state is available to `BeforeExecutionStartAsync`.



Why this matters: Extensions often need access to seeded artifacts and snapshot structure before execution mutates the run.



Expected result: The hook sees an initialized run with seeded artifacts and phase snapshots, while the event stream has not yet published `RunStartedEvent`.

## Host_StartAsync_WhenBeforeExecutionStartAsyncThrows_FailsBeforeExecutionAndReturnsToIdle

- Kind: Member
- Signature: `public async Task Host_StartAsync_WhenBeforeExecutionStartAsyncThrows_FailsBeforeExecutionAndReturnsToIdle() {`

### Summary

Verifies that a failing pre-execution lifecycle hook aborts startup before any phase executes.

### Remarks

Purpose: Protect the failure policy for `BeforeExecutionStartAsync`.



Why this matters: Startup hooks should be able to block execution when required preconditions are not met.



Expected result: The exception is surfaced to the caller, no phase runs, and the host returns to the idle state.

## Host_StartAsync_PassesFactoryKey_ThroughPhaseContext

- Kind: Member
- Signature: `public async Task Host_StartAsync_PassesFactoryKey_ThroughPhaseContext() {`

### Summary

Verifies that the phase context receives the factory key used to build the plan.

### Remarks

Purpose: Ensure that launch-time plan metadata survives into phase execution.



Why this matters: Diagnostics, telemetry, and artifact scoping rely on the correct phase key at runtime.



Expected result: The executing phase observes the same key that was assigned by the phase factory in the plan.


