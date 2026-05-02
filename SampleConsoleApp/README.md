# BeltRunner: Sample Console App

This console application is a demo that shows how BeltRunner is actually used.

This sample uses only the basic features of BeltRunner and does not include additional advanced usage patterns or features. For that kind of sample, see [Sample Web App](../SampleWebApp/README.md).

> [!NOTE]  
> If you do not know anything about BeltRunner yet, it is recommended that you first read [Getting Started](../Document/_site/docs/tutorials/GettingStarted.html), which explains the basic concepts.

> [!NOTE]  
> IDs such as `Ref01` that appear in this document are also written in the code comments, so you can use them as markers when you search for the corresponding code shown in this document.

## Build and Run

- .NET 8.0
- C# 12

This sample is provided as one project in the solution and references the BeltRunner `Core` project.

Because of that, it is recommended that you get the entire solution and run this sample from there.
The following example runs it from the solution folder.

```shell
dotnet run --project .\SampleConsoleApp\SampleConsoleApp.csproj
```

## Scenario

As a sample scenario, this application assumes that it has received order data from users and performs the following processing on that data.

1. **Order Validation:** Validate the order data.
2. **Item Expansion:** Expand valid orders into shipment tasks.
3. **Package Planning:** Group shipment tasks into packages.

> [!IMPORTANT]  
> This is only a simulation for sample purposes.
> It does not perform real network communication, file generation, or external API calls.

## Program Flow

At a high level, this application runs as follows.

1. Start from the `Main()` method in `Program.cs`.
2. Create a Host.
3. Start the Host and get a Run.
4. Wait for the Run to complete and get a `RunOutcome`.
5. Output the results.

### Creating the Host

The Host is the top-level controller in the BeltRunner model.
Several preparation steps are performed before it is created.

#### Initial Data

_Reference ID: `Ref01`_

```csharp
IReadOnlyList<OrderData> initialOrders = CreateInitialOrders();
ListArtifactKey<OrderData> incomingOrdersKey = new(ArtifactName.Create(OrderValidationPhaseFactory.INCOMING_ORDERS));
IReadOnlyList<IProducedArtifact> initialArtifacts = [
    ArtifactSeeds.Seed(incomingOrdersKey, initialOrders)
];
```

This creates the initial data that is passed to the first phase.
Because this is a sample, the data is simple hard-coded test data.
Those values are then stored in a list of `IProducedArtifact`.

The name `Produce` may feel a little strange at first.
That is because BeltRunner follows the same idea even for the first phase: the `Consume` input of one phase is the `Produce` output of the previous phase.
There is no previous phase here, but the initial data is still created as produced data.

