# Moving from MediatR 12

ForkForward.Mediator is the code of MediatR 12.5.0 under its own names. The types, their members and the behaviour are the same; what differs is what the packages, the assemblies, the namespace and two identifiers are called. Moving a project takes three find-and-replace steps.

## Which version

| Your application runs on | Use |
|---|---|
| .NET 10 or .NET 11 | ForkForward.Mediator 13 |
| .NET 8, .NET 9, .NET Framework 4.6.2 or later, anything else with .NET Standard 2.0 | ForkForward.Mediator 12.8 |

Both have the API of MediatR 12.5.0 under the names below. Version 13 adds registration without reflection for
[Native AOT](Native-AOT.md) and uses the runtime's async support on .NET 11; it no longer has a .NET Standard 2.0
asset.

## 1. Package references

| Remove | Add |
|---|---|
| `MediatR` | `ForkForward.Mediator` |
| `MediatR.Contracts` | `ForkForward.Mediator.Contracts` |

```
dotnet remove package MediatR
dotnet add package ForkForward.Mediator
```

`ForkForward.Mediator` depends on `ForkForward.Mediator.Contracts` in exactly its own version, so a project that references the mediator does not need a reference to the contracts. Reference `ForkForward.Mediator.Contracts` alone in projects that only declare requests and notifications.

## 2. Namespaces

Replace `MediatR` with `ForkForward.Mediator` in `using` directives and fully qualified names:

| MediatR 12 | ForkForward.Mediator |
|---|---|
| `MediatR` | `ForkForward.Mediator` |
| `MediatR.Pipeline` | `ForkForward.Mediator.Pipeline` |
| `MediatR.NotificationPublishers` | `ForkForward.Mediator.NotificationPublishers` |
| `MediatR.Registration` | `ForkForward.Mediator.Registration` |
| `MediatR.Entities` | `ForkForward.Mediator.Entities` |
| `MediatR.Wrappers` | `ForkForward.Mediator.Wrappers` |

The registration extension methods stay in `Microsoft.Extensions.DependencyInjection`, where MediatR 12 has them.

## 3. Two identifiers

| MediatR 12 | ForkForward.Mediator |
|---|---|
| `services.AddMediatR(...)` | `services.AddMediator(...)` |
| `MediatRServiceConfiguration` | `MediatorServiceConfiguration` |

If you call the registrar directly, `ServiceRegistrar.AddMediatRClasses` and `AddMediatRClassesWithTimeout` are now `AddMediatorClasses` and `AddMediatorClassesWithTimeout`.

There are no `[Obsolete]` copies of the old names: nothing in the public API carries the name of the original project.

## Example

```csharp
using ForkForward.Mediator;

services.AddMediator(cfg => cfg.RegisterServicesFromAssemblyContaining<Program>());

public record Ping(string Message) : IRequest<string>;

public class PingHandler : IRequestHandler<Ping, string>
{
    public Task<string> Handle(Ping request, CancellationToken cancellationToken)
        => Task.FromResult($"{request.Message} Pong");
}
```

## What to know

- **Everything else is unchanged.** `IMediator`, `ISender`, `IPublisher`, `IRequest`, `INotification`, `IRequestHandler<,>`, `IPipelineBehavior<,>`, `Unit`, the pre- and post-processors, the exception handlers and actions, the notification publishers and the configuration options have the names, members and behaviour of MediatR 12.5.0. The public API was compared member by member with the 12.5.0 code: after the renames above there is no difference.
- **Assemblies.** `ForkForward.Mediator.dll` and `ForkForward.Mediator.Contracts.dll`, signed with the fork's own key (public key token `fe080643cbba1f62`). A library that was compiled against `MediatR.dll` has to be recompiled against the fork; a binding redirect does not help, because the assembly name is different.
- **The two are different types now.** `ForkForward.Mediator.IRequest` is not `MediatR.IRequest`. The original and the fork can be referenced in the same application, since nothing in them has the same name any more, but a request declared with one cannot be sent through the other. Move the contracts projects together with the projects that handle them.
- **A class and a namespace share a name.** The class `Mediator` lives in the namespace `ForkForward.Mediator`. It only matters in code whose own namespace starts with `ForkForward.`: there, write `ForkForward.Mediator.Mediator` or add a `using` alias when you mean the class.
- **The preview is not this.** `ForkForward.Mediator` 12.6.0-preview.1 and `ForkForward.Mediator.Contracts` 2.6.0 were published before the rename and still contain `MediatR.dll` and `MediatR.Contracts.dll`. Use 12.7.0 or later; there is no 12.6.0.
