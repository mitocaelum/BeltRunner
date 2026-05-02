# PhaseTelemetryIntegrationTests

- Source: `Host/PhaseTelemetryIntegrationTests.cs`
- Namespace: `BeltRunner.TEST.Core.Host`
- Generated from XML documentation comments.

## PhaseTelemetryIntegrationTests

- Kind: Type
- Signature: `public sealed class PhaseTelemetryIntegrationTests {`

### Summary

Verifies end-to-end telemetry propagation from a phase into runtime snapshots and unit state.

### Remarks

Purpose: Protect the integration boundary between `IPhaseContext`, `IPhaseTelemetry`, and snapshot projection.



Why this matters: Telemetry is only useful if runtime state, diagnostics, and unit status all agree after execution completes.



Expected result: Telemetry updates populate run diagnostics, finalize unit state, and preserve the phase key observed during execution.

## Host_StartAsync_PhaseTelemetry_UpdatesSnapshotDiagnostics_AndRuntimeUnitState

- Kind: Member
- Signature: `public async Task Host_StartAsync_PhaseTelemetry_UpdatesSnapshotDiagnostics_AndRuntimeUnitState() {`

### Summary

Verifies that phase telemetry updates run diagnostics and runtime unit state during host execution.

### Remarks

Purpose: Confirm that telemetry emitted from a phase reaches all runtime representations that a consumer would inspect.



Why this matters: A broken connection between telemetry and snapshots would make debugging and progress reporting misleading.



Expected result: The completed run exposes the expected phase key, completed unit state, full progress, and warning diagnostic in the run log.


