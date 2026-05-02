using BeltRunner.Core.Execution.Interaction;
using BeltRunner.Core.Phase;
using BeltRunner.Core.Units;
using BeltRunner.SampleWebApp.ScrapingDemo.Scraping;

namespace BeltRunner.SampleWebApp.ScrapingDemo.Phases;

/// <summary>
/// Simulates the discovery of target page URLs from the operator-provided source URL.
/// </summary>
internal sealed class DiscoverTargetsPhase : IPhase {
    private const string AUTHENTICATION_CHALLENGE_KIND = "phase1-auth-challenge";

    //  RefID: RefPhaseName
    /// <summary>
    /// Gets the display name shown for the phase.
    /// </summary>
    public string Name => "Discover Target Pages";

    //  RefID: RefPhaseUnits
    /// <summary>
    /// Gets the unit collection that represents the work handled by the phase.
    /// </summary>
    public UnitSet Units { get; } = new();

    /// <summary>
    /// Executes the phase and produces a list of sample target page URLs.
    /// </summary>
    /// <param name="context">The execution context that provides artifacts, telemetry, and interactions.</param>
    /// <param name="ct">The cancellation token used to stop the simulated work.</param>
    /// <returns>The phase outcome that publishes the discovered page URLs.</returns>
    public async Task<IPhaseOutcome> ExecuteAsync(IPhaseContext context, CancellationToken ct = default) {
        //  RefID: RefPhaseExecuteAsync
        string sourceUrl = context.Artifacts.GetRequired(ScrapingArtifacts.SourceUrl);
        bool injectAuthenticationChallenge = context.Artifacts.GetRequired(ScrapingArtifacts.InjectAuthenticationChallenge);
        DemoUnit<string> sourceUnit = new(sourceUrl, "Input URL");

        this.Units.AddAndLock(sourceUnit);

        using IPhaseProgressTracker progressTracker = context.Telemetry.BeginPhaseProgressTracking(1);
        using ITrackedUnitScope trackedUnit = progressTracker.BeginUnit(sourceUnit);

        try {
            context.Telemetry.Info("Phase 1 started building the target page list.");

            if( injectAuthenticationChallenge ) {
                await HandleAuthenticationChallengeAsync(context, sourceUnit, ct).ConfigureAwait(false);
            }

            int[] checkpoints = [15, 40, 70, 100];
            for( int i = 0; i < checkpoints.Length; i++ ) {
                int percentage = checkpoints[i];
                context.Telemetry.ReportUnitProgress(sourceUnit, percentage / 100d);
                await Task.Delay(180, ct).ConfigureAwait(false);
            }

            List<string> pages = new(10);
            for( int i = 0; i < 10; i++ ) {
                string suffix = $"page-{i + 1:00}";
                pages.Add($"{sourceUrl.TrimEnd('/')}/{suffix}");
            }

            trackedUnit.Complete();
            context.Telemetry.Info("Phase 1 finished building the target page list.");

            //  RefID: RefPhaseReturn
            return new PhaseOutcome().Produce(ScrapingArtifacts.TargetPages, pages);
        } catch( OperationCanceledException ) {
            context.Telemetry.SetUnitStatus(sourceUnit.Id, UnitStatus.Cancelled);
            throw;
        }
    }

    private static async Task HandleAuthenticationChallengeAsync(IPhaseContext context, DemoUnit<string> sourceUnit, CancellationToken ct) {
        context.Telemetry.Warn("Phase 1 simulated a web authentication challenge.", unitId: sourceUnit.Id);

        //  Ref53
        InteractionRequest<(string UserName, string Password)> request = new(
            AUTHENTICATION_CHALLENGE_KIND,
            context.Key,
            title: "Authentication required",
            message: "The simulated website requested credentials before Phase 1 could continue.");

        //  Ref54
        InteractionResult<(string UserName, string Password)> result = await context.Interaction.TryAskAsync(request, ct).ConfigureAwait(false);
        if( result.IsAccepted ) {
            string userName = string.IsNullOrWhiteSpace(result.Response.UserName) ? "(empty)" : result.Response.UserName.Trim();
            context.Telemetry.Info($"The operator provided credentials for Phase 1. userName={userName}", sourceUnit.Id);
            return;
        }

        context.Telemetry.Warn("The operator canceled the Phase 1 authentication challenge.", unitId: sourceUnit.Id);
        throw new OperationCanceledException("The operator canceled the authentication challenge in Phase 1.");
    }
}
