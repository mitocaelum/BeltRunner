# BeltRunner: Sample Web App

This web application is a Razor-based demo that shows slightly more advanced BeltRunner usage.

When you start it, the web site launches and opens in a browser.
The screen contains controls and options for starting the workflow, along with UI elements that show progress while the workflow runs.

This helps illustrate how BeltRunner can be combined with a UI and observed while it is running.

> [!NOTE]  
> If you are not familiar with BeltRunner yet, it is recommended that you first read the basic concept article "[Getting Started](../Document/_site/docs/tutorials/GettingStarted.html)".

> [!NOTE]  
> It is also recommended that you first review the sample that uses only the basic BeltRunner features: "[SampleConsoleApp](../SampleConsoleApp/README.md)".

> [!NOTE]  
> IDs such as `Ref01` that appear in this article are also included in code comments, so you can search for the corresponding locations in the code.

## Build and Run

- .NET 8.0
- C# 12

This sample is provided as one project in the solution and references the BeltRunner `Core` project.

For that reason, it is recommended that you clone the full solution and run the sample there.
The following example runs it from the solution folder.

```shell
dotnet run --project F:\Development\BeltRunner\SampleWebApp\SampleWebApp.csproj --launch-profile http
```

## Scenario

1. **Discover Target Pages**  
   Retrieves 10 links from the specified page URL.
2. **Scrape Page Content**  
   Processes each page in parallel, obtains language information, and builds vectors.
3. **Generate Statistics**  
   Produces result items for summary cards from the vectors.

> [!IMPORTANT]  
> This is only a scraping simulation for demonstration purposes.
> It does not perform real network communication, file generation, or external API calls.

In addition to the basic features shown in "[SampleConsoleApp](../SampleConsoleApp/README.md)", this sample uses the following features.

- BeltRunner logging through NLog
- Use of `HostOptions`
- UI integration for progress updates and operator interaction
- Helper features such as `HostBuilder`
- Use of DI

## Program Flow

At a high level, the application works as follows.

1. Execution begins from the top-level statements in `Program.cs`, where the web application is configured.
2. BeltRunner and other required services are registered in DI.
3. When the user clicks the run button in the UI, `ScrapingDemoController.InvokeHost()` is called and prepares the run start.
   - At this point, the input values are normalized and the screen state is reset
4. `StartSequentialAsync()` builds the plan and initial artifacts on the spot and starts the run.
5. `RunLaunchOptions` callbacks attach subscriptions for snapshots, diagnostics, and interactions so that the UI can observe run state changes.
6. The three phases run in order, and progress, diagnostics, and operator interaction requests are reflected in `ScrapingDemoState`.
7. After the run completes, the application reads the generated artifacts, shows the result in the UI, and finally disposes the run.

### Using DI

_Reference ID: `Ref50`_

```csharp
builder.Services.AddScoped<ScrapingDemoState>();
builder.Services.AddScoped<BeltRunner.Core.Host.IHost>(_ => CreateBeltRunnerHost());
builder.Services.AddScoped<ScrapingDemoController>();
```

Using DI is not required, but BeltRunner can also be used through DI.
Here, the host is registered as `IHost`.

_Reference ID: `Ref51`_

```csharp
return new HostBuilder()
    .UseInteractionBrokerFactory(static () => new InMemoryInteractionBroker())
    .UsePublicFaultInfoPolicy(new SamplePublicFaultInfoPolicy())
    .WithDiagnosticMode(DiagnosticMode.All)
    .Configure(options => {
        // The sample uses bounded retention and light snapshot coalescing so repeated demo runs
        // keep predictable in-memory behavior without hiding the latest observable state.
        options.RunEventLogMaxRetainedCount = 256;
        options.InteractionRequestLogMaxRetainedCount = 32;
        options.InteractionMaxPendingRequestCount = 4;
        options.RunDiagnosticsMaxRetainedCount = 128;
        options.SnapshotPublishCoalescingInterval = TimeSpan.FromMilliseconds(100);
    })
    .Build();
```

