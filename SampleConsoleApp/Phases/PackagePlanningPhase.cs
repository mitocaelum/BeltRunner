using BeltRunner.Core.Phase;
using BeltRunner.Core.Units;
using BeltRunner.SampleConsoleApp.Containers;
using BeltRunner.SampleConsoleApp.Units;

namespace BeltRunner.SampleConsoleApp.Phases;

/// <summary>
/// Groups shipment tasks into package records that describe how work will leave the warehouse.
/// </summary>
/// <remarks>
/// <para>
/// This phase is the final transformation step in the sample pipeline.
/// It shows how a phase can consume a flat task list, apply business grouping rules,
/// and publish a higher-level planning artifact.
/// </para>
/// <para>
/// The grouping logic keeps bulky tasks separate and upgrades the main package to insulated handling
/// when chilled shipment work is present.
/// That gives readers a small but realistic example of a domain-specific aggregation rule.
/// </para>
/// </remarks>
internal sealed class PackagePlanningPhase : PhaseBase<PackagePlanningPhaseFactory> {
    /// <summary>
    /// Gets the display name shown in snapshots and diagnostics for this phase.
    /// </summary>
    public override string Name => "Package Planning";

    /// <summary>
    /// Gets the tracked units that represent the packages created by this phase.
    /// </summary>
    /// <remarks>
    /// Each tracked unit maps to one planned package so final snapshots can show the exact package count
    /// produced by the grouping rules.
    /// </remarks>
    public override UnitSet Units { get; } = new();

    /// <summary>
    /// Builds the package plan from the shipment-task list and publishes the final package artifact.
    /// </summary>
    /// <param name="context">
    /// The phase execution context that provides artifact access and telemetry reporting.
    /// </param>
    /// <param name="ct">
    /// A cancellation token that stops package planning cooperatively before the next package is processed.
    /// </param>
    /// <returns>
    /// A completed task whose result contains the package plan for the run.
    /// The sample always reports <see cref="PhaseResult.Succeeded"/> because the planning rules are deterministic
    /// once the shipment-task list has been produced successfully.
    /// </returns>
    public override Task<IPhaseOutcome> ExecuteAsync(IPhaseContext<PackagePlanningPhaseFactory> context, CancellationToken ct = default) {
        IReadOnlyList<ShipmentData> shipmentTasks = context.Artifacts.GetRequired(context.Factory.ShipmentTasksKey);
        List<PackageData> packages = PlanPackages(shipmentTasks);
        List<PackageUnit> units = new(packages.Count);

        for( int i = 0; i < packages.Count; i++ ) {
            PackageUnit unit = new(packages[i]);
            units.Add(unit);
        }

        this.Units.AddRangeAndLock(units);
        using IPhaseProgressTracker tracker = context.Telemetry.BeginPhaseProgressTracking(packages.Count);

        foreach( PackageUnit unit in units ) {
            ct.ThrowIfCancellationRequested();
            using ITrackedUnitScope trackedUnit = tracker.BeginUnit(unit);
            trackedUnit.Complete();
        }

        context.Telemetry.Info($"Package planning completed. packages={packages.Count}.");

        IPhaseOutcome outcome = new PhaseOutcome(PhaseResult.Succeeded)
            .Produce(context.Factory.PlannedPackagesKey, packages);

        return Task.FromResult(outcome);
    }

    private static List<PackageData> PlanPackages(IReadOnlyList<ShipmentData> shipmentTasks) {
        List<PackageData> packages = new();
        int packageSequence = 1001;

        IEnumerable<IGrouping<string, ShipmentData>> groupedByOrder = shipmentTasks
            .GroupBy(task => task.OrderId)
            .OrderBy(group => group.Key, StringComparer.Ordinal);

        foreach( IGrouping<string, ShipmentData> orderGroup in groupedByOrder ) {
            List<ShipmentData> tasks = orderGroup.ToList();

            int bulkyTaskCount = tasks.Count(task => string.Equals(task.ShippingCategory, "Bulky", StringComparison.OrdinalIgnoreCase));
            int mainTaskCount = tasks.Count - bulkyTaskCount;

            if( mainTaskCount > 0 ) {
                bool hasChilled = tasks.Any(task => string.Equals(task.ShippingCategory, "Chilled", StringComparison.OrdinalIgnoreCase));
                string packageType = hasChilled ? "Insulated" : "Standard";

                packages.Add(new PackageData(
                    PackageId: $"P-{packageSequence++}",
                    OrderId: orderGroup.Key,
                    TaskCount: mainTaskCount,
                    PackageType: packageType));
            }

            if( bulkyTaskCount > 0 ) {
                packages.Add(new PackageData(
                    PackageId: $"P-{packageSequence++}",
                    OrderId: orderGroup.Key,
                    TaskCount: bulkyTaskCount,
                    PackageType: "Bulky"));
            }
        }

        return packages;
    }
}
