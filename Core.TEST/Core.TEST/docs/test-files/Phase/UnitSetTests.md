# UnitSetTests

- Source: `Phase/UnitSetTests.cs`
- Namespace: `BeltRunner.TEST.Core.Phase`
- Generated from XML documentation comments.

## UnitSetTests

- Kind: Type
- Signature: `public sealed class UnitSetTests {`

### Summary

Verifies collection and locking behavior in `UnitSet`.

### Remarks

Purpose: Protect the runtime unit collection against duplicate identifiers, incorrect event behavior, and unintended mutability.



Why this matters: Unit tracking is foundational for progress, snapshots, and telemetry, so collection semantics must remain predictable.



Expected result: Bulk addition locks the set correctly, duplicate additions are rejected safely, and invalid duplicate batches leave the set unchanged.

## AddRangeAndLock_AddsUnits_LocksSet_AndRaisesChangedOnce

- Kind: Member
- Signature: `public void AddRangeAndLock_AddsUnits_LocksSet_AndRaisesChangedOnce() {`

### Summary

Verifies that adding a range and locking the set updates membership and raises the changed event once.

### Remarks

Purpose: Define the combined behavior of bulk addition and locking.



Why this matters: Runtime phases may finalize discovered units in one operation, and observers need a stable single notification.



Expected result: The units are present, the set is locked, and the changed event fires exactly once.

## TryAdd_WhenDuplicateIdExists_ReturnsFalse_AndDoesNotRaiseChanged

- Kind: Member
- Signature: `public void TryAdd_WhenDuplicateIdExists_ReturnsFalse_AndDoesNotRaiseChanged() {`

### Summary

Verifies that adding a duplicate unit identifier with `TryAdd` fails without raising a changed event.

### Remarks

Purpose: Protect the non-throwing duplicate handling path.



Why this matters: Duplicate discovery can happen in caller code, and the collection should reject it without pretending that state changed.



Expected result: The duplicate is rejected, the original membership remains intact, and the changed event count does not increase.

## AddRange_WhenInputContainsDuplicateIds_Throws_AndLeavesSetUnchanged

- Kind: Member
- Signature: `public void AddRange_WhenInputContainsDuplicateIds_Throws_AndLeavesSetUnchanged() {`

### Summary

Verifies that a duplicate identifier inside a bulk addition throws and leaves the set unchanged.

### Remarks

Purpose: Define the failure behavior for invalid bulk additions.



Why this matters: Partially applied duplicate batches would make runtime state hard to reason about and harder to recover from.



Expected result: An `InvalidOperationException` is thrown, and the set remains empty and unlocked.


