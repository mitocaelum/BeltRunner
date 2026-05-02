using System.Collections.Concurrent;
using System.Diagnostics;

namespace BeltRunner.Core.TEST.Testing;

internal static class HumanReadableTestLog {
    private static readonly ConcurrentDictionary<string, Stopwatch> Stopwatches = new();
    private static readonly ConcurrentDictionary<string, ConcurrentQueue<string>> Observations = new();

    public static void Begin(string testId) {
        Observations[testId] = new ConcurrentQueue<string>();
        Stopwatches[testId] = Stopwatch.StartNew();
    }

    public static void Observe(string testId, string observation) {
        if( string.IsNullOrWhiteSpace(observation) ) {
            return;
        }

        ConcurrentQueue<string> lines = Observations.GetOrAdd(testId, static _ => new ConcurrentQueue<string>());
        lines.Enqueue(observation.Trim());
    }

    public static HumanReadableTestCompletion Complete(string testId) {
        TimeSpan duration = TimeSpan.Zero;

        if( Stopwatches.TryRemove(testId, out Stopwatch? stopwatch) ) {
            stopwatch.Stop();
            duration = stopwatch.Elapsed;
        }

        List<string> observed = new();

        if( Observations.TryRemove(testId, out ConcurrentQueue<string>? lines) ) {
            observed.AddRange(lines);
        }

        return new HumanReadableTestCompletion(duration, observed);
    }
}
