# AGENTS.md

## Project

- This repository contains `ProcessGroup`, a reusable .NET library for cross-platform child process lifetime management.
- The public API lives in `src/ProcessGroup`.
- Tests live in `tests/ProcessGroup.Tests`.
- Keep the repository focused on the library; do not introduce CLI, UI, hosting, logging, or dependency injection infrastructure unless explicitly requested.

## Platform

- Target platforms: Windows, Linux, macOS, FreeBSD.
- Windows behavior is implemented with Windows Job Objects.
- Unix-like behavior is implemented with POSIX process groups.

## Runtime

- Use .NET 10.
- Target framework must remain `net10.0` unless explicitly changed.
- Use the repository-wide language settings from `Directory.Build.props`.

## Dependencies

- Do not introduce new NuGet packages without explicit approval.
- Use centralized package management.
- Manage package versions only in `Directory.Packages.props`.
- Do not put package versions on individual `PackageReference` items.

## Current Libraries

- Production project currently has no external NuGet dependencies.
- Test project uses:
	- Microsoft.NET.Test.Sdk
	- NUnit
	- NUnit3TestAdapter
- `Microsoft.NET.Test.Sdk` is required for test discovery and execution through `dotnet test`; do not remove it.

## Architecture

- Keep all functionality available as reusable library APIs.
- Keep platform-specific implementations internal to the library.
- The library namespace is `ProcessGroups` (plural); the public class is `ProcessGroup` (singular). Do not collapse them — the plural namespace exists to avoid a name/type collision and keep `using ProcessGroups; new ProcessGroup()` unambiguous.
- Preserve the split between:
	- `ProcessGroup` public API
	- `IProcessGroupImpl` internal abstraction
	- `WindowsJobObject` Windows implementation
	- `UnixProcessGroup` Unix-like implementation
- Do not expose platform-specific implementation types publicly unless explicitly requested.
- Do not add dependency injection for this small library unless there is a concrete need.

## Thread Safety

- `ProcessGroup` public methods (`Start`, `Add`, `TerminateAll`, `GetStats`, `Dispose`, `DisposeAsync`) are safe to call concurrently from multiple threads.
- `_disposed` is coordinated with `Interlocked.Exchange` / `Volatile.Read`; `Dispose` and `DisposeAsync` run their underlying teardown exactly once.
- `UnixProcessGroup` guards `_processes` and `_pgid` with a `System.Threading.Lock` and uses a snapshot pattern in `TerminateAll`, `GetStats`, `Dispose`, and `DisposeAsync` so blocking work (waiting for child exit) happens outside the lock.
- `WindowsJobObject` relies on the kernel's own synchronisation (`SafeFileHandle` + Win32 Job Object APIs) and holds no managed list.
- Do not remove this protection without a replacement; concurrent `Start` followed by `Dispose` from another thread must not corrupt state or leak a child process.

## Process Execution

- All processes started through `ProcessGroup.Start` must be attached to the active process group implementation immediately after start.
- On Windows, every spawned process managed by this library must be assigned to a Windows Job Object.
- On Linux, macOS, and FreeBSD, every spawned process managed by this library must be assigned to the POSIX process group used by the library.
- `Dispose` must terminate managed child processes.
- `TerminateAll` must terminate all processes currently managed by the group.
- Tests that start external processes must always clean them up, including failure paths.

## Project References

- Do not use `ProjectReference`.
- Cross-project references must use `Reference`.
- Do not use `HintPath`.
- Projects that reference outputs from other projects must define `AssemblySearchPaths`.
- `AssemblySearchPaths` must contain the output directories of referenced projects.
- Project references must resolve through assembly lookup paths only.

## Build Ordering

- Use the `.slnx` solution format.
- `.slnx` must define build dependencies between projects.
- Referencing projects must depend on referenced projects.
- Referenced projects must build before dependent projects.
- Build ordering must be explicit and deterministic.

## Repository Structure

- Use `ProcessGroup.slnx` as the solution file.
- Use `Directory.Build.props` for repository-wide MSBuild configuration.
- Use `Directory.Packages.props` for centralized package versions.
- Keep source code under `src/`.
- Keep tests under `tests/`.
- Keep helper scripts under `scripts/`.

## MSBuild Path Properties

- `Directory.Build.props` defines two canonical path properties available to every project in the repository:
	- `$(RepoRoot)` — absolute path to the repository root, with a trailing directory separator. Resolved from `$(MSBuildThisFileDirectory)` inside `Directory.Build.props`, which always equals the directory containing that file.
	- `$(ProcessGroupProjectDir)` — absolute path to `src/ProcessGroup/` (the library project directory).