The actual host is created by `CreateBeltRunnerHost()`, which uses the helper builder `HostBuilder()`.

In "[SampleConsoleApp](../SampleConsoleApp/README.md)", the host was created in a more primitive way without this builder, but this more convenient approach is also available.

The full details belong in the API reference, but the following list briefly explains what the builder is doing here.

- `UseInteractionBrokerFactory`: Enables interactions, which are used for operator input during execution.
- `UsePublicFaultInfoPolicy`: Defines the policy for how exceptions are exposed externally. See "[Handling Exceptions from Inside a Phase](#handling-exceptions-from-inside-a-phase)".
- `WithDiagnosticMode`: Sets which diagnostic levels should be retained for runs started by the BeltRunner host. See "[Diagnostic Levels](#diagnostic-levels)".
- `FaultOnFailedOutcome`: This is not used here, but it controls whether a run that ends as `Failed` should also be treated as a host fault.
- `Configure`: Lets you change log limits, snapshot sizes, and similar options.

### Running from Home

`Home.razor` contains several options that the user can choose and the button used to start the workflow.

- **\[Source URL\]**: Specifies the URL of the web page used as the starting point of the workflow, although the application does not actually access it. It is used as an example of passing an initial parameter to the first phase.
- **\[Run\]**: Starts the workflow.
- **\[Open App Logs\]**: This sample configures NLog in memory, so you can inspect the NLog entries produced by BeltRunner.
- **\[Simulate an authentication challenge in Phase 1\]**: When enabled, a login dialog appears during Phase 1 as an interaction demo. Whatever the user enters is not actually used.
- **\[Simulate a recoverable anomaly in Phase 2\]**: When enabled, a dialog appears during Phase 2 asking whether processing should stop after an anomaly occurs. This is also an interaction demo, but it uses interactions in a different way.

When the **\[Run\]** button is clicked, it eventually calls `ScrapingDemoController.InvokeHost()`.

_Reference ID: `Ref52`_

```csharp
IRun run = await this.host.StartSequentialAsync(
    ConfigurePlan,
    builder => ConfigureInitialArtifacts(builder, normalizedSourceUrl, injectAuthenticationChallenge, injectMinorWarning),
    CreateRunLaunchOptions()).ConfigureAwait(false);

_ = ObserveCompletionAsync(run);
```

The host has already been injected from DI and stored in a field, so it can be accessed through `this.host`.

In "[SampleConsoleApp](../SampleConsoleApp/README.md)", the sample prepared a plan and then used `StartAsync()` to begin processing through the host.
Here, it uses `StartSequentialAsync()` instead.
This is a helper method that builds the plan in place and starts the run immediately as a shortcut.
There are also overloads that let you provide artifacts or `HostOptions`.

As with `StartAsync()`, it returns `IRun`, so you can wait for `Completion` to observe the end of the workflow.

The `RunLaunchOptions` passed from `CreateRunLaunchOptions()` to `StartSequentialAsync()` defines callbacks that run at run start and run end.
For details, see "[Run-Scoped Options](#run-scoped-options)".

---

## Using Interactions

It is common for workflows to require operator intervention while they are running.
BeltRunner calls this an interaction and implements it through a class that implements the `IInteractionBroker` interface.
In ordinary usage, the default implementation `InMemoryInteractionBroker` is usually sufficient.

The interaction broker factory is set when the host is created.

```csharp
.UseInteractionBrokerFactory(static () => new InMemoryInteractionBroker())
```

### On the Side That Requests an Interaction

Inside a phase, a request is sent when an interaction becomes necessary.

_Reference ID: `Ref53`_

```csharp
InteractionRequest<(string UserName, string Password)> request = new(
    AUTHENTICATION_CHALLENGE_KIND,
    context.Key,
    title: "Authentication required",
    message: "The simulated website requested credentials before Phase 1 could continue.");
```

First, the request is created.

