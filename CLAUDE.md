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

`GetStats()` is backed by `QueryInformationJobObject` on Windows (accounting + extended limit info) and by iterating `_processes` on Unix (active count only; CPU and memory are zero).

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

`scripts/test-linux.ps1` mounts the repo into `mcr.microsoft.com/dotnet/sdk:10.0` and runs `dotnet build` + `dotnet test`. Anonymous Docker volumes shadow `src/ProcessGroup/bin`, `src/ProcessGroup/obj`, `tests/ProcessGroup.Tests/bin`, and `tests/ProcessGroup.Tests/obj` to keep the host working copy untouched. A named volume (`processgroup-nuget`) caches packages between runs. CI mirrors this with `.github/workflows/ci.yml` (ubuntu-latest, triggers on PR and push to main).
