# Exception handling

When a handler or a pipeline behavior throws, two kinds of classes can step in:

| Interface | Can | Use it to |
|---|---|---|
| `IRequestExceptionHandler<TRequest, TResponse, TException>` | return a response instead of the exception | fall back to a default, a cached value or an error result |
| `IRequestExceptionAction<TRequest, TException>` | only observe; the exception is thrown on | log, count, alert |

Both are in the namespace `ForkForward.Mediator.Pipeline`. `AddMediator` finds them when it scans the assemblies
and adds the behaviors that call them only when there is at least one.

## Return a response instead

```csharp
using ForkForward.Mediator.Pipeline;

public class PriceFallback(PriceCache cache) : IRequestExceptionHandler<GetPrice, decimal, TimeoutException>
{
    public Task Handle(GetPrice request, TimeoutException exception, RequestExceptionHandlerState<decimal> state,
        CancellationToken cancellationToken)
    {
        if (cache.TryGet(request.ProductId, out var price))
            state.SetHandled(price);
        return Task.CompletedTask;
    }
}
```

`state.SetHandled(response)` marks the exception as handled: `Send` returns that response and nothing is thrown.
A handler that does not call it leaves the exception to the next handler. When no handler calls it, the original
exception is thrown on, with its type and stack trace. A response of `null` counts as not handled.

## Which handler runs first

The exception handlers are tried for the exact type of the exception first, then for each of its base types up to
`Exception`. The first one that calls `SetHandled` ends the search. For a `TimeoutException`, a handler for
`TimeoutException` runs before a handler for `Exception`.

Among several handlers for the same exception type, those in the assembly of the request come first, then those
closest to it by namespace. A handler class that derives from another handler class replaces it.

## Only react

```csharp
public class LogFailures<TRequest>(ILogger<LogFailures<TRequest>> logger) : IRequestExceptionAction<TRequest, Exception>
    where TRequest : notnull
{
    public Task Execute(TRequest request, Exception exception, CancellationToken cancellationToken)
    {
        logger.LogError(exception, "{Request} failed", typeof(TRequest).Name);
        return Task.CompletedTask;
    }
}
```

An action cannot stop the exception. It is chosen in the same order as the handlers, by exception type.

## Actions and handled exceptions

`RequestExceptionActionProcessorStrategy` decides whether the actions also see exceptions that a handler has handled:

| Value | The actions run |
|---|---|
| `ApplyForUnhandledExceptions` (default) | only when no exception handler called `SetHandled`; they run after the handlers |
| `ApplyForAllExceptions` | for every exception, before the exception handlers |

```csharp
services.AddMediator(cfg =>
{
    cfg.RegisterServicesFromAssemblyContaining<Program>();
    cfg.RequestExceptionActionProcessorStrategy = RequestExceptionActionProcessorStrategy.ApplyForAllExceptions;
});
```

## Where they sit in the pipeline

The exception behaviors are the outermost part of the pipeline. They see exceptions from the handler, from every
[pipeline behavior](Pipeline-behaviors.md#order) and from the pre- and post-processors. They do not apply to
notifications or to [streams](Streams.md).
