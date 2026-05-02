# AGENTS: Core.TEST

## Purpose

This file documents local rules for creating and maintaining tests in `Core.TEST`.

## XML Documentation For Tests

- Write XML documentation comments for every test fixture and every test method.
- Keep comments in American English.
- Use `/// <summary>` for the direct behavior being verified.
- Use `/// <remarks>` to describe the test intent in a stable format.
- In `remarks`, include these three `para` blocks in this order:
  - `Purpose: ...`
  - `Why this matters: ...`
  - `Expected result: ...`
- Write expected behavior concretely enough that a human can compare the console output with the intended contract.
- When a fixture protects a broader contract, document that at the fixture level as well.
- Continue to use `NUnit 4` and add `[TestOf(...)]` to the fixture for the public types or members under test.

Example:

```xml
/// <summary>
/// Verifies that snapshot coalescing suppresses publication growth during a dense telemetry burst.
/// </summary>
/// <remarks>
/// <para>Purpose: Protect the coalescing option from degrading into near one-publication-per-update behavior.</para>
/// <para>Why this matters: Telemetry-heavy phases can otherwise overwhelm observers with snapshot churn and unnecessary allocations.</para>
/// <para>Expected result: A coalesced run completes with the same final state as an uncoalesced run while emitting dramatically fewer snapshots.</para>
/// </remarks>
```

## Console Output Policy

- Test output must help a human understand "what the test is trying to prove" and "what happened", not only whether the test passed or failed.
- Prefer structured per-test output with these concepts:
  - `Purpose`
  - `Expected`
  - `Observed`
  - `Result`
- Prefer `TestContext.Progress` for test-facing runtime output instead of ad-hoc `Console.WriteLine`.
- Keep output readable and compact. Do not flood the console with raw object dumps unless the raw value is itself the assertion target.
- When a test fails, the output should make the mismatch obvious enough that a reader can understand the failure without opening the source immediately.
- When observed values are important, print the final human-readable observation explicitly instead of relying only on NUnit assertion messages.
- If a shared helper or attribute is introduced for output, new tests should follow that shared mechanism instead of inventing per-file formatting.

## Performance Tests

- This project includes performance-oriented regression tests.
- Existing performance coverage is represented by tests such as `HostPerformanceRegressionTests` and uses `[Category("Performance")]`.
- Performance tests are intended to protect against avoidable regressions in publication volume, retention behavior, and other in-memory costs.
- Keep performance assertions stable and regression-focused. Do not turn them into machine-specific microbenchmarks.
- Prefer relative or bounded expectations over fragile wall-clock thresholds when possible.
- Document the risk being guarded, the scaling condition, and the expected bound in the XML comments.
- If a new test is primarily about throughput, retention growth, burst handling, or coalescing behavior, consider whether it belongs in the performance category.
