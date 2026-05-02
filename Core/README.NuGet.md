# BeltRunner

BeltRunner is a small framework that helps when your application needs to execute multiple processing steps in sequence.
It packages the processing itself into manageable components and connects them through controlled data flow, while also providing features such as UI-friendly notifications and cancellation handling.

BeltRunner focuses on a pipeline-style execution model and does not provide presentation features such as a UI.
It communicates with your application through Rx (Reactive Extensions).

`BeltRunner.Core` is the main package that provides the runtime and public API surface for that workflow model.

## What this library is for

For example, imagine that you need to process a text file.
If you only need to run the processing and present the final result to the user, you may not need this framework at all.
However, if your application needs to show progress through a UI or allow user intervention when an abnormal condition occurs, this framework may help.

BeltRunner helps with the following:

- Manage each processing step and each processing target as components.
- Broadcast progress and state through Rx and retained collections.
- Provide a way to handle user intervention during processing.
- Pass cancellation requests into the running workflow.

## Key Concepts

BeltRunner models processing through the following concepts:
- **Host**: The top-level BeltRunner component that manages the other components listed below.
- **Phase**: A component that represents one processing step and is implemented by the user. This concept makes it easier for the framework to recognize and manage the processing itself.
- **Unit**: A component that wraps one processing target so that the framework can track and handle it.
- **Plan**: A design that describes the order in which phases run.
- **Run**: A component that represents the state of a running plan. This separates design (`Plan`) from execution (`Run`) and allows the framework to provide additional runtime features.
- **Artifact**: An object used to make the data exchanged between phases explicit.

## Install

BeltRunner is distributed through NuGet.
Run the following command to install it, or search for `BeltRunner.Core` in your IDE's NuGet package manager.

```shell
dotnet add package BeltRunner.Core
```


As an optional addition, the Roslyn analyzer package `BeltRunner.Analysis` is also available (https://nuget.org/packages/BeltRunner.Analysis).
It checks code that uses BeltRunner and provides suggestions when appropriate.
Run the following command to install it, or search for `BeltRunner.Analysis` in your IDE's NuGet package manager.

```shell
dotnet add package BeltRunner.Analysis
```

## Package Information

- Target Framework: `.NET Standard 2.0`
- Dependencies:
  - `NLog`: Used by the framework for optional internal logging
  - `System.Reactive`: Provides the Rx-based observable surface used by BeltRunner runtime APIs

## Git Repository
For the source code, samples, and documentation, see the repository in GitHub:  
[github.com/mitocaelum/BeltRunner](https://github.com/mitocaelum/BeltRunner)

## Logging with NLog

`BeltRunner.Core` includes internal framework logging that assumes NLog.
If the host application configures NLog rules for BeltRunner loggers, those logs can be captured.

However, this package does not provide an application-specific NLog configuration by default.
It does not automatically decide where logs should be written, and it does not configure your logging pipeline for you.

If you want BeltRunner framework logs, configure NLog in your own application.
If you do not, BeltRunner still runs normally.

## License

BeltRunner is released under the following license.

[Apache License 2.0](https://www.apache.org/licenses/LICENSE-2.0)
