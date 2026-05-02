using BeltRunner.Core.Phase;
using BeltRunner.SampleConsoleApp.Containers;
using BeltRunner.SampleConsoleApp.Units;

namespace BeltRunner.SampleConsoleApp.Phases;

/// <summary>
/// Converts validated orders into shipment-task records that describe the physical work to be prepared.
/// </summary>
/// <remarks>
/// <para>
/// This phase demonstrates a common BeltRunner pattern where one upstream record can expand into multiple
/// downstream units of work.
/// In the sample, larger orders create more shipment tasks, and special handling flags influence the task category.
/// </para>
/// <para>
/// Orders already marked as on hold are intentionally skipped here.
/// The phase emits a warning diagnostic for those orders so the final run summary still makes the decision visible.
/// </para>
/// </remarks>
internal sealed class ItemExpansionPhase : PhaseBase<ItemExpansionPhaseFactory> {
    /// <summary>
    /// Gets the display name shown in snapshots and diagnostics for this phase.
    /// </summary>
    public override string Name => "Item Expansion";

    /// <summary>
    /// Gets the tracked units that represent the shipment tasks created by this phase.
    /// </summary>
    /// <remarks>
    /// Units are registered before processing begins so snapshot readers can see the full expected task set
    /// as soon as the phase starts work.
    /// </remarks>
    public override UnitSet Units { get; } = new();

    //  Ref22
    /// <summary>
    /// Expands non-held orders into shipment tasks and publishes the resulting task list.
    /// </summary>
    /// <param name="context">
    /// The phase execution context that provides artifact access and telemetry reporting.
    /// </param>
    /// <param name="ct">
    /// A cancellation token that stops expansion cooperatively before the next order or task is processed.
    /// </param>
    /// <returns>
    /// A completed task whose result contains the generated shipment tasks.
    /// This sample phase always reports <see cref="PhaseResult.Succeeded"/> because held orders are treated
    /// as an expected upstream business decision rather than a failure inside expansion.
    /// </returns>
    public override async Task<IPhaseOutcome> ExecuteAsync(IPhaseContext<ItemExpansionPhaseFactory> context, CancellationToken ct = default) {
        //  Ref23: Get the previous phase's produce
        IReadOnlyList<OrderData> validatedOrders = context.Artifacts.GetRequired(context.Factory.ValidatedOrdersKey);
        
        //  Get the valid, not-hold, orders
        List<OrderData> validOrders = validatedOrders.Where(order => !order.IsOnHold).ToList();
        
        Dictionary<string, List<ShipmentUnit>> unitsByOrder = new(StringComparer.Ordinal);
        List<ShipmentUnit> allUnits = new();

        int expectedTaskCount = 0;
        foreach( OrderData order in validOrders ) {
            int taskCount = CalculateTaskCount(order);
            List<ShipmentData> tasks = CreateTasks(order, taskCount);
            List<ShipmentUnit> taskUnits = new(tasks.Count);

            for( int i = 0; i < tasks.Count; i++ ) {
                ShipmentUnit unit = new(tasks[i]);
                taskUnits.Add(unit);
                allUnits.Add(unit);
            }

            unitsByOrder.Add(order.OrderId, taskUnits);
            expectedTaskCount += tasks.Count;
        }

        //  Ref24
        Units.AddRangeAndLock(allUnits);
        using IPhaseProgressTracker tracker = context.Telemetry.BeginPhaseProgressTracking(expectedTaskCount);

        List<ShipmentData> allTasks = new(expectedTaskCount);

        foreach( OrderData order in validatedOrders ) {
            ct.ThrowIfCancellationRequested();

            if( order.IsOnHold ) {
                context.Telemetry.Warn($"Order {order.OrderId} remained on hold and did not create shipment tasks.");
                continue;
            }

            List<ShipmentUnit> created = unitsByOrder[order.OrderId];

            for( int i = 0; i < created.Count; i++ ) {
                ct.ThrowIfCancellationRequested();

                ShipmentUnit unit = created[i];
                //  Ref25
                using ITrackedUnitScope trackedUnit = tracker.BeginUnit(unit);
                allTasks.Add(unit.Data);
                //  Do any actual process here
                await Task.Delay(TimeSpan.FromSeconds(1), ct).ConfigureAwait(false);
                trackedUnit.Complete();
            }
        }

        context.Telemetry.Info($"Expansion completed. shipmentTasks={allTasks.Count}.");

        //  Ref26
        IPhaseOutcome outcome = new PhaseOutcome()
            .Produce(context.Factory.ShipmentTasksKey, allTasks);

        return outcome;
    }

    private static int CalculateTaskCount(OrderData order) {
        if( order.ItemCount <= 2 )
            return 1;

        if( order.ItemCount <= 4 )
            return 2;

        return 3;
    }

    private static List<ShipmentData> CreateTasks(OrderData order, int taskCount) {
        List<ShipmentData> tasks = new(taskCount);

        for( int i = 1; i <= taskCount; i++ ) {
            string category = "Standard";

            if( order.RequiresChilledHandling && i == 1 ) {
                category = "Chilled";
            } else if( order.ContainsBulkyItem && i == taskCount ) {
                category = "Bulky";
            }

            tasks.Add(new ShipmentData(
                TaskId: $"T-{order.OrderId}-{i:00}",
                OrderId: order.OrderId,
                ItemName: $"Item-{i}",
                Quantity: i == taskCount && order.ItemCount > 3 ? 2 : 1,
                ShippingCategory: category));
        }

        return tasks;
    }
}
