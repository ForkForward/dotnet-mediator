# Native AOT and trimming

From version 13 ForkForward.Mediator works in applications that are published with
[Native AOT](https://learn.microsoft.com/dotnet/core/deploying/native-aot/) or trimmed. Such an application has
no JIT: the compiler builds native code for everything it can see being used, and removes the rest. Two things the
mediator does with a JIT are not possible there, and each has a replacement.

| With a JIT | Why it cannot work under Native AOT | Instead |
|---|---|---|
| `RegisterServicesFromAssembly` finds handlers by reflection. | The compiler cannot see that the handlers are used, and removes them. | Register each handler with a method that names it. |
| A message of a type first seen at run time gets its dispatch code created on the spot. | There is no code generator in the application. | The same registration creates that code at compile time. |

## Register handlers one by one

```csharp
services.AddMediator(cfg =>
{
    cfg.AddRequestHandler<GetOrder, OrderDto, GetOrderHandler>();        // request with a response
    cfg.AddRequestHandler<CancelOrder, CancelOrderHandler>();            // request without a response
    cfg.AddNotificationHandler<OrderPlaced, ReserveStock>();             // any number per notification
    cfg.AddNotificationHandler<OrderPlaced, NotifyWarehouse>();
    cfg.AddStreamRequestHandler<GetOrderLines, OrderLine, GetOrderLinesHandler>();
    cfg.AddRequestExceptionHandler<GetPrice, decimal, TimeoutException, PriceFallback>();
    cfg.AddRequestExceptionAction<GetPrice, Exception, LogPriceFailures>();
});
```

| Method | Registers |
|---|---|
| `AddRequestHandler<TRequest, TResponse, THandler>()` | the handler of a request with a response |
| `AddRequestHandler<TRequest, THandler>()` | the handler of a request without a response |
| `AddNotificationHandler<TNotification, THandler>()` | one handler of a notification |
| `AddStreamRequestHandler<TRequest, TResponse, THandler>()` | the handler of a stream request |
| `AddRequestExceptionHandler<TRequest, TResponse, TException, THandler>()` | an [exception handler](Exception-handling.md) |
| `AddRequestExceptionAction<TRequest, TException, TAction>()` | an [exception action](Exception-handling.md#only-react) |

No assembly is needed when handlers are added this way. The methods use no reflection and also work with a JIT,
where they can be mixed with scanning: a request keeps its first handler, and a handler that scanning has already
found is not registered twice.

## The compiler tells you

`RegisterServicesFromAssembly`, `RegisterServicesFromAssemblyContaining` and `RegisterServicesFromAssemblies` are
marked with `RequiresUnreferencedCode` and `RequiresDynamicCode`. An application with `PublishAot`, `PublishTrimmed`
or `IsAotCompatible` set gets the warnings IL2026 and IL3050 at the line that calls them. An application without
those settings sees nothing and works as before.

## What works

These are checked on every change: a test application is published with Native AOT and run, on Linux and on
Windows, for .NET 10 and for .NET 11
([source](https://github.com/ForkForward/dotnet-mediator/tree/main/test/ForkForward.Mediator.AotTests)).

- `Send` for requests with a reference-type response, a value-type response and no response; `Send(object)`.
- `Publish` to any number of handlers, with both notification publishers and with one of your own; `Publish(object)`.
- `CreateStream` and `CreateStream(object)`, with cancellation.
- Behaviors and stream behaviors registered closed, in registration order.
- Pre- and post-processors, closed and open, for every kind of request.
- Exception handlers and exception actions with both strategies, for every kind of request.
- `Lifetime`, `NotificationPublisherType` and `MediatorImplementationType`.
- Handlers of notifications and of requests without a response that were put into the container directly, when
  they are sent with `Publish<TNotification>` and `Send<TRequest>`.

## What is different

**A message has to be registered before it is sent as an object.** `Send(object)`, `Publish(object)`,
`CreateStream(object)`, and `Send` and `CreateStream` for a request with a response find the message type at run
time. If its handler was not added with one of the methods above, the call ends in an `InvalidOperationException`
that names the method to add. `Publish<TNotification>` and `Send<TRequest>` for a request without a response know
the type at compile time and need no registration for that; a notification without handlers is published without
an error.

**Open generic behaviors and value types.** The dependency injection container cannot close an open generic type
over a value type under Native AOT. A request without a response has the response type `Unit`, which is a value
type, like `int` or `bool`. So an open behavior works for requests whose response is a reference type, and for
any other request the container refuses, with its own message:

```
Unable to create a generic service for type 'IPipelineBehavior`2[CancelOrder,Unit]' because 'Unit' is a ValueType.
Native code to support creating generic services might not be available with native AOT.
```

When an application has such requests, register the behavior closed for each request instead of open:

```csharp
cfg.AddBehavior<IPipelineBehavior<GetOrder, OrderDto>, Timing<GetOrder, OrderDto>>();
cfg.AddBehavior<IPipelineBehavior<CancelOrder, Unit>, Timing<CancelOrder, Unit>>();
```

The same holds for open stream behaviors and streams of value types.

**The built-in behaviors follow the registrations.** The behaviors that run pre-processors, post-processors,
exception handlers and exception actions are registered for the requests that were added with `AddRequestHandler`.
That is what makes them work for requests without a response and with value-type responses. A request handler
that was put into the container directly, without `AddRequestHandler`, does not get them.

**Exception handlers and actions have to be added with their methods.** With a JIT the mediator finds every
`IRequestExceptionHandler` in the container. Under Native AOT it finds the ones added with
`AddRequestExceptionHandler` and `AddRequestExceptionAction`. The order in which they run is the same.

**Generic request handlers are not available.** `RegisterGenericHandlers` closes handler types at run time and is
part of scanning.

## Trimming without Native AOT

An application published with `PublishTrimmed` still has a JIT. Register handlers one by one there too, so that
the trimmer keeps them; everything else behaves as it does with a JIT, including open behaviors for every kind of
request and messages that were not registered. The same test application is published trimmed and run on every
change as well.

## Trying it without publishing

For a project with `PublishAot`, the .NET SDK switches dynamic code off under `dotnet run` too. The mediator then
takes the same paths as under Native AOT, and a message that is not registered is refused in development already,
not only in the published application.

## Later

A source generator that writes these registrations, including the closed behaviors, from the handlers in a project
is on the [roadmap](https://github.com/ForkForward/dotnet-mediator/blob/main/ROADMAP.md).
