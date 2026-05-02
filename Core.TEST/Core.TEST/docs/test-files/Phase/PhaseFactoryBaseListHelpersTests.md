# PhaseFactoryBaseListHelpersTests

- Source: `Phase/PhaseFactoryBaseListHelpersTests.cs`
- Namespace: `BeltRunner.TEST.Core.Phase`
- Generated from XML documentation comments.

## PhaseFactoryBaseListHelpersTests

- Kind: Type
- Signature: `public sealed class PhaseFactoryBaseListHelpersTests {`

### Summary

Verifies the list-oriented artifact helper APIs exposed by `PhaseFactoryBase`.

### Remarks

Purpose: Protect the typed helper methods that create consumed, produced, and shared list artifact keys.



Why this matters: The helpers are convenience APIs, but they still define naming and typing conventions that downstream phases depend on.



Expected result: Each helper creates the expected logical name and list value type, and shared keys can be created without additional prefixes.

## ConsumeList_AndProduceList_DeclareExpectedKeys

- Kind: Member
- Signature: `public void ConsumeList_AndProduceList_DeclareExpectedKeys() {`

### Summary

Verifies that list consume and produce helpers declare the expected keys and registrations.

### Remarks

Purpose: Confirm that helper-generated keys preserve both logical names and list value typing.



Why this matters: Incorrect registrations would break artifact wiring between phases while still looking superficially valid.



Expected result: The factory exposes the expected key names, list value types, and consume or produce registrations.

## SharedList_CreatesKey_FromLogicalNameOnly

- Kind: Member
- Signature: `public void SharedList_CreatesKey_FromLogicalNameOnly() {`

### Summary

Verifies that shared list keys are created from the logical name only.

### Remarks

Purpose: Define the naming contract for shared list artifacts.



Why this matters: Shared keys should remain stable and predictable across producers and consumers.



Expected result: The created key keeps the requested logical name and uses a read-only list value type.


