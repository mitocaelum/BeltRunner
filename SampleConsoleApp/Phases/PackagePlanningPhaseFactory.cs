using BeltRunner.Core.Phase;
using BeltRunner.Core.Plan.Artifacts;
using BeltRunner.SampleConsoleApp.Containers;

namespace BeltRunner.SampleConsoleApp.Phases;

/// <summary>
/// Creates the phase that groups shipment tasks into package records.
/// </summary>
/// <remarks>
/// <para>
/// This factory represents the final sample stage.
/// It exposes its artifact names as public constants and creates the typed keys inside the factory so the sample
/// clearly shows where the artifact contract is declared.
/// </para>
/// <para>
/// The type is useful as a reference when readers want to see the simplest possible factory for a final pipeline step
/// that only transforms one artifact into another.
/// </para>
/// </remarks>
internal sealed class PackagePlanningPhaseFactory : PhaseFactoryBase<PackagePlanningPhaseFactory> {
    /// <summary>
    /// Gets the artifact name used for the shipment task list consumed by this phase.
    /// </summary>
    public const string SHIPMENT_TASKS = "shipmentTasks";

    /// <summary>
    /// Gets the artifact name used for the planned package list produced by this phase.
    /// </summary>
    public const string PLANNED_PACKAGES = "plannedPackages";

    /// <summary>
    /// Gets the shipment-task artifact consumed by this phase.
    /// </summary>
    public ListArtifactKey<ShipmentData> ShipmentTasksKey { get; }

    /// <summary>
    /// Gets the planned-package artifact produced by this phase.
    /// </summary>
    public ListArtifactKey<PackageData> PlannedPackagesKey { get; }

    /// <summary>
    /// Initializes a new instance of the <see cref="PackagePlanningPhaseFactory"/> class.
    /// </summary>
    /// <remarks>
    /// The phase key is fixed to <c>package-planning</c> so the sample exposes a stable identifier for
    /// final pipeline status and diagnostics.
    /// </remarks>
    public PackagePlanningPhaseFactory() : base("package-planning") {
        ListArtifactKey<ShipmentData> shipmentTasks = new(ArtifactName.Create(SHIPMENT_TASKS));
        ListArtifactKey<PackageData> plannedPackages = new(ArtifactName.Create(PLANNED_PACKAGES));

        ShipmentTasksKey = Consume(shipmentTasks);
        PlannedPackagesKey = Produce(plannedPackages);
    }

    /// <summary>
    /// Creates a new <see cref="PackagePlanningPhase"/> instance for one run.
    /// </summary>
    /// <returns>
    /// A phase that groups shipment tasks by order and shipping characteristics to produce package records.
    /// </returns>
    public override IPhase Create() {
        return new PackagePlanningPhase();
    }
}
