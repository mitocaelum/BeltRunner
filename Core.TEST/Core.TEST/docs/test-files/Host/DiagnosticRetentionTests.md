# DiagnosticRetentionTests

- Source: `Host/DiagnosticRetentionTests.cs`
- Namespace: `BeltRunner.TEST.Core.Host`
- Generated from XML documentation comments.

## DiagnosticRetentionTests

- Kind: Type
- Signature: `public sealed class DiagnosticRetentionTests {`

### Summary

Verifies configurable retention behavior for run diagnostics.

### Remarks

Purpose: Confirm that the host can cap diagnostic retention without changing the default behavior.



Why this matters: Unbounded diagnostics make retained runtime history grow over time and amplify replay costs during telemetry-heavy runs.



Expected result: Null keeps all diagnostics, positive limits keep only the newest diagnostics, mode filtering works, and invalid limits are rejected.

## Host_StartAsync_WithoutDiagnosticRetentionLimits_RetainsFullDiagnosticHistory

- Kind: Member
- Signature: `public async Task Host_StartAsync_WithoutDiagnosticRetentionLimits_RetainsFullDiagnosticHistory() {`

### Summary

Verifies that the default host configuration keeps the full diagnostic history in the run log.

## Host_StartAsync_WithDiagnosticRetentionLimits_RetainsNewestDiagnosticsOnly

- Kind: Member
- Signature: `public async Task Host_StartAsync_WithDiagnosticRetentionLimits_RetainsNewestDiagnosticsOnly() {`

### Summary

Verifies that configured retention limits keep only the newest diagnostics in the run log.

## Host_StartAsync_WithDiagnosticModeDisabled_EmitsNoDiagnostics

- Kind: Member
- Signature: `public async Task Host_StartAsync_WithDiagnosticModeDisabled_EmitsNoDiagnostics() {`

### Summary

Verifies that diagnostics can be disabled entirely.

## Host_StartAsync_WithDiagnosticModeErrorsOnly_RetainsOnlyErrors

- Kind: Member
- Signature: `public async Task Host_StartAsync_WithDiagnosticModeErrorsOnly_RetainsOnlyErrors() {`

### Summary

Verifies that the errors-only mode filters out information and warning diagnostics.

## HostOptions_DiagnosticRetentionLimits_WithNonPositiveValue_Throws

- Kind: Member
- Signature: `public void HostOptions_DiagnosticRetentionLimits_WithNonPositiveValue_Throws() {`

### Summary

Verifies that non-positive diagnostic retention limits are rejected.