`InteractionRequest` specifies the expected return type, such as `<(string UserName, string Password)>`.

`AUTHENTICATION_CHALLENGE_KIND` is a string used to distinguish the interaction kind.
BeltRunner itself does not interpret it. The application reads it and uses it for its own handling logic.

The `title` and `message` here act as a heading and explanation.
How they are actually used depends on the application that handles the interaction.

> [!NOTE]
> In _`Ref58`_, you can see another interaction example.
> That one requests a `bool`.

_Reference ID: `Ref54`_

```csharp
InteractionResult<(string UserName, string Password)> result = 
    await context.Interaction.TryAskAsync(request, ct).ConfigureAwait(false);
if( result.IsAccepted ) {
    string userName = string.IsNullOrWhiteSpace(result.Response.UserName) ? "(empty)" : result.Response.UserName.Trim();
    context.Telemetry.Info($"The operator provided credentials for Phase 1. userName={userName}", sourceUnit.Id);
    return;
}
```

The phase waits until it receives a result.
`InteractionResult` uses the same expected return type as the request: `<(string UserName, string Password)>`.

`InteractionResult` includes `IsAccepted`, which indicates whether the interaction succeeded, so that value is evaluated first.

If the interaction is canceled on the application side, for example if the user closes the authentication dialog with Cancel, `IsAccepted` may be set to `false`.
Whether that happens is up to the application, and `Reason` can also contain the reason why the value became `false`.

The returned value itself is stored in `Response`.

### On the Side That Handles the Interaction Request

_Reference ID: `Ref55`_

```csharp
this.interactionSubscription = run.Interaction.ActiveRequestsChanges.Subscribe(
    new DelegateObserver<IReadOnlyList<IInteractionRequest>>(_ => this.state.ApplyActiveInteractions(run.ActiveInteractions)));
```

On the application side, the code subscribes to `run.Interaction.ActiveRequestsChanges` so that it can respond to interaction requests.
This subscription is set inside the application method `AttachRunState()`, and that method is called from `RunLaunchOptions.LifecycleCallbacks.BeforeExecutionStartAsync`, after the run is created but before it starts executing.

_Reference ID: `Ref56`_

```csharp
if( broker.TryRespond(requestId.Value, (userName ?? string.Empty, password ?? string.Empty)) ) {
    this.state.ClearInteraction();
}
```

When a request occurs, the application shows a dialog box in the UI and asks the user for input.
The result is sent back to the phase through the interaction broker by calling `TryRespond()`.
This is the normal route in which the user completes the input successfully.

_Reference ID: `Ref57`_

```csharp
if( broker.TryReject(requestId.Value, reason ?? string.Empty) ) {
    this.state.ClearInteraction();
}
```

If the user rejects the interaction, for example by canceling the dialog box, the application responds by calling `TryReject()`.
In that case, `InteractionResult.IsAccepted` becomes `false`.

## Handling Exceptions from Inside a Phase

```csharp
.UsePublicFaultInfoPolicy(new SamplePublicFaultInfoPolicy())
```

`UsePublicFaultInfoPolicy`, which is indirectly applied to `HostOptions`, accepts an implementation of `IPublicFaultInfoPolicy` and injects the rule that determines how exceptions are exposed externally by the BeltRunner host.

If you do not specify one, `DefaultPublicFaultInfoPolicy` is used.

The `Create()` method on `IPublicFaultInfoPolicy` receives an `Exception` and an origin string such as `"run"`, `"host"`, or `"phase:Build"`, and is responsible for producing `PublicFaultInfo` that is safe to expose.

If you show raw exception messages or stack traces directly to users, they may contain information that you do not want to expose.
To avoid that, you return `PublicFaultInfo`, which contains only a safe summary such as `FaultKind`, `PublicMessage`, and `Origin`.

## Diagnostic Levels

```csharp
.WithDiagnosticMode(DiagnosticMode.All)
```

