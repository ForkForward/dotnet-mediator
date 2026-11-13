# Getting started

A mediator stands between the code that asks for something and the code that does it. The caller sends a message
to `IMediator`; the mediator finds the handler for that message in the dependency injection container and calls it.
The caller does not know the handler, and a [pipeline](Pipeline-behaviors.md) can add logging, validation or
transactions around every handler in one place.

## Install

```
dotnet add package ForkForward.Mediator
```

## Register

`AddMediator` scans assemblies for handlers and registers them, together with `IMediator`, `ISender` and
`IPublisher`, in a `Microsoft.Extensions.DependencyInjection` container:

```csharp
using Microsoft.Extensions.DependencyInjection;

builder.Services.AddMediator(cfg => cfg.RegisterServicesFromAssemblyContaining<Program>());
```

Give it every assembly that contains handlers: `RegisterServicesFromAssemblyContaining<T>()` once per assembly, or
`RegisterServicesFromAssemblies(...)`. All options are on [Dependency injection](Dependency-injection.md).

Scanning uses reflection. An application that is trimmed or published with Native AOT registers each handler by
name instead; see [Native AOT and trimming](Native-AOT.md).

## Send a request

A request is a class or record that implements `IRequest<TResponse>`. It has exactly one handler:

```csharp
using ForkForward.Mediator;

public record GetGreeting(string Name) : IRequest<string>;

public class GetGreetingHandler : IRequestHandler<GetGreeting, string>
{
    public Task<string> Handle(GetGreeting request, CancellationToken cancellationToken)
        => Task.FromResult($"Hello, {request.Name}");
}
```

Ask the container for `ISender` (or `IMediator`) and send it:

```csharp
public class GreetingEndpoint(ISender sender)
{
    public Task<string> Get(string name, CancellationToken cancellationToken)
        => sender.Send(new GetGreeting(name), cancellationToken);
}
```

A request that returns nothing implements `IRequest`, and its handler `IRequestHandler<TRequest>`. More on
[Requests](Requests.md).

## Publish a notification

A notification implements `INotification`. It can have any number of handlers, including none:

```csharp
public record UserRegistered(string Email) : INotification;

public class SendWelcomeMail : INotificationHandler<UserRegistered>
{
    public Task Handle(UserRegistered notification, CancellationToken cancellationToken) => Task.CompletedTask;
}

public class UpdateStatistics : INotificationHandler<UserRegistered>
{
    public Task Handle(UserRegistered notification, CancellationToken cancellationToken) => Task.CompletedTask;
}
```

```csharp
await publisher.Publish(new UserRegistered("ada@example.com"), cancellationToken);
```

By default the handlers run one after the other. More on [Notifications](Notifications.md).

## Which interface to inject

| Interface | Methods | Use it in |
|---|---|---|
| `ISender` | `Send`, `CreateStream` | code that sends requests or reads streams |
| `IPublisher` | `Publish` | code that only publishes notifications |
| `IMediator` | all of them | code that does both |

All three resolve to the same object. Injecting the narrowest one says what a class does with the mediator.

## Contracts in their own assembly

`IRequest`, `IRequest<TResponse>`, `INotification`, `IStreamRequest<TResponse>` and `Unit` are in a separate package,
`ForkForward.Mediator.Contracts`. A project that only declares messages, for example a shared API contracts library
or a Blazor client, can reference that package alone. `ForkForward.Mediator` depends on it in exactly its own version.
