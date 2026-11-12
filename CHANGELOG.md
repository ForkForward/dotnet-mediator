# Changelog

All notable changes to ForkForward.Mediator. The format follows [Keep a Changelog](https://keepachangelog.com/en/1.1.0/); versions follow [SemVer](https://semver.org/) and continue MediatR's 12.x line, which is the code this fork starts from.

## [Unreleased]

## [12.7.0] - 2026-11-12

The first stable release of ForkForward.Mediator. It is MediatR 12.5.0 (commit `8a64581e`, 2025-03-24), the last release under the Apache License 2.0, with its own identity and the build and release infrastructure of this fork. There are no changes to what the library does.

There is no 12.6.0. The only version in that line is `12.6.0-preview.1`, which still used the original assembly names and namespace, as did `ForkForward.Mediator.Contracts` 2.6.0 that was published with it. Do not use them.

### Changed

- **Own assembly names, namespace and signing key.** The assemblies are `ForkForward.Mediator.dll` and `ForkForward.Mediator.Contracts.dll`, the namespaces start with `ForkForward.Mediator` (`ForkForward.Mediator.Pipeline`, `ForkForward.Mediator.NotificationPublishers`, and so on), and both are signed with the fork's own key (public key token `fe080643cbba1f62`). The original key is no longer in the repository.
- **No name of the original project in the public API.** `AddMediatR` is `AddMediator`, `MediatRServiceConfiguration` is `MediatorServiceConfiguration`, and `ServiceRegistrar.AddMediatRClasses` and `AddMediatRClassesWithTimeout` are `AddMediatorClasses` and `AddMediatorClassesWithTimeout`. There are no `[Obsolete]` copies of the old names. All other types, their members and the behaviour are those of MediatR 12.5.0. Moving a project is a package reference, a `using` directive and those names: [Moving from MediatR 12](docs/Moving-from-MediatR-12.md).
- The packages are `ForkForward.Mediator` and `ForkForward.Mediator.Contracts`, each with its own description, logo, readme and project address.
- The contracts are published by the fork as `ForkForward.Mediator.Contracts`: the code and the public types of the original `MediatR.Contracts` 2.0.1. Both packages have one version, taken from the release tag, and `ForkForward.Mediator` depends on exactly that version of the contracts and no longer on the original `MediatR.Contracts` package. `ForkForward.Mediator.Contracts` 2.6.0, published with the first preview, had a version of its own and the original assembly name; it is superseded.
- Both packages carry `LICENSE` and `NOTICE.md`.
- Versions are computed from `v*.*.*` tags.

### Removed

- The scripts and workflow steps that pushed packages to the original project's feeds.

[Unreleased]: https://github.com/ForkForward/dotnet-mediator/compare/v12.7.0...HEAD
[12.7.0]: https://github.com/ForkForward/dotnet-mediator/compare/v12.5.0...v12.7.0