`WithDiagnosticMode`, which is applied through `HostOptions`, is the option that controls which diagnostic levels are retained while phases are running, as described in "[Diagnostic Log](../SampleConsoleApp/README.md#diagnostic-log)" in "[SampleConsoleApp](../SampleConsoleApp/README.md)".

- `Disabled`: no diagnostics are retained
- `ErrorsOnly`: only `Error` diagnostics are retained
- `All`: `Information`, `Warning`, and `Error` diagnostics are all retained

This lets the application remove log levels that it does not need and can improve memory efficiency in some cases.

## Run-Scoped Options

`RunLaunchOptions`, which can be passed optionally to `StartAsync()` or `StartSequentialAsync()`, defines options for the specific run that is about to start.
It is not host-scoped.
The following option can be specified.

- `RunLifecycleCallbacks`

(At present, it contains only callback-related options.)

### RunLifecycleCallbacks

You can define callbacks that run at several points in the lifecycle.

- `BeforeExecutionStartAsync`: Called after `IRun` is created and initialized, but before execution starts. The run object already exists, but the run has not started yet. You can begin subscriptions to `SnapshotStream` or `EventStream`, wire the run into the UI, and so on.
- `OnCompletedAsync`: A supplemental hook that is called after the run result is settled. It can be used for branching after success, failure, or cancellation, or for completion-time aggregation and summary creation.
- `BeforeRunDisposeAsync`: Called immediately before the internal teardown triggered by `Dispose()`. It can be used to remove subscriptions, clear external references, and detach the UI from the run handle.

## Managing Progress

This sample demonstrates the higher-level API that BeltRunner provides for progress management.
For an example that uses the lower-level API, see "[SampleConsoleApp](../SampleConsoleApp/README.md)".

### Starting Phase Progress Tracking

An application that shows progress will often want to know the total number of units that a phase is expected to process.

_Reference ID: `Ref60`_

```csharp
using IPhaseProgressTracker progressTracker 
    = context.Telemetry.BeginPhaseProgressTracking(pageUrls.Count);
```

`BeginPhaseProgressTracking` is a higher-level API for handling both "how many units this phase has in total" and "how many have completed so far" together.
(Internally, it calls `SetTotalUnits()`.)

The returned `IPhaseProgressTracker` is used by the subsequent progress-management logic.

<!--
`Telemetry.SetTotalUnits()` tells the phase how many units it is expected to process in total.
This is less about "setting the number of units that currently exist" and more about "communicating the expected total for progress calculation".
In other words, it does not have to match the number of units that have already been created. The purpose is to communicate the expected total externally.
Because of that, even if only 5 units currently exist, it is still valid to call `SetTotalUnits(10)`.
This value can be read as `TotalUnits` from the phase `Snapshot`.
> [!IMPORTANT]  
> However, if you call `SetTotalUnits(10)` and the actual number of units is 12, `TotalUnits` becomes 12.
-->

### Starting and Completing Unit Processing

_Reference ID: `Ref61`_

```csharp
using ITrackedUnitScope trackedUnit 
    = progressTracker.BeginUnit(unit);
```

When the application actually begins processing a unit, it calls `BeginUnit()` to declare that "processing for this unit starts now" under the tracker.
As a result, the unit status becomes `Running`.

_Reference ID: `Ref62`_

```csharp
trackedUnit.Complete();
```

After the required work is done and unit processing has completed, call `Complete()`.
This changes the unit status to `Succeeded`.
At the same time, the phase `ProcessedUnits` value is incremented by 1.

> [!IMPORTANT]  
> You must call this method explicitly.
> Merely leaving the `using` scope does not change the status or related progress values.

_Reference ID: `Ref63`_

```csharp
context.Telemetry.SetUnitStatus(unit.Id, UnitStatus.Cancelled);
```

For cancellation or failure cases, call the lower-level API `SetUnitStatus()` to update the status.

## Other Samples

This solution also contains the following sample.
- [SampleConsoleApp](../SampleConsoleApp/README.md)
