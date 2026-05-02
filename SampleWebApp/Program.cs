using BeltRunner.Core.Execution.Interaction;
using BeltRunner.SampleWebApp.Components;
using BeltRunner.Core.Host;
using BeltRunner.SampleWebApp.ScrapingDemo;
using BeltRunner.SampleWebApp.ScrapingDemo.Scraping;
using NLog;
using NLog.Config;
using NLog.Targets;
using HostBuilder = BeltRunner.Core.Host.HostBuilder;

WebApplicationBuilder builder = WebApplication.CreateBuilder(args);
InMemoryLogStore logStore = ConfigureNLog();

builder.Services.AddRazorComponents().AddInteractiveServerComponents();
builder.Services.AddSingleton(logStore);
//  Ref50
builder.Services.AddScoped<ScrapingDemoState>();
builder.Services.AddScoped<BeltRunner.Core.Host.IHost>(_ => CreateBeltRunnerHost());
builder.Services.AddScoped<ScrapingDemoController>();

WebApplication app = builder.Build();

if( !app.Environment.IsDevelopment() ) {
    app.UseExceptionHandler("/Error", createScopeForErrors: true);
    app.UseHsts();
}

app.UseHttpsRedirection();
app.UseStaticFiles();
app.UseAntiforgery();

app.MapRazorComponents<App>().AddInteractiveServerRenderMode();
app.Lifetime.ApplicationStopped.Register(LogManager.Shutdown);

app.Run();

static BeltRunner.Core.Host.IHost CreateBeltRunnerHost() {
    //  Ref51
    return new HostBuilder()
        .UseInteractionBrokerFactory(static () => new InMemoryInteractionBroker())
        .UsePublicFaultInfoPolicy(new SamplePublicFaultInfoPolicy())
        .WithDiagnosticMode(DiagnosticMode.All)
        .Configure(options => {
            // The sample uses bounded retention and light snapshot coalescing so repeated demo runs
            // keep predictable in-memory behavior without hiding the latest observable state.
            options.RunEventLogMaxRetainedCount = 256;
            options.InteractionRequestLogMaxRetainedCount = 32;
            options.InteractionMaxPendingRequestCount = 4;
            options.RunDiagnosticsMaxRetainedCount = 128;
            options.SnapshotPublishCoalescingInterval = TimeSpan.FromMilliseconds(100);
        })
        .Build();
}

static InMemoryLogStore ConfigureNLog() {
    const string LOG_LAYOUT = "${longdate}|${level:uppercase=true}|${logger}|${message}|runId=${event-properties:item=runId}|phaseKey=${event-properties:item=phaseKey}|phaseIndex=${event-properties:item=phaseIndex}|phaseResult=${event-properties:item=phaseResult}|unitId=${event-properties:item=unitId}|cancelReason=${event-properties:item=cancelReason}|severity=${event-properties:item=diagnosticSeverity}|hostState=${event-properties:item=hostState}|eventType=${event-properties:item=eventType}|exceptionType=${exception:format=Type}|exceptionMessage=${exception:format=Message}";

    MemoryTarget target = new("beltRunnerMemory") {
        Layout = LOG_LAYOUT,
        MaxLogsCount = 1000
    };

    LoggingConfiguration configuration = LogManager.Configuration ?? new LoggingConfiguration();
    configuration.AddTarget(target);
    configuration.LoggingRules.Add(new LoggingRule("BeltRunner.*", NLog.LogLevel.Trace, target));
    LogManager.Configuration = configuration;
    LogManager.ReconfigExistingLoggers();

    return new InMemoryLogStore(target);
}
