using BeltRunner.Core.Units;
using BeltRunner.SampleConsoleApp.Containers;

namespace BeltRunner.SampleConsoleApp.Units;

internal sealed class OrderUnit : Unit<OrderData> {
    public OrderUnit(OrderData data) : base(data, data.OrderId) {
    }
}
