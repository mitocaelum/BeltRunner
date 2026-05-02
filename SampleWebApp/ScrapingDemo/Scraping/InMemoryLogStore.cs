using NLog.Targets;

namespace BeltRunner.SampleWebApp.ScrapingDemo.Scraping;

internal sealed class InMemoryLogStore {
    public InMemoryLogStore(MemoryTarget target) {
        this.Target = target ?? throw new ArgumentNullException(nameof(target));
    }

    public MemoryTarget Target { get; }

    public IReadOnlyList<string> GetSnapshot() {
        return this.Target.Logs.ToArray();
    }
}
