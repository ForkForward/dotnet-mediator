# Notifications

A notification is a message with any number of handlers, including none. Use it for events: something happened,
and whoever is interested reacts.

## Publish

```csharp
using ForkForward.Mediator;

public record OrderPlaced(int OrderId) : INotification;

public class ReserveStock : INotificationHandler<OrderPlaced>
{
    public Task Handle(OrderPlaced notification, CancellationToken cancellationToken) => Task.CompletedTask;
}

public class NotifyWarehouse : INotificationHandler<OrderPlaced>
{
    public Task Handle(OrderPlaced notification, CancellationToken cancellationToken) => Task.CompletedTask;
}
```

```csharp
await publisher.Publish(new OrderPlaced(42), cancellationToken);
```

`AddMediator` registers every handler it finds. A notification without handlers is published without an error.
`Publish(object)` does the same for an object whose type is known only at run time; an object that does not
implement `INotification` ends in an `ArgumentException`.

## Synchronous handlers

A handler that does no asynchronous work can derive from `NotificationHandler<TNotification>` and override a
`void` method:

```csharp
public class CountOrders(Statistics statistics) : NotificationHandler<OrderPlaced>
{
    protected override void Handle(OrderPlaced notification) => statistics.OrdersPlaced++;
}
```

## One handler for a family of notifications

A handler of a base type does **not** receive notifications of derived types when the container is
Microsoft.Extensions.DependencyInjection, because that container does not resolve variant generic types.
`INotificationHandler<OrderEvent>` is not called for an `OrderShipped : OrderEvent`.

Write the handler as an open generic type with a constraint instead. `AddMediator` registers open generic
notification handlers, and the container closes them for every matching notification:

```csharp
public abstract record OrderEvent(int OrderId) : INotification;
public record OrderShipped(int OrderId) : OrderEvent(OrderId);

public class AuditOrderEvents<TEvent> : INotificationHandler<TEvent> where TEvent : OrderEvent
{
    public Task Handle(TEvent notification, CancellationToken cancellationToken) => Task.CompletedTask;
}
```

## How the handlers are called

A notification publisher decides how the handlers run. Two are included:

| Publisher | What it does | When a handler throws |
|---|---|---|
| `ForeachAwaitPublisher` (default) | Calls the handlers one after the other and awaits each before the next. | The exception ends `Publish`; the handlers after it do not run. |
| `TaskWhenAllPublisher` | Starts every handler and awaits them together with `Task.WhenAll`. | The others still run to the end; `Publish` then throws the first exception. A handler that throws *before* it returns its task, from code that is not `async`, stops the handlers after it from starting. |

Choose one in the configuration:

```csharp
using ForkForward.Mediator.NotificationPublishers;

services.AddMediator(cfg =>
{
    cfg.RegisterServicesFromAssemblyContaining<Program>();
    cfg.NotificationPublisher = new TaskWhenAllPublisher();
});
```

`TaskWhenAllPublisher` runs the handlers at the same time. Handlers that share a scoped service which is not
thread-safe, such as an Entity Framework `DbContext`, must not run in parallel.

## A publisher of your own

Implement `INotificationPublisher`. It receives one `NotificationHandlerExecutor` per handler: the handler object,
and a callback that calls it.

```csharp
public class LoggingPublisher(ILogger<LoggingPublisher> logger) : INotificationPublisher
{
    public async Task Publish(IEnumerable<NotificationHandlerExecutor> handlerExecutors, INotification notification,
        CancellationToken cancellationToken)
    {
        foreach (var executor in handlerExecutors)
        {
            logger.LogInformation("{Handler} handles {Notification}", executor.HandlerInstance.GetType().Name, notification);
            await executor.HandlerCallback(notification, cancellationToken);
        }
    }
}
```

Set an instance with `cfg.NotificationPublisher = ...`, or set `cfg.NotificationPublisherType = typeof(LoggingPublisher)`
to have the container create it, with its dependencies, in the lifetime given by `cfg.Lifetime`.

The [PublishStrategies sample](https://github.com/ForkForward/dotnet-mediator/tree/main/samples/ForkForward.Mediator.Examples.PublishStrategies)
shows further strategies: in parallel without waiting, in the background, and continuing past failures.
