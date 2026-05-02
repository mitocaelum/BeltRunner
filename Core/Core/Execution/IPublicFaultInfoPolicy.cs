using System;

namespace BeltRunner.Core.Execution;

/// <summary>
/// Creates sanitized fault summaries for public runtime surfaces.
/// </summary>
public interface IPublicFaultInfoPolicy {
    /// <summary>
    /// Creates a public fault summary for the specified exception and safe origin identifier.
    /// </summary>
    /// <param name="exception">The exception that caused the fault.</param>
    /// <param name="origin">A safe origin identifier such as <c>run</c>, <c>host</c>, or <c>phase:Build</c>.</param>
    /// <returns>A sanitized fault summary that is safe to expose publicly.</returns>
    PublicFaultInfo Create(Exception exception, string origin);
}
