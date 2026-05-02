# RunSnapshotTests

- Source: `Execution/RunSnapshotTests.cs`
- Namespace: `BeltRunner.TEST.Core.Execution`
- Generated from XML documentation comments.

## RunSnapshotTests

- Kind: Type
- Signature: `public sealed class RunSnapshotTests {`

### Summary

Verifies how `Run` materializes runtime state into snapshots.

### Remarks

Purpose: Protect the observable snapshot model that consumers use to render run progress and runtime state.



Why this matters: Snapshots are a high-level integration surface, so subtle drift in status, unit counts, or interaction tracking can break tooling without obvious compiler errors.



Expected result: The snapshot graph stays synchronized with attached phases, unit telemetry, interaction state, and seeded artifacts.

## Snapshots_Replay_Latest_UnitTelemetryState

- Kind: Member
- Signature: `public void Snapshots_Replay_Latest_UnitTelemetryState() {`

### Summary

Verifies that the latest snapshot replays the most recent unit telemetry state.

### Remarks

Purpose: Confirm that telemetry updates are reflected in the replayable snapshot stream.



Why this matters: Consumers often subscribe after execution has started and still need an accurate current state.



Expected result: The latest replayed snapshot shows the running run, running phase, updated unit progress, and matching runtime unit state.

## Snapshots_Include_MixedUnitTypes_FromPhaseOwnedCollection

- Kind: Member
- Signature: `public void Snapshots_Include_MixedUnitTypes_FromPhaseOwnedCollection() {`

### Summary

Verifies that a phase snapshot includes mixed runtime unit types from the phase-owned unit collection.

### Remarks

Purpose: Protect snapshot population when a phase exposes more than one concrete unit implementation.



Why this matters: Snapshot projection must depend on the unit contract, not on a single concrete unit type.



Expected result: The phase snapshot includes both units and associates each unit with the attached phase key.

## Snapshot_Reflects_AttachedPhase_Immediately

- Kind: Member
- Signature: `public void Snapshot_Reflects_AttachedPhase_Immediately() {`

### Summary

Verifies that attaching a phase updates the snapshot immediately.

### Remarks

Purpose: Define the attachment contract for newly created runtime state.



Why this matters: Snapshot consumers should not have to wait for later execution events to discover attached units.



Expected result: The snapshot contains the attached phase and its unit as soon as the phase is attached.

## Snapshots_Grow_TotalUnits_When_NewUnits_AreDiscovered_DuringExecution

- Kind: Member
- Signature: `public void Snapshots_Grow_TotalUnits_When_NewUnits_AreDiscovered_DuringExecution() {`

### Summary

Verifies that discovering new units during execution increases the total unit count in snapshots.

### Remarks

Purpose: Protect dynamic unit discovery behavior in long-running phases.



Why this matters: Progress reporting becomes misleading if total work does not expand when additional units are introduced.



Expected result: Snapshot totals, processed counts, and ratios update when a new unit is added and again when it completes.

## UnitSet_PublicApi_DoesNotExposeRemovalOperations

- Kind: Member
- Signature: `public void UnitSet_PublicApi_DoesNotExposeRemovalOperations() {`

### Summary

Verifies that `UnitSet` does not expose public removal operations.

### Remarks

Purpose: Lock down the intended write model for runtime unit collections.



Why this matters: Allowing arbitrary removal would make snapshot and progress semantics much harder to reason about.



Expected result: Reflection does not find public remove or clear members on `UnitSet`.

## Run_Tracks_ActiveInteractions

- Kind: Member
- Signature: `public async Task Run_Tracks_ActiveInteractions() {`

### Summary

Verifies that active interaction requests appear on the run surface and are removed after resolution.

### Remarks

Purpose: Ensure that interactive runtime state is visible through the run surface.



Why this matters: User interfaces and automation tools need to detect pending prompts without inspecting broker internals.



Expected result: The interaction appears with its metadata while pending and disappears after a response is recorded.

## Artifacts_Property_Exposes_Seeded_Artifacts

- Kind: Member
- Signature: `public void Artifacts_Property_Exposes_Seeded_Artifacts() {`

### Summary

Verifies that seeded artifacts are exposed through the run artifact store.

### Remarks

Purpose: Protect artifact seeding as part of initial run setup.



Why this matters: Downstream phases and lifecycle hooks rely on seeded values being visible before execution starts.



Expected result: The seeded artifact can be detected and retrieved from `Run.Artifacts`.


