# NLogIntegrationTests

- Source: `Host/NLogIntegrationTests.cs`
- Namespace: `BeltRunner.TEST.Core.Host`
- Generated from XML documentation comments.

## NLogIntegrationTests

- Kind: Type
- Signature: `public sealed class NLogIntegrationTests {`

### Summary

Verifies that BeltRunner emits internal framework logs through NLog without owning the application's configuration lifecycle.

### Remarks

Purpose: Protect the contract that BeltRunner logs only when the application provides matching NLog rules.



Why this matters: Framework logging must be useful when configured, silent when not configured, and must not duplicate phase fault exceptions.



Expected result: Matching NLog rules capture run and telemetry logs, missing or non-matching rules capture nothing, and a phase fault is logged only once.

## SetUp

- Kind: Member
- Signature: `public void SetUp() {`

### Summary

Captures the current global NLog state before each test and resets the active configuration.

## TearDown

- Kind: Member
- Signature: `public void TearDown() {`

### Summary

Restores the global NLog state after each test.

## Host_StartAsync_WithoutNLogConfiguration_CompletesWithoutWritingToDetachedTarget

- Kind: Member
- Signature: `public async Task Host_StartAsync_WithoutNLogConfiguration_CompletesWithoutWritingToDetachedTarget() {`

### Summary

Verifies that BeltRunner completes normally when no NLog configuration is active.

## Host_StartAsync_WithBeltRunnerRule_WritesRunLifecycleLogs

- Kind: Member
- Signature: `public async Task Host_StartAsync_WithBeltRunnerRule_WritesRunLifecycleLogs() {`

### Summary

Verifies that matching BeltRunner logger rules capture run lifecycle events.

## Host_StartAsync_WithTelemetryDiagnostics_WritesWarningAndErrorLogs

- Kind: Member
- Signature: `public async Task Host_StartAsync_WithTelemetryDiagnostics_WritesWarningAndErrorLogs() {`

### Summary

Verifies that telemetry diagnostics are emitted with severity and correlation properties.

## Host_StartAsync_WhenPhaseThrows_LogsSingleFaultEntry_AndKeepsRawExceptionInNLogOnly

- Kind: Member
- Signature: `public async Task Host_StartAsync_WhenPhaseThrows_LogsSingleFaultEntry_AndKeepsRawExceptionInNLogOnly() {`

### Summary

Verifies that a phase fault is logged once even though the run also faults with the same exception.

## Host_StartAsync_WithNonMatchingRule_DoesNotWriteLogs

- Kind: Member
- Signature: `public async Task Host_StartAsync_WithNonMatchingRule_DoesNotWriteLogs() {`

### Summary

Verifies that non-matching logger rules do not capture BeltRunner logs.


