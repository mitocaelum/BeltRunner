# PhaseTelemetryExtensionsTests

- Source: `Phase/PhaseTelemetryExtensionsTests.cs`
- Namespace: `BeltRunner.TEST.Core.Phase`
- Generated from XML documentation comments.

## PhaseTelemetryExtensionsTests

- Kind: Type
- Signature: `public sealed class PhaseTelemetryExtensionsTests {`

### Summary

Verifies the convenience telemetry APIs defined by `PhaseTelemetryExtensions`.

### Remarks

Purpose: Protect the extension methods that translate unit-centric calls into telemetry contract operations.



Why this matters: These helpers are intended to make phase code simpler, but they must preserve the exact identifiers and severities required by the runtime.



Expected result: Unit status, progress, and diagnostic helpers forward the expected values to the telemetry sink.

## StartUnit_ReportUnitProgress_AndCompleteUnit_UseUnitIdentifier

- Kind: Member
- Signature: `public void StartUnit_ReportUnitProgress_AndCompleteUnit_UseUnitIdentifier() {`

### Summary

Verifies that the unit lifecycle helpers use the runtime unit identifier.

### Remarks

Purpose: Confirm that unit-based helper methods route through the same identifier used by core telemetry APIs.



Why this matters: A mismatched identifier would corrupt progress tracking and unit status reporting.



Expected result: Starting, reporting progress for, and completing a unit emit the expected unit identifier and ratios.

## SkipUnit_AndFailUnit_WriteExpectedStatuses

- Kind: Member
- Signature: `public void SkipUnit_AndFailUnit_WriteExpectedStatuses() {`

### Summary

Verifies that skip and fail helpers publish the expected terminal statuses.

### Remarks

Purpose: Protect the mapping from convenience helpers to terminal unit states.



Why this matters: Incorrect status forwarding would make dashboards and downstream logic report the wrong outcome.



Expected result: Skipping a unit publishes `UnitStatus.Skipped`, and failing a unit publishes `UnitStatus.Failed`.

## Info_Warn_AndError_PublishExpectedDiagnostics

- Kind: Member
- Signature: `public void Info_Warn_AndError_PublishExpectedDiagnostics() {`

### Summary

Verifies that diagnostic helpers publish the expected severity, message, exception, and unit association.

### Remarks

Purpose: Define the forwarding contract for informational, warning, and error diagnostics.



Why this matters: Diagnostics lose value quickly if severity or associated unit context is dropped.



Expected result: Each helper writes one diagnostic entry with the expected severity, text, exception, and unit identifier.


