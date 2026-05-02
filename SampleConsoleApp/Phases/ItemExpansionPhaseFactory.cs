using BeltRunner.Core.Phase;
using BeltRunner.Core.Plan.Artifacts;
using BeltRunner.SampleConsoleApp.Containers;

namespace BeltRunner.SampleConsoleApp.Phases;

/// <summary>
/// Creates the phase that expands validated orders into shipment-task records.
/// </summary>
/// <remarks>
/// <para>
/// This factory models the middle step of the sample pipeline.
/// It demonstrates the typed factory authoring path where artifact keys remain owned by the factory
/// and are surfaced to the phase through the typed execution context.
/// </para>
/// <para>
/// The factory exists mainly to show how artifact dependencies remain explicit without re-passing the same keys
/// through the phase constructor.
/// </para>
/// </remarks>
internal sealed class ItemExpansionPhaseFactory : PhaseFactoryBase<ItemExpansionPhaseFactory> {
    /// <summary>
    /// Gets the artifact name used for the validated order list consumed by this phase.
    /// </summary>
    public const string VALIDATED_ORDERS = "validatedOrders";

    /// <summary>
    /// Gets the artifact name used for the shipment task list produced by this phase.
    /// </summary>
    public const string SHIPMENT_TASKS = "shipmentTasks";

    /// <summary>
    /// Gets the validated-order artifact consumed by this phase.
    /// </summary>
    public ListArtifactKey<OrderData> ValidatedOrdersKey { get; }

    /// <summary>
    /// Gets the shipment-task artifact produced by this phase.
    /// </summary>
    public ListArtifactKey<ShipmentData> ShipmentTasksKey { get; }

    //  Ref20
    /// <summary>
    /// Initializes a new instance of the <see cref="ItemExpansionPhaseFactory"/> class.
    /// </summary>
    /// <remarks>
    /// The phase key is fixed to <c>item-expansion</c> so the sample exposes a stable step name in diagnostics
    /// and final run snapshots.
    /// </remarks>
    public ItemExpansionPhaseFactory() : base("item-expansion") {
        ListArtifactKey<OrderData> validatedOrders = new(ArtifactName.Create(VALIDATED_ORDERS));
        ListArtifactKey<ShipmentData> shipmentTasks = new(ArtifactName.Create(SHIPMENT_TASKS));

        ValidatedOrdersKey = Consume(validatedOrders);
        ShipmentTasksKey = Produce(shipmentTasks);
    }

    //  Ref21
    /// <summary>
    /// Creates a new <see cref="ItemExpansionPhase"/> instance for one run.
    /// </summary>
    /// <returns>
    /// A phase that expands non-held orders into one or more shipment tasks and publishes the resulting task list.
    /// </returns>
    public override IPhase Create() {
        return new ItemExpansionPhase();
    }
}
