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
- Preserve the split between:
	- `ProcessGroup` public API
	- `IProcessGroupImpl` internal abstraction
	- `WindowsJobObject` Windows implementation
	- `UnixProcessGroup` Unix-like implementation
- Do not expose platform-specific implementation types publicly unless explicitly requested.
- Do not add dependency injection for this small library unless there is a concrete need.

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

## Build And Test

- Use `dotnet build ProcessGroup.slnx` to validate compilation.
- Use `dotnet test tests/ProcessGroup.Tests/ProcessGroup.Tests.csproj --no-build` to run tests after a successful build.
- Test execution must report NUnit discovery and a test summary, for example:
	- `NUnit3TestExecutor discovered ...`
	- `Test summary: total: ..., failed: 0, succeeded: ...`
- A successful test run must execute the discovered tests, not only complete MSBuild targets.
- Because project-to-project references use `Reference` instead of `ProjectReference`, build ordering must come from `ProcessGroup.slnx`.

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
- Do not revert user changes unless explicitly requested.
- Do not rewrite unrelated files.

## Command Conventions

- Commands and APIs should be idempotent where possible.
- Output should remain concise.
- Output should remain script-friendly.
- Breaking changes must be explicit.