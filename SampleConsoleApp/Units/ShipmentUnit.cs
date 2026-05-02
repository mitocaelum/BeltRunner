using BeltRunner.Core.Units;
using BeltRunner.SampleConsoleApp.Containers;

namespace BeltRunner.SampleConsoleApp.Units;

internal sealed class ShipmentUnit : Unit<ShipmentData> {
    public ShipmentUnit(ShipmentData data) : base(data, data.TaskId) {
    }
}
