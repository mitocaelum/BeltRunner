# AGENTS: Instructions for AI (or even for you, humans)

## C# and Documentation Rules

- Target .NET Standard 2.0 and use C# 12 for this project.
- Comments in the code:
  - Write code comments in American English.
  - Use Microsoft Style Guide (https://learn.microsoft.com/en-us/style-guide/welcome/) for writing
  - Add XML documentation comments to all public elements.
  - Write public XML documentation with DocFX publication in mind.
  - `//` comments for private or internal elements are allowed when useful, but they are optional.
- Coding Style:
  - When a public property uses a backing field, place that backing field immediately below the property.
  - Declare `const` fields near the beginning of the type.
  - Place opening braces on the same line as declarations and statements. Use `method() {`, `if (...) {`, and similar forms instead of moving `{` to the next line.
  - When an `if` body contains only a single statement, braces may be omitted unless braces improve clarity or consistency.
  - Include an `else` block when the branching represents a meaningful business or processing decision, even if the `else` branch intentionally does nothing.
    - However, do not add an `else` block for simple guard clauses such as null checks, empty string checks, or other early-exit validations.
  - When an `else` block is intentionally empty, add a short `//` comment that explains why no action is needed.
  - Create a single file for single class, enum, interface, and so on. For most cases, do not include more than one type per file.
  - Use UPPER_SNAKE_CASE for const strings. 

## Version Number Management

- Use MinVer for package version generation.
- Treat Git tags as the single source of truth for released versions.
- Use the tag format `v<SemVer>`, for example `v0.1.0-alpha.1`, `v0.1.0-beta.1`, `v0.1.0-rc.1`, and `v1.0.0`.
- Use the pre-release stages `alpha`, `beta`, and `rc` in that order.
- Keep the Core package version and the Analysis package version identical because the Analysis NuGet package depends on the Core NuGet package.
- Let MinVer manage `AssemblyVersion` automatically unless there is an intentional versioning-policy change.
- Use CI build numbers for `FileVersion`.
- Use commit-aware informational versions for build traceability.
- Package validation is wired for CI, but it is intentionally dormant until the repository variable `BELTRUNNER_PACKAGE_VALIDATION_BASELINE_VERSION` is set.
- When that variable is set, CI and release pack operations validate the current package against that baseline version.
- The default validation target is the NuGet test feed at `https://apiint.nugettest.org/v3/index.json`.
- Switching from the test feed to the public NuGet feed requires an intentional workflow edit.
- When switching to the public feed, update both the source URL and the GitHub Actions secret name together.

## Codex Work Notes

- If `apply_patch` reports a verification failure even after confirming the target text is present, do not keep retrying the same approach. Verify the file once, then switch to a shell-based edit path.
- If a required file is outside the writable roots, request an escalated command before attempting repeated edits. Do not spend time retrying write operations that the sandbox is likely to block.
- On Windows PowerShell, avoid one giant inline edit command for many files. If a command becomes large enough to risk `The filename or extension is too long`, split the work into smaller per-file commands or a short script file.
