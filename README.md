# BeltRunner

BeltRunner is a small framework that helps when your application needs to execute multiple processing steps in sequence.
It packages the processing itself into manageable components and connects them through controlled data flow, while also providing features such as UI-friendly notifications and cancellation handling.

BeltRunner focuses on a pipeline-style execution model and does not provide presentation features such as a UI.
It communicates with your application through Rx (Reactive Extensions).

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


As an optional addition, the Roslyn analyzer package `BeltRunner.Analysis` is also available.
It checks code that uses BeltRunner and provides suggestions when appropriate.
Run the following command to install it, or search for `BeltRunner.Analysis` in your IDE's NuGet package manager.

```shell
dotnet add package BeltRunner.Analysis
```

`BeltRunner.Analysis` is released on the same version line as `BeltRunner.Core` and targets the corresponding `BeltRunner.Core` version.
If you explicitly reference both packages, use the same version for each.

## Getting Started
See the [README](./Core/README.NuGet.md) for the `BeltRunner.Core` package.

## Projects in the solution

This repository is a Visual Studio solution containing the following projects:
- [Core](./Core/README.md): The main BeltRunner workflow engine and public runtime API.
- [Analysis](./Analysis/README.md): Roslyn analyzers that guide recommended BeltRunner usage patterns during development.
- [Core.TEST](./Core.TEST/README.md): The test suite that protects BeltRunner.Core behavior and regression boundaries.
- [SampleConsoleApp](./SampleConsoleApp/README.md): A minimal end-to-end simple sample that shows the basic BeltRunner authoring and execution flow.
- [SampleWebApp](./SampleWebApp/README.md): A richer sample that demonstrates UI integration, interactions, host options, and observable runtime state.


## License

BeltRunner is released under the following license.

[Apache License 2.0](https://www.apache.org/licenses/LICENSE-2.0)

Additional legal, privacy, and disclaimer information is available in [LEGAL.md](./LEGAL.md).

## Feedbacks

If you have any feedback, please open an issue in GitHub.   
If you want to change the code, please create a pull request.
