using BeltRunner.Core.Units;
using BeltRunner.SampleConsoleApp.Containers;

namespace BeltRunner.SampleConsoleApp.Units;

internal sealed class PackageUnit : Unit<PackageData> {
    public PackageUnit(PackageData data) : base(data, data.PackageId) {
    }
}
