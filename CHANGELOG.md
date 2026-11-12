# Changelog

All notable changes to ForkForward.Mediator. The format follows [Keep a Changelog](https://keepachangelog.com/en/1.1.0/); versions follow [SemVer](https://semver.org/) and continue MediatR's 12.x line, which is the code this fork starts from.

## [Unreleased]

This is line 13: .NET 10 and .NET 11 only. Applications on .NET 8, .NET 9, .NET Framework or another .NET Standard 2.0 platform stay on 12.8.

### Breaking

- **No .NET Standard 2.0 asset.** Both packages target `net10.0` and `net11.0` and nothing else. .NET 8 and .NET 9 went out of support on 2026-11-10.
- `Microsoft.Extensions.DependencyInjection.Abstractions` 10.0.0 instead of 8.0.0 on .NET 10; on .NET 11 it is part of the framework, as before. The packages `IsExternalInit` and `Microsoft.Bcl.AsyncInterfaces` are no longer needed.
- An application that is trimmed or published with Native AOT now gets the warnings IL2026 and IL3050 where it calls `RegisterServicesFromAssembly`, `RegisterServicesFromAssemblyContaining` or `RegisterServicesFromAssemblies`, and IL2026 for `AddOpenBehaviors` with a list of types. Scanning never worked there; the warnings say so at compile time. Applications that are not trimmed see nothing.

### Added

- **Native AOT and trimming.** The library is marked `IsAotCompatible`. Handlers can be registered one by one, without reflection: `AddRequestHandler`, `AddNotificationHandler`, `AddStreamRequestHandler`, `AddRequestExceptionHandler` and `AddRequestExceptionAction` on `MediatorServiceConfiguration`. With them `AddMediator` needs no assembly. What works under Native AOT and what is different is on [Native AOT and trimming](docs/source/Native-AOT.md); a test application is published with Native AOT and run on every change.
- **Runtime async on .NET 11.** The `net11.0` assets are compiled with runtime async: the .NET 11 runtime runs the library's async methods itself, without compiler-generated state machines. The `net10.0` assets are compiled as before. There is one source for both and no conditional compilation.
- `Send` for a request without a response and `Publish` skip the lookup of the message type when the object is exactly of the type given as the type argument.

- Documentation at [forkforward.dev/dotnet-mediator](https://forkforward.dev/dotnet-mediator/): getting started, requests, notifications, streams, pipeline behaviors, exception handling, dependency injection and the guide Moving from MediatR 12. It was written for this fork from its code; the original project's wiki is not used.

### Changed

- The package's project website on nuget.org is the documentation site instead of the GitHub repository; the repository is still linked as the source. Both package readmes link the documentation.

### Fixed

- **Two registrations at the same time no longer share their limits.** The limits for generic request handlers (`MaxGenericTypeParameters`, `MaxTypesClosing`, `MaxGenericTypeRegistrations`) and `RegistrationTimeout` were kept in static fields, so an `AddMediator` call running at the same time as another one could be held to the other's limits or time out by the other's timeout. Each registration now uses the configuration it was given. If you call `ServiceRegistrar.AddMediatorClasses` or `AddMediatorClassesWithTimeout` directly, they take the limits and the timeout from their `configuration` argument; `SetGenericRequestHandlerRegistrationLimitations` now only matters for direct calls of `ServiceRegistrar.GenerateCombinations`. This was also why the test `ShouldThrowExceptionWhenTimeoutOccurs` failed now and then.

## [12.8.0] - 2026-11-12

The library moves to current .NET. There are no changes to its code or to what it does.

### Changed

- **Target frameworks: `netstandard2.0`, `net10.0` and `net11.0`.** `net6.0`, which is out of support, is gone. Applications on .NET 10 and .NET 11 get an asset built for their framework. Everything else that supports .NET Standard 2.0, which includes .NET 8, .NET 9 and .NET Framework, keeps working and now gets the `netstandard2.0` asset; for .NET 6 to .NET 9 that adds the dependency `Microsoft.Bcl.AsyncInterfaces`, which the `net6.0` asset did not need.
- On .NET 11 the package no longer depends on `Microsoft.Extensions.DependencyInjection.Abstractions`: it is part of the framework there.
- The build no longer uses the package `Microsoft.SourceLink.GitHub` 8.0.0, whose dependency `Microsoft.Build.Tasks.Git` 8.0.0 has a known vulnerability ([GHSA-23fw-v26w-5fgq](https://github.com/advisories/GHSA-23fw-v26w-5fgq)). It was used only while building and was never part of the published packages. Source Link still works: it has been part of the .NET SDK since version 8.
- For contributors: the solution is `ForkForward.Mediator.slnx`, all build output goes to `artifacts/`, and building needs the .NET 11 SDK.

## [12.7.1] - 2026-11-12

A maintenance release. The library is the same as in 12.7.0; there are no changes to the code.

### Changed

- Every release is now compared with the previous one before it is published, and every pull request that touches the library is compared before it is merged: a call must not be more than 10 % slower, and nothing may allocate more memory. This release is the first that went through the comparison, against 12.7.0. How it works is in the Performance section of [CONTRIBUTING.md](CONTRIBUTING.md#performance).

## [12.7.0] - 2026-11-12

The first stable release of ForkForward.Mediator. It is MediatR 12.5.0 (commit `8a64581e`, 2025-03-24), the last release under the Apache License 2.0, with its own identity and the build and release infrastructure of this fork. There are no changes to what the library does.

There is no 12.6.0. The only version in that line is `12.6.0-preview.1`, which still used the original assembly names and namespace, as did `ForkForward.Mediator.Contracts` 2.6.0 that was published with it. Do not use them.

### Changed

- **Own assembly names, namespace and signing key.** The assemblies are `ForkForward.Mediator.dll` and `ForkForward.Mediator.Contracts.dll`, the namespaces start with `ForkForward.Mediator` (`ForkForward.Mediator.Pipeline`, `ForkForward.Mediator.NotificationPublishers`, and so on), and both are signed with the fork's own key (public key token `fe080643cbba1f62`). The original key is no longer in the repository.
- **No name of the original project in the public API.** `AddMediatR` is `AddMediator`, `MediatRServiceConfiguration` is `MediatorServiceConfiguration`, and `ServiceRegistrar.AddMediatRClasses` and `AddMediatRClassesWithTimeout` are `AddMediatorClasses` and `AddMediatorClassesWithTimeout`. There are no `[Obsolete]` copies of the old names. All other types, their members and the behaviour are those of MediatR 12.5.0. Moving a project is a package reference, a `using` directive and those names: [Moving from MediatR 12](docs/source/Moving-from-MediatR-12.md).
- The packages are `ForkForward.Mediator` and `ForkForward.Mediator.Contracts`, each with its own description, logo, readme and project address.
- The contracts are published by the fork as `ForkForward.Mediator.Contracts`: the code and the public types of the original `MediatR.Contracts` 2.0.1. Both packages have one version, taken from the release tag, and `ForkForward.Mediator` depends on exactly that version of the contracts and no longer on the original `MediatR.Contracts` package. `ForkForward.Mediator.Contracts` 2.6.0, published with the first preview, had a version of its own and the original assembly name; it is superseded.
- Both packages carry `LICENSE` and `NOTICE.md`.
- Versions are computed from `v*.*.*` tags.

### Removed

- The scripts and workflow steps that pushed packages to the original project's feeds.

[Unreleased]: https://github.com/ForkForward/dotnet-mediator/compare/v12.8.0...HEAD
[12.8.0]: https://github.com/ForkForward/dotnet-mediator/compare/v12.7.1...v12.8.0
[12.7.1]: https://github.com/ForkForward/dotnet-mediator/compare/v12.7.0...v12.7.1
[12.7.0]: https://github.com/ForkForward/dotnet-mediator/compare/v12.5.0...v12.7.0