> [!NOTE]  
> `Produce` and `Artifact` appear here for the first time, but they are explained later in the section [Defining Consume and Produce](#defining-consume-and-produce).

#### Creating the Plan

_Reference ID: `Ref02`_

```csharp
SequentialPlan plan = new SequentialPlan(
    new OrderValidationPhaseFactory(),
    new ItemExpansionPhaseFactory(),
    new PackagePlanningPhaseFactory());
```

This creates a Plan by specifying the factory for each phase in order.

#### Creating the Host Instance

_Reference ID: `Ref03`_

```csharp
using IHost host = new Host();
```

This creates a Host instance.
In this sample, the default constructor is used, but you can also create a Host with `HostOptions` to configure various options.
That approach is explained in [SampleWebApp](../SampleWebApp/README.md).

### Starting the Process

_Reference ID: `Ref04`_

```csharp
using IRun run = await host.StartAsync(plan, initialArtifacts).ConfigureAwait(false);
```

Now that the setup is complete, the Host method can be called to start processing.
The process starts with the Plan and the initial data that were created earlier.

The return value is an `IRun`, which represents the execution state of the process.

Note that this only waits until the process has started. It does not wait for the entire process to finish.

### Progress Output

_Reference ID: `Ref05`_

```csharp
using IDisposable progressSubscription = ObserveProgress(run);
```

To print progress from each phase to the console, this sample subscribes to `SnapshotStream` in `ObserveProgress()`.

```csharp
return run.SnapshotStream.Subscribe(snapshot => {
    string? line = FormatProgress(snapshot);
    if( line is null || string.Equals(line, lastLine, StringComparison.Ordinal) ) {
        return;
    }

    lastLine = line;
    Console.WriteLine(line);
});
```

In this way, phases publish their state and unit-processing progress through Rx.

In this sample, the subscription starts after the Run has started, but this observable is replayable, so you can still receive information from before the subscription began.
You can also subscribe before the Run starts by using a callback.
That approach is explained in [SampleWebApp](../SampleWebApp/README.md).

### Waiting for Completion

_Reference ID: `Ref06`_

```csharp
RunOutcome settledOutcome = await run.Completion.ConfigureAwait(false);
```

You can wait for completion by awaiting the `Completion` property of the Run.

If you want to do some other asynchronous work that is unrelated to BeltRunner while the Run is still in progress, you can place that code between startup and the completion wait.

> [!NOTE]  
> This sample waits for completion by using `Completion`, but a callback that runs when processing completes is also available as an option.
> That callback is `OnCompletedAsync` under `RunLaunchOptions.LifecycleCallbacks`.

### Displaying Results

#### Diagnostic Log

_Reference ID: `Ref10`_

```csharp
WriteWarnings(run.DiagnosticLog);
```

`DiagnosticLog` is a kind of structured log that is produced while phases are running.
In the example above, only warnings are written to the console.

This log has three main roles.

- Output information, warnings, and errors from phases.
- Make those entries observable through the public surface of `IRun`.
- Show sanitized fault information instead of raw exceptions when exceptions are involved.

> [!NOTE]  
> `DiagnosticLog` is a static history and is generally used after the Run has finished.
> `DiagnosticStream`, which lets you subscribe to the same information as a stream, is also available.

The actual log entries are produced by the phase implementations.

#### Run Result

_Reference ID: `Ref11`_

```csharp
if( settledOutcome is {Kind: RunOutcomeKind.Faulted, FaultInfo: not null} ) {
    Console.WriteLine("Result: Faulted");
    Console.WriteLine($"Fault kind: {settledOutcome.FaultInfo.FaultKind}");
    Console.WriteLine($"Fault message: {settledOutcome.FaultInfo.PublicMessage}");
    return;
}
```

`RunOutcome` contains the result of the Run.
In the example above, only the faulted result is displayed.

#### Phase Status

_Reference ID: `Ref12`_

```csharp
foreach( IPhaseSnapshot phase in run.Snapshot.Phases ) {
    int totalUnits = phase.TotalUnits ?? phase.Units.Count;
    Console.WriteLine($"- {phase.PhaseName}: {phase.Status} ({phase.ProcessedUnits}/{totalUnits} units)");
}
```

This gets the state and results of each phase from `Snapshot` and displays them.

`Snapshot` is not only for checking results after completion. It can also be used while the Run is still in progress to inspect the current execution state.
It includes phase snapshots, which are retrieved and displayed here.

> [!NOTE]  
> `Snapshot` is static data that represents the latest known state at the time you access it.
> `SnapshotStream`, which lets you subscribe to the same information as a stream, is also available.

> [!NOTE]  
> A phase `Status` and the overall Run `Outcome` do not necessarily mean the same thing.  
> In this sample, each phase itself runs to completion, so the phase status is shown as `Completed`. However, if some orders are put on hold during `Order Validation`, that phase result becomes a partial success, and the overall Run `Outcome` also becomes `PartiallySucceeded`.

## Creating a Phase Factory

When you create a Phase, which is the actual unit of processing in BeltRunner, you also create a factory that is responsible for instantiating that phase.
This section uses `ItemExpansionPhaseFactory`, the factory for `ItemExpansionPhase`, as an example.

### Constructor

To create a phase factory class, implement it by inheriting from the abstract base class `PhaseFactoryBase<TSelf>`.

_Reference ID: `Ref20`_

```csharp
public ItemExpansionPhaseFactory() : base("item-expansion") {
    ListArtifactKey<OrderData> validatedOrders = new(ArtifactName.Create(VALIDATED_ORDERS));
    ListArtifactKey<ShipmentData> shipmentTasks = new(ArtifactName.Create(SHIPMENT_TASKS));

    ValidatedOrdersKey = Consume(validatedOrders);
    ShipmentTasksKey = Produce(shipmentTasks);
}
```

The constructor of `PhaseFactoryBase` requires a value called a Phase Key, so the factory you create also needs a constructor.
The Phase Key is a simple string value, and you provide a unique string that identifies the phase.

### Defining Consume and Produce

The factory needs to define what the phase requires and what it outputs.
That is what enables data to move between phases.

`Consume` defines the values that the phase needs at execution time, and `Produce` defines the values that the phase generates as a result.
Both are methods provided by `PhaseFactoryBase`.

The important concept here is the Artifact.

Instead of using an arbitrary type directly in the phase contract, BeltRunner wraps the type as an Artifact.
You can think of it as a container used for passing data between phases.

An Artifact has a key, `ArtifactKey`, and that key is used to identify the artifact.

Inside the constructor, those definitions are created by calling methods like this.

```csharp
ListArtifactKey<OrderData> validatedOrders = new(ArtifactName.Create(VALIDATED_ORDERS));
ListArtifactKey<ShipmentData> shipmentTasks = new(ArtifactName.Create(SHIPMENT_TASKS));

ValidatedOrdersKey = Consume(validatedOrders);
ShipmentTasksKey = Produce(shipmentTasks);
```

First, this phase needs order data of type `OrderData` as input.
It creates the artifact key `validatedOrders` and registers it with `Consume()`.
Note that this is not the actual data itself.

Then, this phase generates data of type `ShipmentData` as a processing result.
So it creates the artifact key `shipmentTasks` and registers it with `Produce()`.

### Create()

_Reference ID: `Ref21`_

```csharp
public override IPhase Create() {
    return new ItemExpansionPhase();
}
```

When processing starts through `StartAsync()`, the Host calls the factory's `Create()` method and creates a phase instance.

Because of that, this method in the factory must create the appropriate instance and return it.

## Implementing a Phase

The actual processing unit in BeltRunner is the Phase.
In this sample, the phase is implemented as shown below.
This section uses `ItemExpansionPhase` as an example.

### Phase Class

To implement your own processing as a Phase, inherit from the abstract base class `PhaseBase<TFactory>`.

The phase constructor is optional. BeltRunner does not require any specific phase constructor shape.
In practice, BeltRunner does not instantiate the phase directly. It only executes the factory that corresponds to the phase.
Because the factory implementation is also controlled by the user, how the factory uses constructors is also up to the user.

### ExecuteAsync()

_Reference ID: `Ref22`_

```csharp
public override async Task<IPhaseOutcome> ExecuteAsync(
    IPhaseContext<ItemExpansionPhaseFactory> context, 
    CancellationToken ct = default
)
```

The core of a phase is the `ExecuteAsync()` method.
When `StartAsync()` is called, the Host eventually executes this method.

The actual work is implemented inside this method. The following sections show some common ways a phase is used in this sample.

#### Getting Consume Values

If the factory defines it that way, the `Produce` output of the previous phase can be obtained by the current phase as `Consume`.

Consume values are available from `IPhaseContext<ItemExpansionPhaseFactory> context`.

_Reference ID: `Ref23`_

```csharp
IReadOnlyList<OrderData> validatedOrders = 
    context.Artifacts.GetRequired(context.Factory.ValidatedOrdersKey);
```

#### Creating Units

_Reference ID: `Ref24`_

```csharp
this.Units.AddRangeAndLock(allUnits);
```

This creates Units, which are BeltRunner's in-phase representation of the actual processing targets, from the real data of type `ShipmentData`.
In this sample, the type is `ShipmentUnit`.
Those units are then registered through a method on the base class.

A Unit is a class that wraps one processing target. That allows BeltRunner to do things like the following.

- Include the current processing targets in snapshots.
- Track per-unit progress in telemetry.
- Associate diagnostics with a specific unit.

A unit class can be created by inheriting from `Unit<T>`.

> [!NOTE]  
> In this sample, separate Unit types are created for `OrderData`, `PackageData`, and `ShipmentData`.
> However, if you do not need to add any custom properties or behavior, you could also create and reuse one generic type such as `GenericUnit<T>`.

_Reference ID: `Ref25`_

```csharp
using ITrackedUnitScope trackedUnit = tracker.BeginUnit(unit);
allTasks.Add(unit.Data);
//  Do any actual process here
await Task.Delay(TimeSpan.FromSeconds(1), ct).ConfigureAwait(false);
trackedUnit.Complete();
```

This lets the phase notify the outside world about the start and completion of work for each unit.
In this sample, it simply waits for one second to simulate the work.

#### Configuring Produce

_Reference ID: `Ref26`_

```csharp
IPhaseOutcome outcome = new PhaseOutcome()
    .Produce(context.Factory.ShipmentTasksKey, allTasks);
```

The result of the phase can be registered as `Produce` and made available to the next phase or to external code.
The next phase can then retrieve those values as `Consume`.

#### IPhaseOutcome

_Reference ID: `Ref26`_

```csharp
IPhaseOutcome outcome = new PhaseOutcome()
    .Produce(context.Factory.ShipmentTasksKey, allTasks);
```

When phase processing finishes, it must return an `IPhaseOutcome`.

If processing needs to stop because of an exception, invalid data, cancellation, or something similar, it should return with the appropriate `PhaseResult`.

#### Cancellation Handling

As shown by `ct.ThrowIfCancellationRequested();` in the code, this sample is implemented to respond to external cancellation.
If cancellation happens, `OperationCanceledException` is thrown, and the Host handles it as follows.

- The phase does not return `PhaseResult.Succeeded`.
- The Run becomes `RunOutcomeKind.Cancelled`.
- The corresponding phase in the Snapshot is also treated as cancelled.

## Other Samples

This solution also includes the following sample.

- [SampleWebApp](../SampleWebApp/README.md)
