# InteractionRequestLogRetentionTests

- Source: `Host/InteractionRequestLogRetentionTests.cs`
- Namespace: `BeltRunner.TEST.Core.Host`
- Generated from XML documentation comments.

## InteractionRequestLogRetentionTests

- Kind: Type
- Signature: `public sealed class InteractionRequestLogRetentionTests {`

### Summary

Verifies configurable retention behavior for the interaction request log.

### Remarks

Purpose: Confirm that host options can cap in-memory interaction request history without changing the default behavior.



Why this matters: Unbounded retained requests make late-subscriber replay and request history vulnerable to memory growth.



Expected result: Null keeps all requests, a positive limit keeps only the newest requests, and invalid limits are rejected.

## Host_StartAsync_WithoutInteractionRequestRetentionLimit_RetainsFullRequestLog

- Kind: Member
- Signature: `public async Task Host_StartAsync_WithoutInteractionRequestRetentionLimit_RetainsFullRequestLog() {`

### Summary

Verifies that the default host configuration keeps the full interaction request history.

## Host_StartAsync_WithInteractionRequestRetentionLimit_RetainsNewestRequestsOnly

- Kind: Member
- Signature: `public async Task Host_StartAsync_WithInteractionRequestRetentionLimit_RetainsNewestRequestsOnly() {`

### Summary

Verifies that a configured retention limit keeps only the newest request log entries and replay requests.

## HostOptions_InteractionRequestLogMaxRetainedCount_WithNonPositiveValue_Throws

- Kind: Member
- Signature: `public void HostOptions_InteractionRequestLogMaxRetainedCount_WithNonPositiveValue_Throws() {`

### Summary

Verifies that non-positive interaction request retention limits are rejected.


