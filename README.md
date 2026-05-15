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

1. Add the GitHub Packages source to your `nuget.config` (create one in the project root if it doesn't exist):

```xml
<?xml version="1.0" encoding="utf-8"?>
<configuration>
  <packageSources>
    <add key="github" value="https://nuget.pkg.github.com/AntonZhelezniakou-WTG/index.json" />
  </packageSources>
  <packageSourceCredentials>
    <github>
      <add key="Username" value="YOUR_GITHUB_USERNAME" />
      <add key="ClearTextPassword" value="YOUR_GITHUB_PAT" />
    </github>
  </packageSourceCredentials>
</configuration>
```

The PAT must have at least the `read:packages` scope.

2. Add the package:

```sh
dotnet add package ProcessGroup
```

## Usage

```csharp
using System.Diagnostics;
using ProcessGroup;

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

## License

Proprietary and Confidential — Copyright (c) 2026 Anton Zhelezniakou, WiseTech Global. All rights reserved.

Unauthorized copying, redistribution, or modification is strictly prohibited. See [LICENSE](LICENSE).
