# Changelog

All notable changes to **ProcessGroup** are documented in this file.

The format is based on [Keep a Changelog](https://keepachangelog.com/en/1.1.0/),
and this project adheres to [Semantic Versioning](https://semver.org/spec/v2.0.0.html).

## [Unreleased]

### Added
-

### Changed
-

### Fixed
-

## [3.1.3] - 2026-05-17

### Changed

- Document environment-based NuGet credentials

## [3.1.2] - 2026-05-17

### Changed

- Document GitHub Packages auth for package consumers

## [3.1.1] - 2026-05-17

### Added

- Add changelog auto-fill fallback for releases


### Changed

- Generate release notes from the changelog
- Centralize MSBuild path handling for repo-wide references

## [3.1.0] - 2026-05-17

### Changed
- CI now runs across `ubuntu-latest`, `windows-latest`, and `macos-latest`,
  validating both Windows job objects and Unix process groups on every push and PR.

## [3.0.0] - 2026-05-16

### Changed
- Renamed namespace to `ProcessGroups` (plural) so `using ProcessGroups; new ProcessGroup()`
  resolves without name/type ambiguity. Public type remains `ProcessGroup`.
- `ProcessGroup` is now thread-safe — `_disposed` uses `Interlocked.Exchange`/`Volatile.Read`;
  `UnixProcessGroup` guards state with `System.Threading.Lock` and takes a snapshot
  before any blocking wait so I/O runs outside the lock.

### Added
- Linux container test runner (`scripts/test-linux.ps1`) and CI workflow on Ubuntu.

## [2.0.0] - 2026-05-15

### Added
- `TerminateAll()` — sends `SIGTERM` to `-pgid` on Unix and calls `TerminateJobObject`
  on Windows.
- `DisposeAsync()` — async disposal honouring the 2-second Unix shutdown deadline.
- `GetStats()` returning `ProcessGroupStats(ActiveProcessCount, TotalCpuTime, PeakMemoryBytes)`.
- Manual release workflow (bump version, build, test, pack, publish to GitHub Packages).

### Fixed
- Malformed package copyright metadata.

[Unreleased]: https://github.com/AntonZhelezniakou-WTG/ProcessGroup/compare/v3.1.3...HEAD
[3.1.3]: https://github.com/AntonZhelezniakou-WTG/ProcessGroup/compare/v3.1.2...v3.1.3
[3.1.2]: https://github.com/AntonZhelezniakou-WTG/ProcessGroup/compare/v3.1.1...v3.1.2
[3.1.1]: https://github.com/AntonZhelezniakou-WTG/ProcessGroup/compare/v3.1.0...v3.1.1
[3.1.0]: https://github.com/AntonZhelezniakou-WTG/ProcessGroup/compare/v3.0.0...v3.1.0
[3.0.0]: https://github.com/AntonZhelezniakou-WTG/ProcessGroup/compare/v2.0.0...v3.0.0
[2.0.0]: https://github.com/AntonZhelezniakou-WTG/ProcessGroup/releases/tag/v2.0.0
