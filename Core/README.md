# BeltRunner.Core

This README describes the `Core` project itself.
If you want the overall BeltRunner concepts, architecture, or solution-wide introduction, start with the [repository root README](../README.md).

> [!NOTE]  
> In most cases, you only need this document when you are going to develop, inspect, or change `BeltRunner.Core` itself.
> If you only want to use BeltRunner from your own application, follow the guidance in the [repository root README](../README.md) and reference the library package. You do not need to work in this project just to consume BeltRunner.

## About this Core project

`BeltRunner.Core` is the main library project in the solution.
It contains the reusable runtime and public API surface for BeltRunner itself.

In practical terms, this project is where the framework types live:

- host and run APIs
- phase authoring APIs
- sequential plan APIs
- artifact APIs
- execution, diagnostics, and interaction runtime types

This project is the part that an application references when it wants to use BeltRunner as a library.

## Target Framework And Language Settings

The project is configured as follows:

- Target framework: `.NET Standard 2.0`
- C# language version: `12`
- Nullable reference types: `enable`

Those settings make `BeltRunner.Core` consumable from a wide range of .NET application types while still using the authoring rules adopted by this repository.

## Packaging And Versioning Notes

This project is intended to be packed as a NuGet package.

- Package generation is enabled in the project file
- Symbols are included and the symbol package format is `snupkg`
- Repository URL and publish metadata are included in the package
- Version generation uses `MinVer`

Versioning policy and release rules for the repository are described in the project instructions and are rooted in Git tags rather than in hard-coded package versions.

## Dependencies

The direct NuGet dependencies declared by this project are:

- `MinVer` (`PrivateAssets="All"`): Generates package versions from Git tags during build and pack
- `NLog`: Used by the framework for optional internal logging
- `System.Reactive`: Provides the Rx-based observable surface used by BeltRunner runtime APIs

This project also packs a few repository files into the NuGet package:

- `README.md`
- `LICENSE`
- `Logo256.png`

## Logging

`BeltRunner.Core` includes logging integration that assumes NLog.
In other words, the framework can emit logs through NLog if the host application wants to collect them.

However, logging is not fully configured by default:

- `BeltRunner.Core` does not ship an application-specific NLog configuration
- it does not automatically set up logger rules for you
- it does not assume where logs should be written

Because of that, applications that want BeltRunner logs must configure NLog on their own side.
If the application does not provide matching NLog configuration, BeltRunner still runs normally, just without those framework logs being captured.

## License

The license for `BeltRunner.Core` follows the license terms described in the [repository root README](../README.md).

## Related Reading

Use these documents depending on what you need next:

- [Root README](../README.md): Overall introduction and repository-level guidance
- [SampleConsoleApp README](../SampleConsoleApp/README.md): Basic usage example
- [SampleWebApp README](../SampleWebApp/README.md): More advanced usage example
- [Core.TEST README](../Core.TEST/README.md): What the test suite is protecting
