using BeltRunner.Core.Execution.Interaction;
using BeltRunner.Core.Phase;
using BeltRunner.Core.TEST.Testing;

namespace BeltRunner.Core.TEST.Execution;

/// <summary>
/// Verifies that public execution input models reject unsafe identifier values and sanitize free-form text.
/// </summary>
[TestFixture]
[TestOf(typeof(PhaseKey))]
[TestOf(typeof(InteractionRequest<bool>))]
[TestOf(typeof(InteractionSnapshot))]
public sealed class InputValidationTests {
    /// <summary>
    /// Verifies that phase keys reject embedded control characters.
    /// </summary>
    /// <remarks>
    /// <para>Purpose: Protect identifier validation for public phase keys.</para>
    /// <para>Why this matters: Unsafe control characters in identifiers can break logs, diagnostics, and downstream routing.</para>
    /// <para>Expected result: Creating a phase key with embedded control characters throws <see cref="ArgumentException"/> for the <c>value</c> parameter.</para>
    /// </remarks>
    [Test]
    public void PhaseKey_WithControlCharacters_Throws() {
        ArgumentException ex = Assert.Throws<ArgumentException>(() => _ = new PhaseKey("phase\r\nunsafe"))!;
        TestNarrative.Observe($"constructor rejected control characters with paramName={ex.ParamName}");

        Assert.That(ex.ParamName, Is.EqualTo("value"));
    }

    /// <summary>
    /// Verifies that interaction kinds reject embedded control characters.
    /// </summary>
    /// <remarks>
    /// <para>Purpose: Protect interaction routing identifiers from unsafe content.</para>
    /// <para>Why this matters: Interaction kind values participate in transport and UI decisions, so malformed identifiers should fail fast.</para>
    /// <para>Expected result: Creating an interaction request with control characters in <c>kind</c> throws <see cref="ArgumentException"/> for the <c>kind</c> parameter.</para>
    /// </remarks>
    [Test]
    public void InteractionRequest_WithControlCharactersInKind_Throws() {
        PhaseKey phaseKey = new("phase/a");
        ArgumentException ex = Assert.Throws<ArgumentException>(() => _ = new InteractionRequest<bool>("confirm\r\nunsafe", phaseKey))!;
        TestNarrative.Observe($"request kind validation rejected the value with paramName={ex.ParamName}");

        Assert.That(ex.ParamName, Is.EqualTo("kind"));
    }

    /// <summary>
    /// Verifies that interaction snapshots reject embedded control characters in their routing kind.
    /// </summary>
    /// <remarks>
    /// <para>Purpose: Keep published interaction snapshot metadata safe for replay and display.</para>
    /// <para>Why this matters: Snapshot consumers should not receive routing kinds that contain control characters.</para>
    /// <para>Expected result: Creating an interaction snapshot with control characters in <c>kind</c> throws <see cref="ArgumentException"/> for the <c>kind</c> parameter.</para>
    /// </remarks>
    [Test]
    public void InteractionSnapshot_WithControlCharactersInKind_Throws() {
        PhaseKey phaseKey = new("phase/a");
        ArgumentException ex = Assert.Throws<ArgumentException>(() => _ = new InteractionSnapshot(Guid.NewGuid(), "confirm\tunsafe", "Approve", "Continue?", phaseKey, typeof(bool), DateTimeOffset.UtcNow))!;
        TestNarrative.Observe($"snapshot kind validation rejected the value with paramName={ex.ParamName}");

        Assert.That(ex.ParamName, Is.EqualTo("kind"));
    }

    /// <summary>
    /// Verifies that free-form interaction title and message text are sanitized before publication.
    /// </summary>
    /// <remarks>
    /// <para>Purpose: Protect free-form interaction text from carrying unsafe control characters into public surfaces.</para>
    /// <para>Why this matters: Titles and messages are displayed directly to users and tools, so they should remain readable and safe.</para>
    /// <para>Expected result: The request stores sanitized text without carriage returns, newlines, tabs, or escape characters.</para>
    /// </remarks>
    [Test]
    public void InteractionRequest_WithControlCharactersInTitleAndMessage_SanitizesText() {
        PhaseKey phaseKey = new("phase/a");
        InteractionRequest<bool> request = new("confirm", phaseKey, "Approve\r\nnow", "Continue\twith\u001bcare?");
        TestNarrative.ObserveMany(
            $"sanitized title={request.Title}",
            $"sanitized message={request.Message}");

        Assert.Multiple(() => {
            Assert.That(request.Title, Is.EqualTo("Approve  now"));
            Assert.That(request.Message, Is.EqualTo("Continue with care?"));
            Assert.That(request.Title, Does.Not.Contain("\r"));
            Assert.That(request.Title, Does.Not.Contain("\n"));
            Assert.That(request.Message, Does.Not.Contain("\t"));
            Assert.That(request.Message!.IndexOf('\u001b'), Is.EqualTo(-1));
        });
    }
}
