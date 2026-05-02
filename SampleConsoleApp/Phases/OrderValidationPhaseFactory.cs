using BeltRunner.Core.Phase;
using BeltRunner.Core.Plan.Artifacts;
using BeltRunner.SampleConsoleApp.Containers;

namespace BeltRunner.SampleConsoleApp.Phases;

/// <summary>
/// Creates the phase that validates the seeded order input before any downstream shipment work begins.
/// </summary>
/// <remarks>
/// <para>
/// This factory is the entry point of the sample pipeline.
/// It exposes the artifact names as public constants and creates the typed artifact keys inside the factory.
/// This keeps the sample intentionally explicit so readers can see how a phase factory defines its own input
/// and output keys without relying on a shared artifact catalog type.
/// </para>
/// <para>
/// Readers can use this type as the reference example for how a sample phase factory wires artifact flow
/// without exposing any additional configuration surface.
/// </para>
/// </remarks>
internal sealed class OrderValidationPhaseFactory : PhaseFactoryBase<OrderValidationPhaseFactory> {
    /// <summary>
    /// Gets the artifact name used for the seeded incoming order list.
    /// </summary>
    public const string INCOMING_ORDERS = "incomingOrders";

    /// <summary>
    /// Gets the artifact name used for the validated order list produced by this phase.
    /// </summary>
    public const string VALIDATED_ORDERS = "validatedOrders";

    /// <summary>
    /// Gets the seeded incoming-order artifact consumed by this phase.
    /// </summary>
    public ListArtifactKey<OrderData> IncomingOrdersKey { get; }

    /// <summary>
    /// Gets the validated-order artifact produced by this phase.
    /// </summary>
    public ListArtifactKey<OrderData> ValidatedOrdersKey { get; }

    /// <summary>
    /// Initializes a new instance of the <see cref="OrderValidationPhaseFactory"/> class.
    /// </summary>
    /// <remarks>
    /// The phase key is fixed to <c>order-validation</c> so the sample has a stable identifier in
    /// runtime snapshots, diagnostics, and plan inspection.
    /// </remarks>
    public OrderValidationPhaseFactory() : base("order-validation") {
        ListArtifactKey<OrderData> incomingOrders = new(ArtifactName.Create(INCOMING_ORDERS));
        ListArtifactKey<OrderData> validatedOrders = new(ArtifactName.Create(VALIDATED_ORDERS));

        IncomingOrdersKey = Consume(incomingOrders);
        ValidatedOrdersKey = Produce(validatedOrders);
    }

    /// <summary>
    /// Creates a new <see cref="OrderValidationPhase"/> instance for one run.
    /// </summary>
    /// <returns>
    /// A phase that reads incoming orders, evaluates whether each order can continue,
    /// and writes the validated order list for the next stage.
    /// </returns>
    public override IPhase Create() {
        return new OrderValidationPhase();
    }
}
