# ForkForward.Mediator

In-process messaging for .NET: requests with one handler, notifications with many, and streams, with a pipeline around each call.

!!! info "A community fork of MediatR"

    ForkForward.Mediator is a community fork of [MediatR](https://github.com/LuckyPennySoftware/MediatR), created by
    Jimmy Bogard. It continues the last Apache-2.0-licensed MediatR code (12.5.0) under its own names: the package,
    the assemblies and the namespace are `ForkForward.Mediator`, and `AddMediatR` is `AddMediator`. Everything else
    behaves as in MediatR 12.5.0; [Moving from MediatR 12](Moving-from-MediatR-12.md) lists what differs.

    The original project kept its documentation in a GitHub wiki, which is not part of the source code and today
    describes later versions under another licence. These pages were written for this fork from its code, its tests
    and its samples.

    ForkForward is an independent project and is not affiliated with or endorsed by the original authors.

## Install

```
dotnet add package ForkForward.Mediator
```

Version 13 runs on .NET 10 and .NET 11, with an asset for each. On .NET 11 it uses the runtime's own async
support. It also works in applications published with [Native AOT](Native-AOT.md).

Applications on .NET 8, .NET 9 or .NET Framework use version 12.8, which also has a .NET Standard 2.0 asset:
`dotnet add package ForkForward.Mediator --version 12.8.0`.

## In one example

```csharp
using ForkForward.Mediator;
using Microsoft.Extensions.DependencyInjection;

var services = new ServiceCollection();
services.AddMediator(cfg => cfg.RegisterServicesFromAssemblyContaining<Ping>());
var mediator = services.BuildServiceProvider().GetRequiredService<IMediator>();

var reply = await mediator.Send(new Ping("Hello"));   // "Hello Pong"

public record Ping(string Message) : IRequest<string>;

public class PingHandler : IRequestHandler<Ping, string>
{
    public Task<string> Handle(Ping request, CancellationToken cancellationToken)
        => Task.FromResult($"{request.Message} Pong");
}
```

## The pages

- [Getting started](Getting-started.md): install, register, send a request and publish a notification.
- [Requests](Requests.md): one handler per request, with or without a response.
- [Notifications](Notifications.md): any number of handlers, and how they are called.
- [Streams](Streams.md): a request answered with `IAsyncEnumerable<T>`.
- [Pipeline behaviors](Pipeline-behaviors.md): code that runs around every handler, and pre- and post-processors.
- [Exception handling](Exception-handling.md): turning an exception into a response, or reacting to it.
- [Dependency injection](Dependency-injection.md): what `AddMediator` registers and every option it takes.
- [Native AOT and trimming](Native-AOT.md): registering handlers one by one, what works and what is different.
- [Moving from MediatR 12](Moving-from-MediatR-12.md): package references, namespaces and two renamed identifiers.

## Examples

The [samples](https://github.com/ForkForward/dotnet-mediator/tree/main/samples) folder has runnable programs for
Microsoft.Extensions.DependencyInjection, ASP.NET Core, Autofac, DryIoc, Lamar, LightInject, SimpleInjector and
Stashbox. The [tests](https://github.com/ForkForward/dotnet-mediator/tree/main/test/ForkForward.Mediator.Tests)
cover every feature described here.

## Housekeeping

The releases are on [NuGet](https://www.nuget.org/packages/ForkForward.Mediator); what changed in each is in the
[changelog](https://github.com/ForkForward/dotnet-mediator/blob/main/CHANGELOG.md).

Questions and bug reports go to the [issues](https://github.com/ForkForward/dotnet-mediator/issues) of this fork.
