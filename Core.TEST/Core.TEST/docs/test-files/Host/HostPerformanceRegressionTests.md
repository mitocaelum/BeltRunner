# HostPerformanceRegressionTests

- Source: `Host/HostPerformanceRegressionTests.cs`
- Namespace: `BeltRunner.TEST.Core.Host`
- Generated from XML documentation comments.

## HostPerformanceRegressionTests

- Kind: Type
- Signature: `public sealed class HostPerformanceRegressionTests {`

### Summary

Verifies high-volume execution scenarios that protect against avoidable in-memory performance regressions.

### Remarks

Purpose: Keep the host stable when telemetry, event history, or diagnostic volume increases sharply.



Why this matters: Performance regressions often first appear as runaway publication counts or retained history that grows beyond configured bounds.



Expected result: Snapshot coalescing suppresses bursty telemetry publication, and configured retention limits cap replayable event and diagnostic history under heavier loads.

## Host_StartAsync_WithSnapshotPublishCoalescingInterval_DuringTelemetryBurst_PublishesFarFewerSnapshots

- Kind: Member
- Signature: `public async Task Host_StartAsync_WithSnapshotPublishCoalescingInterval_DuringTelemetryBurst_PublishesFarFewerSnapshots() {`

### Summary

Verifies that snapshot coalescing suppresses publication growth during a dense telemetry burst.

### Remarks

Purpose: Protect the coalescing option from degrading into near one-publication-per-update behavior.



Why this matters: Telemetry-heavy phases can otherwise overwhelm observers with snapshot churn and unnecessary allocations.



Expected result: A coalesced run completes with the same final state as an uncoalesced run while emitting dramatically fewer snapshots.

## Host_StartAsync_WithRunEventRetentionLimit_DuringLargePlan_RetainsOnlyNewestReplayableEvents

- Kind: Member
- Signature: `public async Task Host_StartAsync_WithRunEventRetentionLimit_DuringLargePlan_RetainsOnlyNewestReplayableEvents() {`

### Summary

Verifies that a tight event-log retention limit still caps replayable history during a larger multi-phase run.

### Remarks

Purpose: Protect event replay cost when plans contain many short-lived phases.



Why this matters: A retention regression can quietly turn a long but simple run into an ever-growing in-memory history.



Expected result: Only the newest retained events remain in both the event log and late-subscriber replay stream.

## Host_StartAsync_WithDiagnosticRetentionLimit_DuringDiagnosticBurst_RetainsOnlyNewestReplayableDiagnostics

- Kind: Member
- Signature: `public async Task Host_StartAsync_WithDiagnosticRetentionLimit_DuringDiagnosticBurst_RetainsOnlyNewestReplayableDiagnostics() {`

### Summary

Verifies that diagnostic retention remains bounded when a phase emits a large warning burst.

### Remarks

Purpose: Protect diagnostic replay and retained memory usage under diagnostic-heavy execution.



Why this matters: Warning storms are a common source of accidental retention growth even when execution still succeeds.



Expected result: The run retains and replays only the newest configured diagnostics after a larger diagnostic burst.


