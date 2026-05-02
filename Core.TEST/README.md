# BeltRunner.TEST.Core

This project verifies `BeltRunner.Core` as both a specification suite and a regression safety net. The goal is not only to prove that the code works today, but also to make future behavior changes visible, reviewable, and intentional.

## Test Policy

- Prefer externally observable behavior and public contracts over implementation details.
- Keep each test focused on one protected behavior so that failures point to a clear broken rule.
- Cover boundary conditions such as cancellation, failure, partial success, duplicate registration, and lifecycle ordering in addition to standard success paths.
- Name tests so that the method name reads like a compact specification: precondition, action, and expected result.
- Add a `TestOf` attribute to each test fixture so the relationship between the fixture and the target API stays explicit.
- Maintain XML documentation comments that explain purpose, motivation, and expected outcome for each documented test element.

## What The Tests Protect

This test project primarily protects the following guarantees.

- Execution flow state transitions remain consistent across events, snapshots, and completion outcomes.
- Phase, artifact, and unit APIs preserve type safety and contract consistency.
- Host and executor lifecycle callbacks run in the expected order and honor their failure policies.
- A broken behavior can be traced through both this README and the generated per-file test documentation.

## Documentation Workflow

- Generate one Markdown document for each `*.cs` file in this project.
- Use XML documentation comments in the source files as the single source of truth.
- Run `tools/Generate-TestDocs.ps1` to regenerate the documentation set.
- Write generated files to `docs/test-files` while preserving the source file directory structure.
- Update the document index in this README as part of the same generation step.

## Regeneration

```powershell
pwsh ./tools/Generate-TestDocs.ps1
```

The same script can also be run from Windows PowerShell.

## Running By Category

- Run the default suite without performance-focused tests: `dotnet test --filter "TestCategory!=Performance"`
- Run only the performance-focused tests: `dotnet test --filter "TestCategory=Performance"`

## Test File Documentation

<!-- TEST-DOC-LINKS:START -->
- [Execution/RunSnapshotTests.cs](docs/test-files/Execution/RunSnapshotTests.md)
- [Execution/RunTerminalPolicyTests.cs](docs/test-files/Execution/RunTerminalPolicyTests.md)
- [Host/DefaultSequentialPlanExecutorTests.cs](docs/test-files/Host/DefaultSequentialPlanExecutorTests.md)
- [Host/DiagnosticRetentionTests.cs](docs/test-files/Host/DiagnosticRetentionTests.md)
- [Host/HostPerformanceRegressionTests.cs](docs/test-files/Host/HostPerformanceRegressionTests.md)
- [Host/InteractionRequestLogRetentionTests.cs](docs/test-files/Host/InteractionRequestLogRetentionTests.md)
- [Host/NLogIntegrationTests.cs](docs/test-files/Host/NLogIntegrationTests.md)
- [Host/PhaseTelemetryIntegrationTests.cs](docs/test-files/Host/PhaseTelemetryIntegrationTests.md)
- [Host/PublicFaultSurfaceTests.cs](docs/test-files/Host/PublicFaultSurfaceTests.md)
- [Host/RunEventLogRetentionTests.cs](docs/test-files/Host/RunEventLogRetentionTests.md)
- [Host/RunLaunchLifecycleTests.cs](docs/test-files/Host/RunLaunchLifecycleTests.md)
- [Host/SnapshotPublishCoalescingTests.cs](docs/test-files/Host/SnapshotPublishCoalescingTests.md)
- [ObservableRecorder.cs](docs/test-files/ObservableRecorder.md)
- [Phase/PhaseFactoryBaseListHelpersTests.cs](docs/test-files/Phase/PhaseFactoryBaseListHelpersTests.md)
- [Phase/PhaseTelemetryExtensionsTests.cs](docs/test-files/Phase/PhaseTelemetryExtensionsTests.md)
- [Phase/UnitSetTests.cs](docs/test-files/Phase/UnitSetTests.md)
- [Plan/Artifacts/ArtifactKeyCatalogListTests.cs](docs/test-files/Plan/Artifacts/ArtifactKeyCatalogListTests.md)
- [Plan/Artifacts/ListArtifactKeyApiTests.cs](docs/test-files/Plan/Artifacts/ListArtifactKeyApiTests.md)
<!-- TEST-DOC-LINKS:END -->




