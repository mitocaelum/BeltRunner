# PublicFaultSurfaceTests

- Source: `Host/PublicFaultSurfaceTests.cs`
- Namespace: `BeltRunner.TEST.Core.Host`
- Generated from XML documentation comments.

## PublicFaultSurfaceTests

- Kind: Type
- Signature: `public sealed class PublicFaultSurfaceTests {`

### Summary

Verifies that public fault surfaces expose sanitized fault summaries instead of raw exception objects.

### Remarks

Purpose: Protect public runtime contracts from leaking raw exception instances.



Why this matters: Stack traces, exception data, and framework-specific exception graphs can reveal internal details.



Expected result: Public diagnostics and fault events expose `PublicFaultInfo` values, while completion reports `ExceptionOutcome`.

## PublicFaultTypes_DoNotExposeRawExceptionDetails

- Kind: Member
- Signature: `public void PublicFaultTypes_DoNotExposeRawExceptionDetails() {`

### Summary

Verifies that the public fault projection types no longer expose raw exception details.

## IPhaseContext_ExposesNarrowedExecutionSurface

- Kind: Member
- Signature: `public void IPhaseContext_ExposesNarrowedExecutionSurface() {`

### Summary

Verifies that phase context exposes only the narrowed interaction and cancellation surface.

## Host_StartAsync_WhenPhaseThrows_ExposesSanitizedPublicFaultInfo

- Kind: Member
- Signature: `public async Task Host_StartAsync_WhenPhaseThrows_ExposesSanitizedPublicFaultInfo() {`

### Summary

Verifies that fault diagnostics and events expose sanitized fault summaries after a phase throws.

## Host_StartAsync_WhenPhaseThrows_EventStreamCompletesWithoutObservableError

- Kind: Member
- Signature: `public async Task Host_StartAsync_WhenPhaseThrows_EventStreamCompletesWithoutObservableError() {`

### Summary

Verifies that the run event stream completes instead of faulting when execution fails.


