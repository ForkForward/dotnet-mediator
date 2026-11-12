# Pipeline behaviors

A pipeline behavior runs around the handler of a request. It sees the request before the handler and the response
after it, and decides whether to call the handler at all. Logging, validation, caching, transactions and timing are
written once as behaviors instead of in every handler.

## Write a behavior

```csharp
using ForkForward.Mediator;

public class Timing<TRequest, TResponse>(ILogger<Timing<TRequest, TResponse>> logger)
    : IPipelineBehavior<TRequest, TResponse> where TRequest : notnull
{
    public async Task<TResponse> Handle(TRequest request, RequestHandlerDelegate<TResponse> next,
        CancellationToken cancellationToken)
    {
        var started = Stopwatch.GetTimestamp();
        var response = await next(cancellationToken);
        logger.LogInformation("{Request} took {Elapsed}", typeof(TRequest).Name, Stopwatch.GetElapsedTime(started));
        return response;
    }
}
```

`next` is the rest of the pipeline: the next behavior, and in the end the handler. A behavior that does not call it
ends the request with its own response, for example from a cache.

## Register it

Behaviors are not found by scanning; register each one:

```csharp
services.AddMediator(cfg =>
{
    cfg.RegisterServicesFromAssemblyContaining<Program>();
    cfg.AddOpenBehavior(typeof(Timing<,>));   // every request
    cfg.AddBehavior<OnlyForGetOrder>();       // only the requests it is written for
});
```

| Method | Registers |
|---|---|
| `AddOpenBehavior(typeof(B<,>), lifetime)` | an open generic behavior, for every request it can be closed for |
| `AddOpenBehaviors(types, lifetime)` | several open behaviors, in the order given |
| `AddOpenBehaviors(IEnumerable<OpenBehavior>)` | several open behaviors, each with its own lifetime |
| `AddBehavior<TImplementation>(lifetime)` | a closed behavior, for every `IPipelineBehavior<TRequest, TResponse>` it implements |
| `AddBehavior<TService, TImplementation>(lifetime)` | a closed behavior, for one service type |

The lifetime is `ServiceLifetime.Transient` unless you give another one.

## For some requests only

Constrain the type parameters, and the behavior applies only to requests that satisfy the constraints:

```csharp
public interface ICommand<TResponse> : IRequest<TResponse>;

public class Transaction<TRequest, TResponse>(AppDbContext db) : IPipelineBehavior<TRequest, TResponse>
    where TRequest : ICommand<TResponse>
{
    public async Task<TResponse> Handle(TRequest request, RequestHandlerDelegate<TResponse> next,
        CancellationToken cancellationToken)
    {
        await using var transaction = await db.Database.BeginTransactionAsync(cancellationToken);
        var response = await next(cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        return response;
    }
}
```

Requests without a response go through the same pipeline with `TResponse` being `Unit` (see [Requests](Requests.md)).

## Order

The behavior registered first is the outermost one: it runs first on the way in and last on the way out.

```csharp
cfg.AddOpenBehavior(typeof(Outer<,>));
cfg.AddOpenBehavior(typeof(Inner<,>));
// Outer in > Inner in > handler > Inner out > Outer out
```

The pipeline that `AddMediator` builds, from the outside in:

1. exception actions and exception handlers ([Exception handling](Exception-handling.md)), when there are any;
2. the pre-processors;
3. the post-processors;
4. your behaviors, in the order they were registered;
5. the handler.

The post-processors therefore run after your behaviors have returned, and an exception handler sees the exceptions
of every behavior.

## Pre- and post-processors

For code that only has to run before or after the handler, without wrapping it, there are processors:

```csharp
using ForkForward.Mediator.Pipeline;

public class Validate<TRequest>(IEnumerable<IValidator<TRequest>> validators) : IRequestPreProcessor<TRequest>
    where TRequest : notnull
{
    public async Task Process(TRequest request, CancellationToken cancellationToken)
    {
        foreach (var validator in validators)
            await validator.ValidateAndThrowAsync(request, cancellationToken);
    }
}

public class Audit<TRequest, TResponse>(AuditLog log) : IRequestPostProcessor<TRequest, TResponse>
    where TRequest : notnull
{
    public Task Process(TRequest request, TResponse response, CancellationToken cancellationToken)
        => log.Write(request, response, cancellationToken);
}
```

```csharp
services.AddMediator(cfg =>
{
    cfg.RegisterServicesFromAssemblyContaining<Program>();
    cfg.AddOpenRequestPreProcessor(typeof(Validate<>));
    cfg.AddOpenRequestPostProcessor(typeof(Audit<,>));
});
```

`AddRequestPreProcessor<T>()` and `AddRequestPostProcessor<T>()` register closed processors. Several processors of
one kind run one after the other, in the order they were registered.

!!! warning "`AutoRegisterRequestProcessors` alone does not run the processors"

    With `cfg.AutoRegisterRequestProcessors = true`, the scan registers every pre- and post-processor it finds, but
    the behaviors that call them, `RequestPreProcessorBehavior<,>` and `RequestPostProcessorBehavior<,>`, are only
    added when at least one processor was registered with one of the `Add...Processor` methods. That is how
    MediatR 12.5.0 behaves, and this fork keeps it. Either register the processors with those methods, or add the
    behaviors yourself:

    ```csharp
    services.AddTransient(typeof(IPipelineBehavior<,>), typeof(RequestPreProcessorBehavior<,>));
    services.AddTransient(typeof(IPipelineBehavior<,>), typeof(RequestPostProcessorBehavior<,>));
    ```

## Streams

Stream requests have a pipeline of their own, `IStreamPipelineBehavior<TRequest, TResponse>`; see [Streams](Streams.md#stream-behaviors).
