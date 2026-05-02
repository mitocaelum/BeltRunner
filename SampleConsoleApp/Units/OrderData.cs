namespace BeltRunner.SampleConsoleApp.Containers;

internal sealed record OrderData(
    string OrderId,
    string CustomerName,
    string Address,
    bool PaymentConfirmed,
    int ItemCount,
    bool RequiresChilledHandling,
    bool ContainsBulkyItem,
    bool IsOnHold = false,
    string HoldReason = "");
