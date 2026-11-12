# Requests

A request is a message with exactly one handler. Use it for commands and queries.

## With a response

```csharp
using ForkForward.Mediator;

public record GetOrder(int Id) : IRequest<OrderDto>;

public class GetOrderHandler(OrderStore store) : IRequestHandler<GetOrder, OrderDto>
{
    public async Task<OrderDto> Handle(GetOrder request, CancellationToken cancellationToken)
        => await store.Find(request.Id, cancellationToken);
}
```

```csharp
OrderDto order = await sender.Send(new GetOrder(42), cancellationToken);
```

The type of the response comes from the request, so `Send` needs no type arguments.

## Without a response

```csharp
public record CancelOrder(int Id) : IRequest;

public class CancelOrderHandler(OrderStore store) : IRequestHandler<CancelOrder>
{
    public Task Handle(CancelOrder request, CancellationToken cancellationToken)
        => store.Cancel(request.Id, cancellationToken);
}
```

```csharp
await sender.Send(new CancelOrder(42), cancellationToken);
```

Inside the pipeline such a request has the response type `Unit`, a value that stands for "nothing". A
[pipeline behavior](Pipeline-behaviors.md) written for any `TResponse` therefore runs for these requests too, with
`TResponse` being `Unit`.

## When the type is known only at run time

`Send(object)` finds the request type of the object it is given:

```csharp
object request = Deserialize(message);
object? response = await sender.Send(request, cancellationToken);
```

The result is the handler's response, or `Unit.Value` for a request without one. An object that implements neither
`IRequest` nor `IRequest<TResponse>` ends in an `ArgumentException`.

## One handler, and only one

`AddMediator` registers one handler per request type. If none is registered, `Send` ends in an
`InvalidOperationException` from the container (`No service for type 'ForkForward.Mediator.IRequestHandler...'`).
If the scan finds two handlers for the same request, the first one found is registered and the second is ignored.
A handler registered in the container before `AddMediator` is kept.

## Cancellation

The `CancellationToken` given to `Send` reaches every pipeline behavior and the handler. Pass it on to the database
or HTTP calls the handler makes.

## Generic handlers

A handler can be generic itself, for example one handler for every `GetById<TEntity>` request:

```csharp
public record GetById<TEntity>(int Id) : IRequest<TEntity?> where TEntity : class, IEntity;

public class GetByIdHandler<TEntity>(DbContext db) : IRequestHandler<GetById<TEntity>, TEntity?>
    where TEntity : class, IEntity
{
    public async Task<TEntity?> Handle(GetById<TEntity> request, CancellationToken cancellationToken)
        => await db.Set<TEntity>().FindAsync([request.Id], cancellationToken);
}
```

Such handlers are not registered unless you ask for it:

```csharp
services.AddMediator(cfg =>
{
    cfg.RegisterServicesFromAssemblyContaining<Program>();
    cfg.RegisterGenericHandlers = true;
});
```

`AddMediator` then registers one closed handler for every type in the scanned assemblies that satisfies the
constraints. That can be a lot of registrations, so it is limited by `MaxGenericTypeParameters`, `MaxTypesClosing`,
`MaxGenericTypeRegistrations` and `RegistrationTimeout`; see [Dependency injection](Dependency-injection.md#options).
