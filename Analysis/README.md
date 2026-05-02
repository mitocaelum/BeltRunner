# BeltRunner.Analysis

BeltRunner.Analysis provides Roslyn analyzers for your coding using BeltRunner.

> [!NOTE]  
> In most cases, you only need this project when you are going to develop, inspect, or change `BeltRunner.Analysis` itself.
> If you only want to use BeltRunner from your own application, follow the guidance in the [repository root README](../README.md) and add the required NuGet package references there. You do not need to keep the `Analysis` project code in your local workspace just to consume BeltRunner.

## Included Rules

- `BR0001`: Prefer aggregate phase progress tracking over direct `SetTotalUnits(...)` calls.
- `BR0002`: Prefer tracked unit scopes over manual unit start and running-status calls.
- `BR0003`: Prefer `PhaseBase<TFactory>` over directly implementing `IPhase` for new phases.

## Typical Use

When you use the BeltRunner library in your project, add this package too. 
Diagnostics appear in the IDE and during command-line builds.

## Target Framework And Language Settings

The project is configured as follows:

- Target framework: `.NET Standard 2.0`
- C# language version: `12`
- Nullable reference types: `enable`

Those settings keep `BeltRunner.Analysis` aligned with the authoring rules used in this repository while remaining packable as a standard analyzer package.

## Packaging And Versioning Notes

This project is intended to be packed as a NuGet package.

- Package generation is enabled in the project file
- Symbols are included and the symbol package format is `snupkg`
- Repository URL and publish metadata are included in the package
- Version generation uses `MinVer`
- The NuGet package readme is authored in `README.NuGet.md` and packed as `README.md`

`BeltRunner.Analysis` is versioned in lockstep with `BeltRunner.Core`.
In practical terms, a given `BeltRunner.Analysis` package version targets the matching `BeltRunner.Core` package version and declares an exact NuGet dependency on it.

For example, if `BeltRunner.Analysis` is packed as `0.1.0-alpha.2`, it requires `BeltRunner.Core` version `0.1.0-alpha.2`.

## Dependencies

The direct NuGet dependencies declared by this project are:

- `BeltRunner.Core` (exact package dependency at pack time): Provides the target API surface that these analyzers inspect
- `Microsoft.CodeAnalysis.Analyzers` (`PrivateAssets="all"`): Enables analyzer-specific build checks and validation during development
- `Microsoft.CodeAnalysis.CSharp`: Provides the Roslyn C# APIs used to inspect syntax and semantic models
- `Microsoft.CodeAnalysis.CSharp.Workspaces`: Provides workspace-level Roslyn APIs used by analyzer and code-fix infrastructure

## License

The license for `BeltRunner.Analysis` follows the license terms described in the [repository root README](../README.md).
