# Changelog

All notable changes to ForkForward.Mediator. The format follows [Keep a Changelog](https://keepachangelog.com/en/1.1.0/); versions follow [SemVer](https://semver.org/) and continue MediatR's 12.x line, which is the code this fork starts from.

## [Unreleased]

ForkForward.Mediator starts from MediatR 12.5.0 (commit `8a64581e`, 2025-03-24), the last release under the Apache License 2.0. There are no changes to the library's code or behaviour.

### Changed

- The package is `ForkForward.Mediator`, with its own description, logo, readme and project address. The assembly is still `MediatR.dll` and the namespace is still `MediatR`.
- The contracts are published by the fork as `ForkForward.Mediator.Contracts` 2.6.0: the code and the public types of the original `MediatR.Contracts` 2.0.1, in an assembly that is still called `MediatR.Contracts.dll`, now with assembly version 2.6.0.0. `ForkForward.Mediator` depends on exactly this version, `[2.6.0]`, and no longer on the original `MediatR.Contracts` package. A project that references `MediatR.Contracts` itself replaces that reference with `ForkForward.Mediator.Contracts`.
- Both packages carry `LICENSE` and `NOTICE.md`.
- Versions are computed from `v*.*.*` tags and start above the original: untagged commits build `12.6.0-preview.0.<n>`.

### Removed

- The scripts and workflow steps that pushed packages to the original project's feeds.

[Unreleased]: https://github.com/ForkForward/dotnet-mediator/compare/v12.5.0...HEAD
