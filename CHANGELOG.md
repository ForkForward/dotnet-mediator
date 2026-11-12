# Changelog

All notable changes to ForkForward.Mediator. The format follows [Keep a Changelog](https://keepachangelog.com/en/1.1.0/); versions follow [SemVer](https://semver.org/) and continue MediatR's 12.x line, which is the code this fork starts from.

## [Unreleased]

ForkForward.Mediator starts from MediatR 12.5.0 (commit `8a64581e`, 2025-03-24), the last release under the Apache License 2.0. Nothing has been published yet. There are no changes to the library's code or behaviour.

### Changed

- The package is `ForkForward.Mediator`, with its own description, logo, readme and project address. The assembly is still `MediatR.dll` and the namespace is still `MediatR`.
- The package carries `LICENSE` and `NOTICE.md`.
- Versions are computed from `v*.*.*` tags and start above the original: untagged commits build `12.6.0-preview.0.<n>`.

### Removed

- The `MediatR.Contracts` project is no longer packed: that package name belongs to the original authors. The library still depends on their `MediatR.Contracts` 2.0.1 (Apache License 2.0).
- The scripts and workflow steps that pushed packages to the original project's feeds.

[Unreleased]: https://github.com/ForkForward/dotnet-mediator/compare/v12.5.0...HEAD
