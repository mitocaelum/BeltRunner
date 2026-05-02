# RunEventLogRetentionTests

- Source: `Host/RunEventLogRetentionTests.cs`
- Namespace: `BeltRunner.TEST.Core.Host`
- Generated from XML documentation comments.

## RunEventLogRetentionTests

- Kind: Type
- Signature: `public sealed class RunEventLogRetentionTests {`

### Summary

Verifies configurable retention behavior for the run event log.

### Remarks

Purpose: Confirm that the host can cap in-memory run history without changing the default behavior.



Why this matters: Unbounded run history makes late-subscriber replay and retained event logs vulnerable to memory growth.



Expected result: Null keeps all events, a positive limit keeps only the newest events, and invalid limits are rejected.

## Host_StartAsync_WithoutRetentionLimit_RetainsFullEventLog

- Kind: Member
- Signature: `public async Task Host_StartAsync_WithoutRetentionLimit_RetainsFullEventLog() {`

### Summary

Verifies that the default host configuration keeps the full run event history.

## Host_StartAsync_WithRetentionLimit_RetainsNewestEventsOnly

- Kind: Member
- Signature: `public async Task Host_StartAsync_WithRetentionLimit_RetainsNewestEventsOnly() {`

### Summary

Verifies that a configured retention limit keeps only the newest event log entries and replay events.

## HostOptions_RunEventLogMaxRetainedCount_WithNonPositiveValue_Throws

- Kind: Member
- Signature: `public void HostOptions_RunEventLogMaxRetainedCount_WithNonPositiveValue_Throws() {`

### Summary

Verifies that non-positive retention limits are rejected.


