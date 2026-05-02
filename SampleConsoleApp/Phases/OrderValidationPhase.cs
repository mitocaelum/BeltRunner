using BeltRunner.Core.Phase;
using BeltRunner.Core.Units;
using BeltRunner.SampleConsoleApp.Containers;
using BeltRunner.SampleConsoleApp.Units;

namespace BeltRunner.SampleConsoleApp.Phases;

/// <summary>
/// Validates incoming orders and decides whether each order can continue into shipment preparation.
/// </summary>
/// <remarks>
/// <para>
/// This phase is intentionally straightforward for first-time BeltRunner readers.
/// It demonstrates three core ideas at once: reading a typed artifact, registering tracked units,
/// and returning a produced artifact through <see cref="IPhaseOutcome"/>.
/// </para>
/// <para>
/// Orders that fail validation are not removed from the pipeline payload.
/// Instead, they are returned with hold information so later phases can make an explicit decision to skip them.
/// That keeps the artifact history easy to inspect after the run finishes.
/// </para>
/// </remarks>
internal sealed class OrderValidationPhase : PhaseBase<OrderValidationPhaseFactory> {
    /// <summary>
    /// Gets the display name shown in snapshots and diagnostics for this phase.
    /// </summary>
    public override string Name => "Order Validation";

    /// <summary>
    /// Gets the tracked units that represent the orders evaluated by this phase.
    /// </summary>
    /// <remarks>
    /// Each unit corresponds to one input order so runtime observers can see which orders completed successfully
    /// and which orders finished with a warning state.
    /// </remarks>
    public override UnitSet Units { get; } = new();

    /// <summary>
    /// Validates each incoming order and produces the normalized order list for downstream phases.
    /// </summary>
    /// <param name="context">
    /// The phase execution context that provides artifact access and telemetry reporting.
    /// </param>
    /// <param name="ct">
    /// A cancellation token that stops validation cooperatively before the next order is processed.
    /// </param>
    /// <returns>
    /// A completed task whose result contains the validated order list.
    /// The phase returns <see cref="PhaseResult.PartiallySucceeded"/> when one or more orders are placed on hold;
    /// otherwise it returns <see cref="PhaseResult.Succeeded"/>.
    /// </returns>
    public override Task<IPhaseOutcome> ExecuteAsync(IPhaseContext<OrderValidationPhaseFactory> context, CancellationToken ct = default) {
        IReadOnlyList<OrderData> incomingOrders = context.Artifacts.GetRequired(context.Factory.IncomingOrdersKey);
        List<OrderData> validatedOrders = new(incomingOrders.Count);
        List<OrderUnit> units = new(incomingOrders.Count);

        for( int i = 0; i < incomingOrders.Count; i++ ) {
            OrderData order = incomingOrders[i];
            OrderUnit unit = new(order);
            units.Add(unit);
        }

        this.Units.AddRangeAndLock(units);
        using IPhaseProgressTracker tracker = context.Telemetry.BeginPhaseProgressTracking(incomingOrders.Count);

        int onHoldCount = 0;

        for( int i = 0; i < incomingOrders.Count; i++ ) {
            ct.ThrowIfCancellationRequested();

            OrderData order = incomingOrders[i];
            OrderUnit unit = units[i];
            using ITrackedUnitScope trackedUnit = tracker.BeginUnit(unit);

            OrderData validated = Validate(order);
            validatedOrders.Add(validated);
            trackedUnit.Complete();

            if( validated.IsOnHold ) {
                onHoldCount++;
                context.Telemetry.SetUnitStatus(unit.Id, UnitStatus.Warning);
                context.Telemetry.Warn($"Order {validated.OrderId} is on hold because {validated.HoldReason}.", unitId: unit.Id);
            }
        }

        context.Telemetry.Info($"Validation completed. ready={validatedOrders.Count - onHoldCount}, onHold={onHoldCount}.");

        IPhaseOutcome outcome = new PhaseOutcome(onHoldCount > 0 ? PhaseResult.PartiallySucceeded : PhaseResult.Succeeded)
            .Produce(context.Factory.ValidatedOrdersKey, validatedOrders);

        return Task.FromResult(outcome);
    }

    private static OrderData Validate(OrderData order) {
        List<string> issues = new();

        if( string.IsNullOrWhiteSpace(order.OrderId) )
            issues.Add("order id is missing");

        if( string.IsNullOrWhiteSpace(order.Address) )
            issues.Add("shipping address is missing");

        if( !order.PaymentConfirmed )
            issues.Add("payment is not confirmed");

        if( order.ItemCount < 1 )
            issues.Add("order has no items");

        return issues.Count == 0
            ? order with { IsOnHold = false, HoldReason = string.Empty }
            : order with { IsOnHold = true, HoldReason = string.Join(", ", issues) };
    }
}
