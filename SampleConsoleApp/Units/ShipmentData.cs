namespace BeltRunner.SampleConsoleApp.Containers;

internal sealed record ShipmentData(
    string TaskId,
    string OrderId,
    string ItemName,
    int Quantity,
    string ShippingCategory);