- Use these properties instead of relative constructs (`..\..\`, `$(MSBuildThisFileDirectory)..\`, etc.) whenever a project file needs to reference a file or directory outside its own directory.
- Do not hardcode cross-project or cross-directory relative paths in `.csproj`, `.props`, or `.targets` files.
- If a new project is added that other projects must reference by path, add a corresponding `$(XxxProjectDir)` property to `Directory.Build.props`.

## Build And Test

- Use `dotnet build ProcessGroup.slnx` to validate compilation.
- Use `dotnet test tests/ProcessGroup.Tests/ProcessGroup.Tests.csproj --no-build` to run tests after a successful build.
- Test execution must report NUnit discovery and a test summary, for example:
	- `NUnit3TestExecutor discovered ...`
	- `Test summary: total: ..., failed: 0, succeeded: ...`
- A successful test run must execute the discovered tests, not only complete MSBuild targets.
- Because project-to-project references use `Reference` instead of `ProjectReference`, build ordering must come from `ProcessGroup.slnx`.

## Linux Testing (local, from Windows)

- `scripts/test-linux.ps1` runs the full test suite inside a Linux container using Rancher Desktop or Docker Desktop.
- Requires PowerShell 7+ and a running Docker daemon (`docker` on PATH).
- The script shadows `bin/` and `obj/` folders with anonymous Docker volumes so Windows IDE artifacts do not leak into the Linux build.
- A named volume (`processgroup-nuget`) caches NuGet packages between runs.
- Supports `-Filter`, `-Configuration`, and `-Rebuild` parameters.
- Do not modify the anonymous-volume list in the script without also verifying that the Linux build still resolves `ProcessGroup.dll` correctly (the test project uses `AssemblySearchPaths` pointing to the standard `src/ProcessGroup/bin/` location).

## Formatting

- Use tabs for indentation.
- Never use spaces for indentation.
- Apply tab-only indentation to:
	- `.cs`
	- `.csproj`
	- `.props`
	- `.targets`
	- `.slnx`
	- `.config`
	- `.md`
	- all other repository files
- Preserve LF line endings unless a file-specific rule says otherwise.
- Follow `.editorconfig`.

## C# Style

- Use file-scoped namespaces.
- Keep nullable annotations enabled.
- Keep implicit usings enabled.
- Treat warnings as errors.
- Prefer simple, direct code over new abstractions.
- Minimize public API surface area.
- Public API changes must be intentional and documented.

## Documentation

- All documentation must be written in English.
- All code comments must be written in English.
- Functional changes must include corresponding README updates when behavior, requirements, usage, or public API changes.
- README updates must reflect the current behavior of the module.
- Documentation changes must be completed after implementation and successful validation.
- Do not leave changed behavior undocumented.

## Changelog

- `CHANGELOG.md` is the single source of truth for release notes.
- The release workflow reads `## [Unreleased]` automatically to populate the GitHub Release body and the NuGet `<PackageReleaseNotes>` field.
- Add a manual bullet under `## [Unreleased]` in `CHANGELOG.md` whenever you want a curated, consumer-friendly wording. Use the appropriate subsection:
	- `### Added` — new features or API members
	- `### Changed` — modified behaviour or API
	- `### Fixed` — bug fixes
- Write the entry for a consumer of the library, not the implementer. Keep it to one line.
- Replace the placeholder `-` with a real bullet; do not leave placeholder lines alongside real entries.
- Do not modify versioned sections (`## [3.1.0]`, etc.) — those are managed by the release workflow.

### Auto-fill fallback

- If `## [Unreleased]` has no real bullets at release time, the workflow auto-generates entries from commits since the previous tag using `git-cliff` (config: `cliff.toml`). Manual entries always win over auto-fill.
- The first word of the commit subject decides the bucket (case-insensitive):
	- `Add`, `Feat` → `### Added`
	- `Fix`, `Bug` → `### Fixed`
	- `Remove`, `Delete`, `Drop` → `### Removed`
	- `Refactor`, `Update`, `Change`, `Rename`, `Perf`, `CI`, `Cleanup`, etc. → `### Changed`
	- `Doc`, `Chore`, `Test`, `Style` → skipped (excluded from notes)
	- `Release v...` and merge commits → skipped
	- anything unrecognised → `### Changed` (fallback)
- Write commit subjects with these prefixes when you want them to land in the right bucket without editing `CHANGELOG.md`.
- If the auto-fill produces no entries (e.g. only skipped commits since the previous tag), the release fails with a clear error — add a manual entry to unblock it.

## Comments

- Minimize comments.
- Write comments only when explaining:
	- why something exists
	- architectural decisions
	- non-obvious platform behavior
	- non-obvious process lifetime behavior
- Do not write comments describing what the code already says.

## Git Rules

- Agents must not create commits.
- Agents must not push changes.
- Do not revert user changes unless the user explicitly agrees to this.
- Do not rewrite unrelated files.

## Command Conventions

- Commands and APIs should be idempotent where possible.
- Output should remain concise.
- Output should remain script-friendly.
- Breaking changes must be explicit.