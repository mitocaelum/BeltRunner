# ListArtifactKeyApiTests

- Source: `Plan/Artifacts/ListArtifactKeyApiTests.cs`
- Namespace: `BeltRunner.TEST.Core.Plan.Artifacts`
- Generated from XML documentation comments.

## ListArtifactKeyTests

- Kind: Type
- Signature: `public sealed class ListArtifactKeyTests {`

### Summary

Verifies core API properties of `ListArtifactKey{TItem}`.

### Remarks

Purpose: Protect the base type semantics of list artifact keys.



Why this matters: The list artifact abstraction only works if its value type stays aligned with read-only lists.



Expected result: The key exposes `IReadOnlyList<TItem>` as its runtime value type.

## ValueType_IsReadOnlyListOfItem

- Kind: Member
- Signature: `public void ValueType_IsReadOnlyListOfItem() {`

### Summary

Verifies that the runtime value type is a read-only list of the item type.

### Remarks

Purpose: Define the runtime type contract for list artifact keys.



Why this matters: Producers and consumers depend on a stable list container type when exchanging artifacts.



Expected result: The key reports `IReadOnlyList<int>` for an integer list artifact.

## ArtifactNameTests

- Kind: Type
- Signature: `public sealed class ArtifactNameTests {`

### Summary

Verifies that `ArtifactName` works cleanly with list artifact key construction.

### Remarks

Purpose: Protect the compatibility between logical artifact names and list-key creation.



Why this matters: Naming helpers are often used together, and friction here would make the list-key API awkward to adopt.



Expected result: A created artifact name can be passed directly into a list key and preserved as the logical key name.

## Create_Works_ForListArtifactKeyConstruction

- Kind: Member
- Signature: `public void Create_Works_ForListArtifactKeyConstruction() {`

### Summary

Verifies that an artifact name created through the factory can be used to construct a list artifact key.

### Remarks

Purpose: Confirm the happy path integration between `ArtifactName.Create(string)` and `ListArtifactKey{TItem}`.



Why this matters: Logical naming should remain straightforward when list artifacts are introduced.



Expected result: The constructed key keeps the provided logical artifact name unchanged.

## ArtifactStoreListKeyTests

- Kind: Type
- Signature: `public sealed class ArtifactStoreListKeyTests {`

### Summary

Verifies artifact store behavior when list artifact keys are used for storage and retrieval.

### Remarks

Purpose: Protect list-key interoperability with the shared artifact store.



Why this matters: The store is where key identity meets runtime values, so list keys must behave exactly like other keys at retrieval time.



Expected result: Values stored through list keys can be retrieved directly and through compatible key signatures.

## SetAndGet_Works_WithListArtifactKey

- Kind: Member
- Signature: `public void SetAndGet_Works_WithListArtifactKey() {`

### Summary

Verifies that the artifact store can set and get a value through a list artifact key.

### Remarks

Purpose: Confirm the basic storage path for list artifacts.



Why this matters: A dedicated key type is only useful if the store accepts it without special handling by the caller.



Expected result: The stored list is found successfully and returned as the same object instance.

## Store_UsesNameAndValueTypeSignature_AcrossKeyImplementations

- Kind: Member
- Signature: `public void Store_UsesNameAndValueTypeSignature_AcrossKeyImplementations() {`

### Summary

Verifies that the artifact store matches list and regular keys by logical name and value-type signature.

### Remarks

Purpose: Define interoperability between compatible key implementations.



Why this matters: Callers may use different key construction paths and still expect the same stored value when the logical contract matches.



Expected result: A value stored with a list key can be retrieved through a regular key with the same name and read-only list value type.

## PhaseOutcomeListKeyTests

- Kind: Type
- Signature: `public sealed class PhaseOutcomeListKeyTests {`

### Summary

Verifies that `PhaseOutcome` accepts list artifact keys as produced outputs.

### Remarks

Purpose: Protect list artifact production within phase outcome composition.



Why this matters: Phase outputs are the handoff point between phases, so list-key support must work there without custom wrappers.



Expected result: Producing with a list key records the key metadata and value exactly as provided.

## Produce_AcceptsListArtifactKey

- Kind: Member
- Signature: `public void Produce_AcceptsListArtifactKey() {`

### Summary

Verifies that producing an artifact with a list key records the expected key and value.

### Remarks

Purpose: Confirm the list-key overload path for produced artifacts.



Why this matters: Output contracts should remain uniform regardless of whether a scalar or list artifact is produced.



Expected result: The outcome records one produced artifact with the original key name, list value type, and value instance.

## ArtifactSeedsListKeyTests

- Kind: Type
- Signature: `public sealed class ArtifactSeedsListKeyTests {`

### Summary

Verifies that `ArtifactSeeds` can create produced artifacts from list artifact keys.

### Remarks

Purpose: Protect list-key support in seed artifact creation.



Why this matters: Initial artifact seeding should work consistently for both scalar and list-based startup data.



Expected result: Seeded produced artifacts preserve the list key metadata and the supplied value instance.

## Seed_AcceptsListArtifactKey

- Kind: Member
- Signature: `public void Seed_AcceptsListArtifactKey() {`

### Summary

Verifies that a list artifact key can be used directly when seeding an artifact.

### Remarks

Purpose: Confirm the explicit list-key seeding path.



Why this matters: Startup data often arrives in batches, and list artifacts should be first-class seeds.



Expected result: The seeded artifact keeps the original key metadata and the original value object.

## Seed_Works_WithLogicalNameOnly

- Kind: Member
- Signature: `public void Seed_Works_WithLogicalNameOnly() {`

### Summary

Verifies that list artifact seeding works cleanly when only a logical name is used to create the key.

### Remarks

Purpose: Protect the common path where callers build a list key from a logical artifact name and immediately seed it.



Why this matters: The ergonomic path should not hide any metadata mismatch or unexpected key rewriting.



Expected result: The seeded artifact keeps the logical name, exposes the read-only list value type, and stores the original values object.


