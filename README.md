# ProcessGroup

Cross-platform child process lifetime management for .NET.

ProcessGroup ensures that child processes are terminated
when the parent exits — whether gracefully or via crash.

On Windows this is backed by kernel Job Objects;
on Unix systems (Linux, macOS, FreeBSD) it uses POSIX process groups.

## Requirements

- .NET 10.0 or later
- Windows 8+ / Linux / macOS / FreeBSD
- AOT-compatible

## Installation

The package is hosted on [GitHub Packages](https://github.com/AntonZhelezniakou-WTG/ProcessGroup/pkgs/nuget/ProcessGroup).

GitHub Packages always requires authentication, even for public packages.
You will need a GitHub Personal Access Token (PAT) with the `read:packages` scope.

### 1. Create a PAT

Go to **GitHub → Settings → Developer settings → Personal access tokens → Tokens (classic)**
and create a token with only the **`read:packages`** scope checked — nothing else is needed.

Alternatively, use a fine-grained token (**Tokens (beta)**): set Repository access to
*Public repositories (read-only)* and enable **Packages: Read** under Permissions.

### 2. Register the package source

**Option A — `nuget.config` in the project root** (checked into the repo; keep the token out of it):

```xml
<?xml version="1.0" encoding="utf-8"?>
<configuration>
  <packageSources>
    <add key="github-processgroup" value="https://nuget.pkg.github.com/AntonZhelezniakou-WTG/index.json" />
  </packageSources>
  <packageSourceCredentials>
    <github-processgroup>
      <add key="Username" value="YOUR_GITHUB_USERNAME" />
      <add key="ClearTextPassword" value="YOUR_PAT" />
    </github-processgroup>
  </packageSourceCredentials>
</configuration>
```

Do not commit the token. Use environment variable substitution or store credentials
separately (see Option B).

**Option B — global credentials via CLI** (stored in your user-level `NuGet.Config`):

```sh
dotnet nuget add source https://nuget.pkg.github.com/AntonZhelezniakou-WTG/index.json \
  --name github-processgroup \
  --username YOUR_GITHUB_USERNAME \
  --password YOUR_PAT \
  --store-password-in-clear-text
```

This writes credentials once to `~/.nuget/NuGet/NuGet.Config` and works for all projects
on the machine without touching any project file.

### 3. Add the package

```sh
dotnet add package ProcessGroup
```

### CI / CD

GitHub Packages does not support truly anonymous access, so pipelines also need a token.
The standard approach is to create a dedicated **machine GitHub account**, generate a
classic PAT for it with only the `read:packages` scope, and store it as a repository
secret (e.g. `NUGET_READ_PAT`). Then in your workflow:

```yaml
- name: Add GitHub Packages source
  run: |
    dotnet nuget add source https://nuget.pkg.github.com/AntonZhelezniakou-WTG/index.json \
      --name github-processgroup \
      --username machine-account \
      --password ${{ secrets.NUGET_READ_PAT }} \
      --store-password-in-clear-text
```

The machine account needs no repository access — the `read:packages` scope alone is
sufficient for public packages.

## Usage

```csharp
using System.Diagnostics;
using ProcessGroups;

// Children are terminated when the group is disposed —
// even if the parent process crashes.
using var group = new ProcessGroup();

var psi = new ProcessStartInfo("myworker", ["--arg"]) { UseShellExecute = false };
var worker = group.Start(psi);

// Kill on cancellation
using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(30));
var transient = group.Start(psi, cts.Token);

// Add an externally-started process to the group
var external = Process.Start(psi);
group.Add(external!);

// Runtime statistics (CPU time, peak memory, active count)
var stats = group.GetStats();
Console.WriteLine($"active={stats.ActiveProcessCount} cpu={stats.TotalCpuTime} peak={stats.PeakMemoryBytes}");

// Async dispose — non-blocking on Unix where SIGTERM/wait is involved
await using var asyncGroup = new ProcessGroup();
// ...
```

## Running tests on Linux from Windows

The Unix code path (`UnixProcessGroup`, `Libc`) is exercised in CI on
`ubuntu-latest`, but you can also run the suite locally against a Linux
container — useful when changing native interop or shutdown semantics.

Requirements:

- [Rancher Desktop](https://rancherdesktop.io/) (or Docker Desktop) with the
  `dockerd` / moby engine enabled so `docker` is on `PATH`
- PowerShell 7+

```pwsh
pwsh ./scripts/test-linux.ps1
```

The script mounts the repo into `mcr.microsoft.com/dotnet/sdk:10.0` and runs
`dotnet build` + `dotnet test`. The host's `bin/` and `obj/` folders are
shadowed inside the container with anonymous volumes, so the Linux build
neither sees the Windows IDE artifacts nor writes back into the host tree.
A named volume (`processgroup-nuget`) caches NuGet packages between runs.

Useful switches:

```pwsh
pwsh ./scripts/test-linux.ps1 -Filter "FullyQualifiedName~TerminateAll"
pwsh ./scripts/test-linux.ps1 -Configuration Debug -Rebuild
```

## Changelog

See [CHANGELOG.md](CHANGELOG.md) for the version history.

## License

Proprietary and Confidential — Copyright (c) 2026 Anton Zhelezniakou, WiseTech Global. All rights reserved.

Unauthorized copying, redistribution, or modification is strictly prohibited. See [LICENSE](LICENSE).
