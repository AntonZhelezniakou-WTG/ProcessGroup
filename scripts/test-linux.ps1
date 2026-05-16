#!/usr/bin/env pwsh
<#
.SYNOPSIS
    Runs ProcessGroup tests inside a Linux container.

.DESCRIPTION
    Cross-platform wrapper around `docker run` that builds and tests the
    solution against a Linux .NET SDK image. Intended for developers on
    Windows using Rancher Desktop (or Docker Desktop) to exercise the
    Unix code path (UnixProcessGroup / Libc) without leaving their host.

    The host's bin/obj folders (populated by the Windows IDE build) are
    shadowed inside the container with anonymous volumes — Linux build
    artifacts live in those throwaway volumes and never touch the host
    working copy. A named volume caches NuGet packages between runs.

.PARAMETER Image
    Container image. Defaults to mcr.microsoft.com/dotnet/sdk:10.0.

.PARAMETER Configuration
    MSBuild configuration. Debug or Release. Defaults to Release.

.PARAMETER Filter
    Optional `dotnet test --filter` expression
    (e.g. "FullyQualifiedName~Dispose_TerminatesStartedProcess").

.PARAMETER Rebuild
    Run `dotnet clean` before the tests.

.EXAMPLE
    pwsh ./scripts/test-linux.ps1

.EXAMPLE
    pwsh ./scripts/test-linux.ps1 -Filter "FullyQualifiedName~TerminateAll"
#>
[CmdletBinding()]
param(
    [string]$Image = 'mcr.microsoft.com/dotnet/sdk:10.0',
    [ValidateSet('Debug', 'Release')]
    [string]$Configuration = 'Release',
    [string]$Filter,
    [switch]$Rebuild
)

$ErrorActionPreference = 'Stop'

$RepoRoot = (Resolve-Path (Join-Path $PSScriptRoot '..')).Path
$NugetVolume = 'processgroup-nuget'

if (-not (Get-Command docker -ErrorAction SilentlyContinue)) {
    Write-Host "docker CLI not found on PATH." -ForegroundColor Red
    Write-Host "Start Rancher Desktop (with the dockerd/moby engine) or install Docker Desktop, then re-open the shell." -ForegroundColor Yellow
    exit 1
}

& docker version --format '{{.Server.Version}}' *> $null
if ($LASTEXITCODE -ne 0) {
    Write-Host "Cannot reach the Docker daemon. Is Rancher Desktop running?" -ForegroundColor Red
    exit 1
}

$bashLines = @('set -e')
if ($Rebuild) {
    $bashLines += "dotnet clean -c $Configuration"
}
$bashLines += "dotnet build -c $Configuration"
$testCmd = "dotnet test --no-build -c $Configuration tests/ProcessGroup.Tests/ProcessGroup.Tests.csproj"
if ($Filter) {
    $testCmd += " --filter `"$Filter`""
}
$bashLines += $testCmd
$bashScript = $bashLines -join "`n"

# Anonymous volumes shadow the host bin/obj folders inside the container so
# Windows IDE artifacts cannot leak into the Linux build, and the Linux build
# does not write back into the host tree. The library DLL still lands at the
# standard `src/ProcessGroup/bin/...` path the test project expects via
# AssemblySearchPaths — just inside the anonymous volume.
$shadowedPaths = @(
    '/src/src/ProcessGroup/bin',
    '/src/src/ProcessGroup/obj',
    '/src/tests/ProcessGroup.Tests/bin',
    '/src/tests/ProcessGroup.Tests/obj'
)

$dockerArgs = @(
    'run', '--rm',
    '-v', "${RepoRoot}:/src",
    '-v', "${NugetVolume}:/root/.nuget/packages"
)
foreach ($p in $shadowedPaths) {
    $dockerArgs += @('-v', $p)
}
$dockerArgs += @(
    '-w', '/src',
    '-e', 'DOTNET_CLI_TELEMETRY_OPTOUT=1',
    '-e', 'DOTNET_NOLOGO=1',
    $Image,
    'bash', '-c', $bashScript
)

Write-Host "==> docker $($dockerArgs -join ' ')" -ForegroundColor DarkGray
& docker @dockerArgs
exit $LASTEXITCODE
