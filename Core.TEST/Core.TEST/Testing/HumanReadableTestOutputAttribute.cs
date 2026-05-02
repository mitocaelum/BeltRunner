using NUnit.Framework.Interfaces;

namespace BeltRunner.Core.TEST.Testing;

/// <summary>
/// Emits human-readable output for each executed test.
/// </summary>
[AttributeUsage(AttributeTargets.Assembly | AttributeTargets.Class | AttributeTargets.Method, AllowMultiple = false, Inherited = true)]
public sealed class HumanReadableTestOutputAttribute : TestActionAttribute {
    /// <inheritdoc />
    public override ActionTargets Targets => ActionTargets.Test;

    /// <inheritdoc />
    public override void BeforeTest(ITest test) {
        if( test.IsSuite ) {
            return;
        }

        HumanReadableTestLog.Begin(test.Id);

        (string? purpose, string? whyThisMatters, string? expected) = HumanReadableTestDocumentation.GetFor(test);

        TestContext.Progress.WriteLine($"[TEST] {test.FullName}");

        if( !string.IsNullOrWhiteSpace(purpose) ) {
            TestContext.Progress.WriteLine($"  Purpose: {purpose}");
        }

        if( !string.IsNullOrWhiteSpace(whyThisMatters) ) {
            TestContext.Progress.WriteLine($"  Why this matters: {whyThisMatters}");
        }

        if( !string.IsNullOrWhiteSpace(expected) ) {
            TestContext.Progress.WriteLine($"  Expected: {expected}");
        }
    }

    /// <inheritdoc />
    public override void AfterTest(ITest test) {
        if( test.IsSuite ) {
            return;
        }

        HumanReadableTestCompletion completion = HumanReadableTestLog.Complete(test.Id);
        TestStatus status = TestContext.CurrentContext.Result.Outcome.Status;
        string? message = TestContext.CurrentContext.Result.Message;

        if( completion.Observed.Count == 0 ) {
            TestContext.Progress.WriteLine("  Observed: No observed values were recorded.");
        } else {
            foreach( string observed in completion.Observed ) {
                TestContext.Progress.WriteLine($"  Observed: {observed}");
            }
        }

        TestContext.Progress.WriteLine($"  Result: {status} in {completion.Duration.TotalMilliseconds:0.###} ms");

        if( !string.IsNullOrWhiteSpace(message) ) {
            TestContext.Progress.WriteLine($"  Detail: {message}");
        }

        TestContext.Progress.WriteLine(string.Empty);
    }
}
