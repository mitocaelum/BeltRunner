using BeltRunner.Core.Execution;

namespace BeltRunner.SampleWebApp.ScrapingDemo;

/// <summary>
/// Preserves operator-facing cancellation details while keeping other public fault messages sanitized.
/// </summary>
internal sealed class SamplePublicFaultInfoPolicy : IPublicFaultInfoPolicy {
    /// <inheritdoc />
    public PublicFaultInfo Create(Exception exception, string origin) {
        if( exception is null ) {
            throw new ArgumentNullException(nameof(exception));
        }

        if( origin is null ) {
            throw new ArgumentNullException(nameof(origin));
        }

        if( exception is OperationCanceledException ) {
            string cancellationMessage = string.IsNullOrWhiteSpace(exception.Message)
                ? "The operator stopped the run."
                : exception.Message;

            return new PublicFaultInfo(
                exception.GetType().Name,
                cancellationMessage,
                null,
                origin,
                DateTimeOffset.UtcNow);
        }

        return new PublicFaultInfo(
            exception.GetType().Name,
            GetSanitizedMessage(origin),
            null,
            origin,
            DateTimeOffset.UtcNow);
    }

    private static string GetSanitizedMessage(string origin) {
        if( origin.StartsWith("phase:", StringComparison.Ordinal) ) {
            return "The phase failed with an unhandled exception.";
        }

        return origin switch {
            "host" => "The host failed while managing the active run.",
            "run" => "The run failed with an unhandled exception.",
            _ => "An unhandled fault occurred."
        };
    }
}
