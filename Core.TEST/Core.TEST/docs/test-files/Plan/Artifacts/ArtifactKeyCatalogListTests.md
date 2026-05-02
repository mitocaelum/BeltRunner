# ArtifactKeyCatalogListTests

- Source: `Plan/Artifacts/ArtifactKeyCatalogListTests.cs`
- Namespace: `BeltRunner.TEST.Core.Plan.Artifacts`
- Generated from XML documentation comments.

## ArtifactKeyCatalogListTests

- Kind: Type
- Signature: `public sealed class ArtifactKeyCatalogListTests {`

### Summary

Verifies typed and list-based key registration behavior in `ArtifactKeyCatalog`.

### Remarks

Purpose: Protect the catalog rules that govern key identity, type reuse, and list-key registration.



Why this matters: Artifact lookup only remains safe if the catalog rejects conflicting registrations and reuses compatible keys consistently.



Expected result: Compatible registrations return the same key instance, and incompatible registrations fail with clear exceptions.

## Get_SameNameAndSameType_ReturnsSameInstance

- Kind: Member
- Signature: `public void Get_SameNameAndSameType_ReturnsSameInstance() {`

### Summary

Verifies that resolving the same logical name and value type returns the same key instance.

### Remarks

Purpose: Define key identity reuse for compatible catalog lookups.



Why this matters: Stable key identity simplifies downstream comparisons and avoids duplicate registrations for the same contract.



Expected result: Two compatible `Get` calls return the exact same artifact key instance.

## TryGet_SameNameAndSameType_ReturnsTrueAndSameInstance

- Kind: Member
- Signature: `public void TryGet_SameNameAndSameType_ReturnsTrueAndSameInstance() {`

### Summary

Verifies that `TryGet` resolves a previously registered compatible key.

### Remarks

Purpose: Protect the non-throwing retrieval path for already registered keys.



Why this matters: Callers often need to probe for an existing key without changing catalog state.



Expected result: The lookup succeeds and returns the same key instance that was originally created.

## Get_Throws_WhenNameAlreadyRegisteredWithDifferentValueType

- Kind: Member
- Signature: `public void Get_Throws_WhenNameAlreadyRegisteredWithDifferentValueType() {`

### Summary

Verifies that registering the same logical name with a different value type fails.

### Remarks

Purpose: Define the catalog protection against type collisions.



Why this matters: Reusing a logical artifact name for multiple value types would break type safety at runtime.



Expected result: A conflicting lookup throws an `InvalidOperationException` that describes the value-type mismatch.

## GetList_CreatesListArtifactKey

- Kind: Member
- Signature: `public void GetList_CreatesListArtifactKey() {`

### Summary

Verifies that `GetList` creates a list artifact key with a read-only list value type.

### Remarks

Purpose: Protect the list-key specialization provided by the catalog.



Why this matters: List artifacts rely on a predictable container type, not just a reused scalar key shape.



Expected result: The created key keeps the logical name and exposes `IReadOnlyList<T>` as its value type.

## RegisterList_ThenTryGetList_ReturnsSameInstance

- Kind: Member
- Signature: `public void RegisterList_ThenTryGetList_ReturnsSameInstance() {`

### Summary

Verifies that a registered list key can be retrieved by list lookup without losing identity.

### Remarks

Purpose: Confirm list-key reuse for explicit registrations.



Why this matters: Callers should be able to register a list key once and retrieve the same object later.



Expected result: `TryGetList` succeeds and returns the exact registered list key instance.

## GetList_Throws_WhenNameAlreadyRegisteredWithDifferentValueType

- Kind: Member
- Signature: `public void GetList_Throws_WhenNameAlreadyRegisteredWithDifferentValueType() {`

### Summary

Verifies that requesting a list key for a name already registered with a different value type fails.

### Remarks

Purpose: Protect list-key creation from value-type collisions.



Why this matters: The list helper should reject incompatible existing registrations just as strictly as scalar key lookup does.



Expected result: The catalog throws an `InvalidOperationException` that identifies the conflicting value type.

## GetList_Throws_WhenNameAlreadyRegisteredWithDifferentKeyFamily

- Kind: Member
- Signature: `public void GetList_Throws_WhenNameAlreadyRegisteredWithDifferentKeyFamily() {`

### Summary

Verifies that requesting a list key for a name already registered in the regular typed-key family fails.

### Remarks

Purpose: Define the separation between list keys and regular typed keys.



Why this matters: Keys with identical value types but different key families still represent different contracts.



Expected result: The catalog throws an `InvalidOperationException` describing the typed-key family conflict.

## Get_Throws_WhenNameAlreadyRegisteredWithDifferentKeyFamily

- Kind: Member
- Signature: `public void Get_Throws_WhenNameAlreadyRegisteredWithDifferentKeyFamily() {`

### Summary

Verifies that requesting a regular typed key for a name already registered as a list key fails.

### Remarks

Purpose: Protect the inverse key-family conflict scenario.



Why this matters: The catalog should be symmetric when rejecting incompatible regular and list key registrations.



Expected result: The catalog throws an `InvalidOperationException` describing the typed-key family mismatch.


