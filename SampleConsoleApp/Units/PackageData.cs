namespace BeltRunner.SampleConsoleApp.Containers;

internal sealed record PackageData(
    string PackageId,
    string OrderId,
    int TaskCount,
    string PackageType);
