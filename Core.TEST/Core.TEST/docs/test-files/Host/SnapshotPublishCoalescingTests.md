# SnapshotPublishCoalescingTests

- Source: `Host/SnapshotPublishCoalescingTests.cs`
- Namespace: `BeltRunner.TEST.Core.Host`
- Generated from XML documentation comments.

## SnapshotPublishCoalescingTests

- Kind: Type
- Signature: `public sealed class SnapshotPublishCoalescingTests {`

### Summary

Verifies host-configured snapshot publish coalescing behavior.

### Remarks

Purpose: Protect the host option that throttles high-frequency snapshot publications.



Why this matters: Telemetry-heavy phases can otherwise force snapshot rebuilds and publications at an unsustainable rate.



Expected result: The option defaults to zero, zero disables coalescing, and a positive interval coalesces rapid telemetry updates into a single publication window.

## HostOptions_SnapshotPublishCoalescingInterval_DefaultsToZero_AndZeroDisablesCoalescing

- Kind: Member
- Signature: `public void HostOptions_SnapshotPublishCoalescingInterval_DefaultsToZero_AndZeroDisablesCoalescing() {`

### Summary

Verifies that the host option defaults to zero and leaves zero as the disabled-coalescing value.

## HostOptions_SnapshotPublishCoalescingInterval_WithNegativeValue_Throws

- Kind: Member
- Signature: `public void HostOptions_SnapshotPublishCoalescingInterval_WithNegativeValue_Throws() {`

### Summary

Verifies that negative coalescing intervals are rejected.

## Host_StartAsync_WithSnapshotPublishCoalescingInterval_CoalescesTelemetrySnapshotPublications

- Kind: Member
- Signature: `public async Task Host_StartAsync_WithSnapshotPublishCoalescingInterval_CoalescesTelemetrySnapshotPublications() {`

### Summary

Verifies that the host coalesces high-frequency telemetry snapshot publications when configured.


