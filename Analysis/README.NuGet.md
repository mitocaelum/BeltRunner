# BeltRunner.Analysis

`BeltRunner.Analysis` provides Roslyn analyzers for code that uses BeltRunner.
It checks common BeltRunner authoring patterns and reports suggestions when higher-level APIs are preferred.

This package is optional.
Use it when you want IDE and build-time diagnostics while developing an application that references `BeltRunner.Core` (https://nuget.org/packages/BeltRunner.Core).

## Install

Run the following command to install it, or search for `BeltRunner.Analysis` in your IDE's NuGet package manager.

```shell
dotnet add package BeltRunner.Analysis
```

`BeltRunner.Analysis` is versioned in lockstep with `BeltRunner.Core`.
Use the matching package version when you explicitly reference both packages.

For example, `BeltRunner.Analysis` version `0.1.0` targets `BeltRunner.Core` version `0.1.0`.

## Included Rules

- `BR0001`: Prefer aggregate phase progress tracking over direct `SetTotalUnits(...)` calls.
- `BR0002`: Prefer tracked unit scopes over manual unit start and running-status calls.
- `BR0003`: Prefer `PhaseBase<TFactory>` over directly implementing `IPhase` for new phases.

## Package Information

- Target Framework: `.NET Standard 2.0`
- Dependencies:
  - `BeltRunner.Core`: The matching package version targeted by these analyzers
  - `Microsoft.CodeAnalysis.CSharp`: Roslyn C# APIs used by the analyzer implementation
  - `Microsoft.CodeAnalysis.CSharp.Workspaces`: Workspace-level Roslyn APIs used by analyzer infrastructure

## Git Repository

For the source code, samples, and documentation, see the repository in GitHub:  
[github.com/mitocaelum/BeltRunner](https://github.com/mitocaelum/BeltRunner)

## License

BeltRunner is released under the following license.

[Apache License 2.0](https://www.apache.org/licenses/LICENSE-2.0)
