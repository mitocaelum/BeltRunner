using System;

namespace BeltRunner.Core.Execution;

internal sealed class DefaultPublicFaultInfoPolicy : IPublicFaultInfoPolicy {
    public PublicFaultInfo Create(Exception exception, string origin) {
        if( exception is null ) throw new ArgumentNullException(nameof(exception));
        if( origin is null ) throw new ArgumentNullException(nameof(origin));

        string faultKind = exception.GetType().Name;
        string publicMessage = GetPublicMessage(origin);
        return new PublicFaultInfo(faultKind, publicMessage, null, origin, DateTimeOffset.UtcNow);
    }

    private static string GetPublicMessage(string origin) {
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
