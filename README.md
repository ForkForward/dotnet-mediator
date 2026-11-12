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

The full rules are in [AI_POLICY.md](AI_POLICY.md).

## Status

- **No package has been published yet.** The package will be `ForkForward.Mediator` on nuget.org. The plan and the decisions that are still open are in [ROADMAP.md](ROADMAP.md); what has changed so far is in [CHANGELOG.md](CHANGELOG.md).
- The code is MediatR 12.5.0. The assembly is still `MediatR.dll` and the namespace is still `MediatR`; whether they are renamed before the first release is an open decision in the roadmap.
- The library builds and its 158 tests pass with a current .NET SDK.

## Using it

In-process messaging with no dependencies. It supports request/response, commands, queries, notifications and events, synchronous and async, with dispatching via C# generic variance.

### Registering with `IServiceCollection`

`Microsoft.Extensions.DependencyInjection.Abstractions` is supported directly. To register the services and handlers:

```csharp
services.AddMediatR(cfg => cfg.RegisterServicesFromAssemblyContaining<Startup>());
```

or with an assembly:

```csharp
services.AddMediatR(cfg => cfg.RegisterServicesFromAssembly(typeof(Startup).Assembly));
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
services.AddMediatR(cfg => {
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

### The contracts

`IRequest`, `INotification`, `IStreamRequest` and `Unit` live in a separate assembly, `MediatR.Contracts`. The library takes it from the package `MediatR.Contracts` 2.0.1, which the original authors published under the Apache License 2.0 in 2023. This fork does not publish a contracts package of its own yet; see [ROADMAP.md](ROADMAP.md).

## Building

The project builds and is tested on Windows and on Linux. You need a current .NET SDK (10 or newer) and the .NET 8 runtime, which the tests run on. The library targets `netstandard2.0` and `net6.0`, as MediatR 12.5.0 does.

```powershell
./Build.ps1
```

This builds the solution, runs all tests and packs `ForkForward.Mediator` into `artifacts/`. It runs under PowerShell on both systems.

## Reporting problems and contributing

Open an issue in this repository for bugs in this fork. Do not report them to the original MediatR project; its maintainers are not responsible for this code. See [CONTRIBUTING.md](CONTRIBUTING.md).

## About the logo

The blue node is the origin: it uses the blue of the MediatR logo. The two arrows are the code moving forward from there. The mark is new artwork; the original MediatR logo is not used.

## Licence and credits

ForkForward.Mediator is released under the [Apache License 2.0](LICENSE), the same licence the original code was published under.

MediatR is Copyright Jimmy Bogard and other contributors. Thank you to them for building it and for publishing it as open source. Modifications made in this fork are Copyright 2026 ForkForward contributors and are released under the same Apache License 2.0 terms. Details are in [NOTICE.md](NOTICE.md).
