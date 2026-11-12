# Dependency injection

ForkForward.Mediator works with `Microsoft.Extensions.DependencyInjection`. `AddMediator` is in the namespace
`Microsoft.Extensions.DependencyInjection`, like the other `Add...` methods of a service collection.

```csharp
services.AddMediator(cfg =>
{
    cfg.RegisterServicesFromAssemblyContaining<Program>();
});
```

At least one assembly is required; without one, `AddMediator` throws an `ArgumentException`.

## What it registers

| Service | Implementation | Lifetime |
|---|---|---|
| `IMediator` | `Mediator`, or `MediatorImplementationType` | `Lifetime` (transient by default) |
| `ISender`, `IPublisher` | the `IMediator` of the same scope | `Lifetime` |
| `INotificationPublisher` | `NotificationPublisher`, or `NotificationPublisherType` | singleton instance, or `Lifetime` for a type |
| `IRequestHandler<,>`, `IRequestHandler<>` | the handlers found by the scan | transient |
| `INotificationHandler<>` | the handlers found by the scan, open generic ones included | transient |
| `IStreamRequestHandler<,>` | the handlers found by the scan | transient |
| `IRequestExceptionHandler<,,>`, `IRequestExceptionAction<,>` | the classes found by the scan, open generic ones included | transient |
| `IPipelineBehavior<,>`, `IStreamPipelineBehavior<,>` | the behaviors you add, and the ones for processors and exceptions | as given; transient by default |

`IMediator`, `ISender`, `IPublisher` and `INotificationPublisher` are added only if they are not registered yet, so
a registration made before `AddMediator` wins. `Lifetime` applies to these four, not to the handlers, which are
always transient.

## Options

| Option | Default | Meaning |
|---|---|---|
| `RegisterServicesFromAssemblyContaining<T>()`, `RegisterServicesFromAssembly(...)`, `RegisterServicesFromAssemblies(...)` | none | the assemblies to scan |
| `TypeEvaluator` | every type | a filter: only the types for which it returns `true` are registered |
| `Lifetime` | `Transient` | the lifetime of `IMediator`, `ISender`, `IPublisher`, and of a `NotificationPublisherType` |
| `MediatorImplementationType` | `typeof(Mediator)` | your own class deriving from `Mediator`, for example one that overrides `PublishCore` |
| `NotificationPublisher` | `new ForeachAwaitPublisher()` | how notification handlers are called; see [Notifications](Notifications.md#how-the-handlers-are-called) |
| `NotificationPublisherType` | none | a publisher type created by the container; overrides `NotificationPublisher` |
| `RequestExceptionActionProcessorStrategy` | `ApplyForUnhandledExceptions` | see [Exception handling](Exception-handling.md#actions-and-handled-exceptions) |
| `AutoRegisterRequestProcessors` | `false` | also register the pre- and post-processors found by the scan; see the warning on [Pipeline behaviors](Pipeline-behaviors.md#pre-and-post-processors) |
| `RegisterGenericHandlers` | `false` | also register generic request handlers; see [Requests](Requests.md#generic-handlers) |
| `MaxGenericTypeParameters` | `10` | generic handlers with more type parameters are not registered |
| `MaxTypesClosing` | `100` | most types that may close one type parameter of a generic handler |
| `MaxGenericTypeRegistrations` | `125000` | most closed generic handlers registered in total |
| `RegistrationTimeout` | `15000` | milliseconds the registration of generic handlers may take before it ends in a `TimeoutException` |

A value of `0` switches off any of the last four limits. `MaxGenericTypeRegistrations` is checked only while
`MaxGenericTypeParameters` is not `0`, so switching off the first switches off both; that is how MediatR 12.5.0
behaves. When a limit is reached, `AddMediator` throws an `ArgumentException` that names it, or a `TimeoutException`
for `RegistrationTimeout`.

!!! note "Two containers with different limits at the same time"

    The four limits are kept in static fields while `AddMediator` runs. Two calls that run at the same moment, with
    different limits, can see each other's values. This is inherited from MediatR 12.5.0 and is recorded in the
    [roadmap](https://github.com/ForkForward/dotnet-mediator/blob/main/ROADMAP.md) to be fixed. It does not affect
    an application that builds one container at startup.

Behaviors and processors are added with the methods on [Pipeline behaviors](Pipeline-behaviors.md#register-it).

## Other containers

Containers that work with `Microsoft.Extensions.DependencyInjection` use `AddMediator` through their integration
package: register into a `ServiceCollection`, then hand it to the container. The
[samples](https://github.com/ForkForward/dotnet-mediator/tree/main/samples) show this for Autofac, DryIoc, Lamar,
LightInject, SimpleInjector and Stashbox, and for ASP.NET Core.
