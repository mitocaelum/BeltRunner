# DefaultSequentialPlanExecutorTests

- Source: `Host/DefaultSequentialPlanExecutorTests.cs`
- Namespace: `BeltRunner.TEST.Core.Host`
- Generated from XML documentation comments.

## DefaultSequentialPlanExecutorTests

- Kind: Type
- Signature: `public sealed class DefaultSequentialPlanExecutorTests {`

### Summary

Verifies outcome mapping behavior in `DefaultSequentialPlanExecutor`.

### Remarks

Purpose: Protect how phase outcomes are translated into run outcomes and downstream execution decisions.



Why this matters: The sequential executor is the control point that decides whether the plan continues, halts, or changes the final run status.



Expected result: Partial success, failure, and cancellation reported by a phase are converted into the correct run outcome and execution flow.

## ExecuteAsync_WhenPhaseReportsPartiallySucceeded_CompletesRunAsPartiallySucceededOutcome

- Kind: Member
- Signature: `public async Task ExecuteAsync_WhenPhaseReportsPartiallySucceeded_CompletesRunAsPartiallySucceededOutcome() {`

### Summary

Verifies that a partially succeeded phase completes the run as partially succeeded while still allowing downstream execution.

### Remarks

Purpose: Define how partial success propagates through sequential execution.



Why this matters: Partial success is informative but not terminal, so the executor must preserve both the outcome and continued execution.



Expected result: The run finishes with `PartiallySucceededOutcome`, the downstream phase runs once, and the final run status is completed.

## ExecuteAsync_WhenPhaseFailsAndHalts_DoesNotRunDownstreamPhase

- Kind: Member
- Signature: `public async Task ExecuteAsync_WhenPhaseFailsAndHalts_DoesNotRunDownstreamPhase() {`

### Summary

Verifies that a failed phase configured to halt stops downstream execution.

### Remarks

Purpose: Protect the executor halt rule for terminal phase failures.



Why this matters: Running later phases after a halting failure can compound damage and hide the original error boundary.



Expected result: The run completes with `FailedOutcome`, the downstream phase does not execute, and the failure summary points at the first failing phase.

## ExecuteAsync_WhenPhaseReportsCancelled_CompletesRunAsCancelledOutcome

- Kind: Member
- Signature: `public async Task ExecuteAsync_WhenPhaseReportsCancelled_CompletesRunAsCancelledOutcome() {`

### Summary

Verifies that a cancelled phase completes the run as cancelled and stops downstream execution.

### Remarks

Purpose: Define cancellation propagation within sequential execution.



Why this matters: Cancellation should stop further work while still producing a stable and inspectable outcome.



Expected result: The run finishes with `CancelledOutcome`, the downstream phase does not execute, and the run status becomes cancelled.


