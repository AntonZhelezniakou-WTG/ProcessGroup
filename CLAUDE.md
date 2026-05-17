# CLAUDE.md

This file provides guidance to Claude Code (claude.ai/code) when working with code in this repository.

## Commands

```bash
# Build (warnings are errors)
dotnet build

# Run all tests
dotnet test tests/ProcessGroup.Tests/ProcessGroup.Tests.csproj

# Run a single test
dotnet test tests/ProcessGroup.Tests/ProcessGroup.Tests.csproj --filter "FullyQualifiedName~TestMethodName"

# Run tests inside a Linux container (requires Rancher Desktop or Docker Desktop, PowerShell 7+)
pwsh scripts/test-linux.ps1
pwsh scripts/test-linux.ps1 -Filter "FullyQualifiedName~TestMethodName"
```

## Architecture

The library namespace is `ProcessGroups` (plural); the public type is `ProcessGroup` (singular). The plural namespace exists deliberately so `using ProcessGroups; new ProcessGroup()` resolves without name/type ambiguity. Project name, `AssemblyName`, and `PackageId` remain `ProcessGroup` — those are the package identity, the namespace is the API surface.

`ProcessGroup` is a thin cross-platform façade over two platform-specific implementations behind `IProcessGroupImpl`:

- **`WindowsJobObject`** — wraps a Windows [Job Object](https://learn.microsoft.com/en-us/windows/win32/procthread/job-objects) with `JOB_OBJECT_LIMIT_KILL_ON_JOB_CLOSE`. All assigned processes are killed atomically when the job handle is closed (on `Dispose`). Native interop lives in `Kernel32.cs` (`[LibraryImport]`, `SafeFileHandle` for the job handle).

- **`UnixProcessGroup`** — creates a POSIX process group (`setpgid`) with the first process's PID as the group leader. On `Dispose`, sends `SIGTERM` to `-pgid` (the whole group), waits up to 2 seconds (monotonic, via `Stopwatch`), then falls back to `Process.Kill(entireProcessTree: true)` for any survivors. Native interop lives in `Libc.cs`.

Native P/Invoke declarations are isolated in `Kernel32.cs` / `Libc.cs` as `static partial` classes (one per DLL), using `[LibraryImport]` exclusively — no `[DllImport]`, no `CsWin32`, no `NativeMethods.txt`. The platform-specific impl classes pull them in via `using static`.

### Public API

```csharp
public sealed class ProcessGroup : IDisposable, IAsyncDisposable
{
    public Process Start(ProcessStartInfo startInfo, CancellationToken cancellationToken = default);
    public void Add(Process process);
    public void TerminateAll();
    public ProcessGroupStats GetStats();
    public void Dispose();
    public ValueTask DisposeAsync();
}

public readonly record struct ProcessGroupStats(
    int ActiveProcessCount,
    TimeSpan TotalCpuTime,
    long PeakMemoryBytes);
```

`GetStats()` is backed by `QueryInformationJobObject` on Windows (accounting + extended limit info). On Unix it iterates `_processes`, summing `TotalProcessorTime` and `PeakWorkingSet64` from live processes (exited and disposed are skipped). Semantics differ slightly: Windows reports the kernel-tracked peak of *total* job memory over time; Unix reports the sum of per-process peaks, which is an upper bound — actual concurrent peak may be lower if processes peaked at different times.

`CancellationToken` in `Start` kills the process on cancellation via `Process.Exited` cleanup — it does not prevent the process from starting.

### Key design constraints

- `setpgid` errors `ESRCH`, `EPERM`, `EACCES` are silently ignored — all three are race conditions with the child's `exec()` or natural exit, not real failures.
- On Unix, processes started by `StartAndAdd` are added to `_processes` **before** calling `setpgid`, so a setpgid failure never leaks a process we created. `Add` (for externally-started processes) intentionally appends only after `setpgid` succeeds — the contract is "if Add throws, we did not take ownership."
- On Windows, if `AssignProcessToJobObject` fails after `Process.Start`, the process is killed and disposed before re-throwing — same guarantee.
- The 2-second Unix shutdown timeout is a **shared deadline** across all processes, not per-process.
- `ProcessGroup` is thread-safe: `_disposed` uses `Interlocked.Exchange`/`Volatile.Read`; `UnixProcessGroup` guards `_processes`/`_pgid` with `System.Threading.Lock` and takes a snapshot before any blocking wait or `await` so I/O runs outside the lock. `WindowsJobObject` relies on the kernel's own synchronisation.
- `IsAotCompatible = true` — keep all P/Invoke via `[LibraryImport]`; no reflection-based interop.
- `TreatWarningsAsErrors = true` — the build is warning-clean; keep it that way.

### Test project setup

Tests reference the library via a direct `<Reference>` + `AssemblySearchPaths` (not `<ProjectReference>`). Run tests after a `dotnet build` or let the test runner build implicitly.

### Linux testing from Windows

`scripts/test-linux.ps1` mounts the repo into `mcr.microsoft.com/dotnet/sdk:10.0` and runs `dotnet build` + `dotnet test`. Anonymous Docker volumes shadow `src/ProcessGroup/bin`, `src/ProcessGroup/obj`, `tests/ProcessGroup.Tests/bin`, and `tests/ProcessGroup.Tests/obj` to keep the host working copy untouched. A named volume (`processgroup-nuget`) caches packages between runs. CI mirrors this with `.github/workflows/ci.yml`, which runs the same build/test across `ubuntu-latest`, `windows-latest`, and `macos-latest` on PR and push to main.

### MSBuild path properties

`Directory.Build.props` defines two canonical path properties that every project in the repo inherits:

- `$(RepoRoot)` — absolute path to the repository root (trailing separator included). Derived from `$(MSBuildThisFileDirectory)` inside `Directory.Build.props`, so it is always the directory that contains that file.
- `$(ProcessGroupProjectDir)` — absolute path to `src/ProcessGroup/`.

Use these properties wherever a `.csproj`, `.props`, or `.targets` file must reference something outside its own directory — never write `..\..\` or `$(MSBuildThisFileDirectory)..\` directly. If a new project is added that others reference by path, add a matching `$(XxxProjectDir)` property to `Directory.Build.props`.

## Changelog

`CHANGELOG.md` is the single source of truth for release notes. The release workflow reads the `## [Unreleased]` section automatically — it populates the GitHub Release body and the NuGet `<PackageReleaseNotes>` field.

**When to add an entry manually:** any user-visible change where you want a curated, consumer-friendly wording — new API, changed behaviour, bug fix, deprecation, removal.

**How to add an entry:**

1. Open `CHANGELOG.md`.
2. Under `## [Unreleased]`, find the appropriate subsection:
   - `### Added` — new features or API members
   - `### Changed` — modified behaviour or API
   - `### Fixed` — bug fixes
3. Replace the placeholder `-` with a real bullet (or append after existing bullets). Keep it one line, written for a consumer of the library — not for the implementer.

Example:

```markdown
### Fixed
- `TerminateAll` no longer throws `ObjectDisposedException` when called concurrently with `Dispose`.
```

Do **not** touch the versioned sections (`## [3.1.0]`, etc.) — the release workflow manages those.

### Auto-fill from git log

If `## [Unreleased]` has no real bullets when the release workflow runs, it auto-generates entries from commits since the previous tag via [git-cliff](https://git-cliff.org/) (config: `cliff.toml`). Manual entries always take priority — auto-fill is a fallback so a release never blocks on missing notes, not the default.

The auto-fill bucket is decided by the first word of the commit subject:

| Prefix (case-insensitive) | Bucket |
|---|---|
| `Add`, `Feat` | `### Added` |
| `Fix`, `Bug` | `### Fixed` |
| `Remove`, `Delete`, `Drop` | `### Removed` |
| `Refactor`, `Update`, `Change`, `Rename`, `Perf`, `CI`, `Cleanup`, ... | `### Changed` |
| `Doc`, `Chore`, `Test`, `Style` | skipped (not in notes) |
| `Release v...`, `Merge ...` | skipped |
| anything else | `### Changed` (fallback) |

Write commit subjects accordingly when you want them to appear in the right bucket without touching `CHANGELOG.md`. If you want one wording for the commit and another for the changelog, write the manual entry — it wins.
