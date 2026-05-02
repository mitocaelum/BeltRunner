namespace BeltRunner.Core.TEST.Testing;

internal readonly record struct HumanReadableTestCompletion(TimeSpan Duration, IReadOnlyList<string> Observed);
