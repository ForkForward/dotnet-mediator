# ForkForward.Mediator

A community fork of [MediatR](https://github.com/LuckyPennySoftware/MediatR), the simple mediator implementation for .NET created by Jimmy Bogard, continuing the last Apache-2.0-licensed code (12.5.0) under the Apache License 2.0, with its own assembly name, namespace and signing key. Independent project: not affiliated with, sponsored by, or endorsed by the original authors. The official MediatR is at [LuckyPennySoftware/MediatR](https://github.com/LuckyPennySoftware/MediatR).

In-process messaging with no dependencies: request/response, commands, queries, notifications and events, synchronous and async.

```csharp
using ForkForward.Mediator;

services.AddMediator(cfg => cfg.RegisterServicesFromAssemblyContaining<Program>());

// later, with an IMediator from the container
var response = await mediator.Send(new Ping("Ping"));

public record Ping(string Message) : IRequest<string>;

public class PingHandler : IRequestHandler<Ping, string>
{
    public Task<string> Handle(Ping request, CancellationToken cancellationToken)
        => Task.FromResult($"{request.Message} Pong");
}
```

## Moving from MediatR 12

1. Replace the `MediatR` package reference with `ForkForward.Mediator`, and a `MediatR.Contracts` reference, where a project has one, with `ForkForward.Mediator.Contracts`.
2. Replace `using MediatR` with `using ForkForward.Mediator` (same for `MediatR.Pipeline`, `MediatR.NotificationPublishers`, and so on).
3. Replace `AddMediatR` with `AddMediator`, and `MediatRServiceConfiguration` with `MediatorServiceConfiguration`.

That is all: the other types, their members and the behaviour are those of MediatR 12.5.0. Full guide: [Moving from MediatR 12](https://github.com/ForkForward/dotnet-mediator/blob/main/docs/Moving-from-MediatR-12.md).

The contracts (`IRequest`, `INotification`, `IStreamRequest`, `Unit`) come from `ForkForward.Mediator.Contracts`, which this package depends on in exactly its own version.

## Links

- Source: https://github.com/ForkForward/dotnet-mediator
- Changelog: https://github.com/ForkForward/dotnet-mediator/blob/main/CHANGELOG.md
- Licence: Apache License 2.0. Copyright Jimmy Bogard and MediatR contributors; modifications copyright 2026 ForkForward contributors. See NOTICE.md in the package.
