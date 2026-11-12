<p align="center">
  <img src="assets/brand/banner.png" alt="ForkForward.Mediator: community fork of MediatR, Apache-2.0, developed with AI tools" width="640">
</p>

# ForkForward.Mediator

**ForkForward.Mediator is a community fork of [MediatR](https://github.com/LuckyPennySoftware/MediatR)**, the simple mediator implementation for .NET created by [Jimmy Bogard](https://jimmybogard.com). It continues the last Apache-2.0-licensed code of MediatR, version 12.5.0, and keeps it building on current .NET. Changes in this fork are written by AI coding tools and automation; people review them and are responsible for releases.

> [!IMPORTANT]
> This is **not** the official MediatR. ForkForward is an independent project. It is not affiliated with, sponsored by, or endorsed by Jimmy Bogard, Lucky Penny Software, or the MediatR project. The name "MediatR" appears here only to say where the code comes from.
> The official MediatR is at [LuckyPennySoftware/MediatR](https://github.com/LuckyPennySoftware/MediatR).

## Where the code comes from

| | |
|---|---|
| Original project | MediatR, by Jimmy Bogard and the MediatR contributors |
| Source repository | [LuckyPennySoftware/MediatR](https://github.com/LuckyPennySoftware/MediatR) (this repository is a GitHub fork of it) |
| Forked from | commit [`8a64581e`](https://github.com/LuckyPennySoftware/MediatR/commit/8a64581e2570e7410c5f44835d9ea112beccbe6b) of 2025-03-24, which is release `v12.5.0` |
| Licence of the original code | Apache License 2.0, Copyright Jimmy Bogard |
| Licence of this fork | Apache License 2.0, unchanged ([LICENSE](LICENSE)) |
| What this fork changed | `git log v12.5.0..HEAD`, summarised in [NOTICE.md](NOTICE.md) |

All the design, the API and nearly all of the code are the work of the original authors. This fork adds maintenance on top.

## Why this fork exists

The official MediatR continues at [LuckyPennySoftware/MediatR](https://github.com/LuckyPennySoftware/MediatR). Since version 13.0.0 it is published under the Reciprocal Public License 1.5 or a commercial licence. Version 12.5.0 is the last one under the Apache License 2.0. That licence lets anyone keep the code alive, and that is all this fork does: it keeps the Apache-2.0 line compiling, tested and compatible as .NET moves on.

If you want new features from the original authors, or commercial support, use the official MediatR.

## How this fork is developed

- Changes are produced with AI coding tools and automation such as Dependabot.
- A person reviews every change before it is merged and decides what gets released.
- No code is taken from MediatR 13.0.0 or later, or from any source whose licence does not allow it. The branches and tags of this repository stop at `v12.5.0` of the original.
- Every change to the library is measured against the last release before it is merged, and again before it is published: it must not be slower and must not allocate more memory.

The full rules are in [AI_POLICY.md](AI_POLICY.md).

## Status

- The current release is `12.8.0`, on nuget.org: [`ForkForward.Mediator`](https://www.nuget.org/packages/ForkForward.Mediator), with its contracts package [`ForkForward.Mediator.Contracts`](https://www.nuget.org/packages/ForkForward.Mediator.Contracts) in the same version. It runs on .NET 10 and .NET 11 with assets of their own, and on everything that supports .NET Standard 2.0, which includes .NET 8, .NET 9 and .NET Framework 4.6.2 and later. What changed in each release is in [CHANGELOG.md](CHANGELOG.md); the plan is in [ROADMAP.md](ROADMAP.md).

  ```
  dotnet add package ForkForward.Mediator
  ```

- Do not use `ForkForward.Mediator` 12.6.0-preview.1 or `ForkForward.Mediator.Contracts` 2.6.0. They were published before the rename described below and still contain `MediatR.dll` and `MediatR.Contracts.dll`. There is no 12.6.0.
- The code is MediatR 12.5.0 under its own names. The assemblies are `ForkForward.Mediator.dll` and `ForkForward.Mediator.Contracts.dll`, the namespaces start with `ForkForward.Mediator`, and both are signed with the fork's own key. Nothing in the public API carries the old name: `AddMediatR` is `AddMediator` and `MediatRServiceConfiguration` is `MediatorServiceConfiguration`. All other types, their members and the behaviour are those of MediatR 12.5.0. Moving a project is a package reference, a `using` directive and those two names: [Moving from MediatR 12](docs/source/Moving-from-MediatR-12.md).
- The library builds and its 158 tests pass with a current .NET SDK.

## Using it

In-process messaging with no dependencies. It supports request/response, commands, queries, notifications and events, synchronous and async, with dispatching via C# generic variance.

### Registering with `IServiceCollection`

`Microsoft.Extensions.DependencyInjection.Abstractions` is supported directly. To register the services and handlers:

```csharp
using ForkForward.Mediator;

services.AddMediator(cfg => cfg.RegisterServicesFromAssemblyContaining<Startup>());
```

or with an assembly:

```csharp
services.AddMediator(cfg => cfg.RegisterServicesFromAssembly(typeof(Startup).Assembly));
```

This registers:

- `IMediator` as transient
- `ISender` as transient
- `IPublisher` as transient
- `IRequestHandler<,>` concrete implementations as transient
- `IRequestHandler<>` concrete implementations as transient
- `INotificationHandler<>` concrete implementations as transient
- `IStreamRequestHandler<>` concrete implementations as transient
- `IRequestExceptionHandler<,,>` concrete implementations as transient
- `IRequestExceptionAction<,>` concrete implementations as transient

This also registers open generic implementations for:

- `INotificationHandler<>`
- `IRequestExceptionHandler<,,>`
- `IRequestExceptionAction<,>`

To register behaviors, stream behaviors, pre/post processors:

```csharp
services.AddMediator(cfg => {
    cfg.RegisterServicesFromAssembly(typeof(Startup).Assembly);
    cfg.AddBehavior<PingPongBehavior>();
    cfg.AddStreamBehavior<PingPongStreamBehavior>();
    cfg.AddRequestPreProcessor<PingPreProcessor>();
    cfg.AddRequestPostProcessor<PingPongPostProcessor>();
    cfg.AddOpenBehavior(typeof(GenericBehavior<,>));
    });
```

With additional methods for open generics and overloads for explicit service types.

Runnable examples for several containers are in [samples](samples).

The documentation is at [forkforward.dev/dotnet-mediator](https://forkforward.dev/dotnet-mediator/). Its source is in [docs/source](docs/source), starting with [Getting started](docs/source/Getting-started.md).

### The contracts

`IRequest`, `INotification`, `IStreamRequest` and `Unit` live in a separate assembly, `ForkForward.Mediator.Contracts.dll`, in the same namespace as the mediator. This fork builds it from `src/ForkForward.Mediator.Contracts` and publishes it as its own package, `ForkForward.Mediator.Contracts`. The code and the public types are those of the original `MediatR.Contracts` 2.0.1. Both packages always have the same version, and `ForkForward.Mediator` depends on exactly that version of the contracts and on no package of the original project.

Reference `ForkForward.Mediator.Contracts` alone where your requests and notifications live in a separate assembly from their handlers, for example API contracts, gRPC contracts or Blazor.

## Building

The project builds and is tested on Windows and on Linux. You need the .NET 11 SDK and the .NET 10 runtime: the library targets `netstandard2.0`, `net10.0` and `net11.0`, and the tests run on .NET 10 and on .NET 11.

```powershell
./Build.ps1
```

This builds the solution, runs all tests and packs `ForkForward.Mediator` and `ForkForward.Mediator.Contracts`. It runs under PowerShell on both systems.

All build output goes to `artifacts/` instead of `bin` and `obj` folders next to each project: the packages to `artifacts/package/release`, test results to `artifacts/TestResults`.

## Reporting problems and contributing

Open an issue in this repository for bugs in this fork. Do not report them to the original MediatR project; its maintainers are not responsible for this code. See [CONTRIBUTING.md](CONTRIBUTING.md).

## About the logo

The blue node is the origin: it uses the blue of the MediatR logo. The two arrows are the code moving forward from there. The mark is new artwork; the original MediatR logo is not used.

## Licence and credits

ForkForward.Mediator is released under the [Apache License 2.0](LICENSE), the same licence the original code was published under.

MediatR is Copyright Jimmy Bogard and other contributors. Thank you to them for building it and for publishing it as open source. Modifications made in this fork are Copyright 2026 ForkForward contributors and are released under the same Apache License 2.0 terms. Details are in [NOTICE.md](NOTICE.md).
