# Streams

A stream request is answered with a sequence of items that arrive over time, as an `IAsyncEnumerable<T>`. Use it
when a result is large or produced bit by bit: rows from a database, lines of a file, events from a feed.

## Request and handler

```csharp
using System.Runtime.CompilerServices;
using ForkForward.Mediator;

public record GetOrderLines(int OrderId) : IStreamRequest<OrderLine>;

public class GetOrderLinesHandler(OrderStore store) : IStreamRequestHandler<GetOrderLines, OrderLine>
{
    public async IAsyncEnumerable<OrderLine> Handle(GetOrderLines request,
        [EnumeratorCancellation] CancellationToken cancellationToken)
    {
        await foreach (var line in store.Lines(request.OrderId).WithCancellation(cancellationToken))
            yield return line;
    }
}
```

## Read the stream

```csharp
await foreach (var line in sender.CreateStream(new GetOrderLines(42), cancellationToken))
{
    Console.WriteLine(line);
}
```

`CreateStream(object)` does the same for an object whose type is known only at run time and returns
`IAsyncEnumerable<object?>`.

## Cancellation

The token given to `CreateStream`, or to `WithCancellation` on the stream it returns, is passed to the handler's
`CancellationToken` parameter. Cancelling it stops the handler at its next check and ends the `await foreach` with
an `OperationCanceledException`.

Mark that parameter with `[EnumeratorCancellation]`, as above. The C# compiler asks for it on every `async`
iterator that takes a token (warning CS8425), and it makes the handler honour `WithCancellation` when it is
enumerated directly, for example in a test.

## Stream behaviors

Stream requests have a pipeline of their own. A stream behavior implements `IStreamPipelineBehavior<TRequest, TResponse>`
and returns the items of `next()`, changed, filtered or counted as it likes:

```csharp
public class StreamLogging<TRequest, TResponse>(ILogger<StreamLogging<TRequest, TResponse>> logger)
    : IStreamPipelineBehavior<TRequest, TResponse> where TRequest : notnull
{
    public async IAsyncEnumerable<TResponse> Handle(TRequest request, StreamHandlerDelegate<TResponse> next,
        [EnumeratorCancellation] CancellationToken cancellationToken)
    {
        var count = 0;
        await foreach (var item in next().WithCancellation(cancellationToken))
        {
            count++;
            yield return item;
        }
        logger.LogInformation("{Request} returned {Count} items", typeof(TRequest).Name, count);
    }
}
```

```csharp
services.AddMediator(cfg =>
{
    cfg.RegisterServicesFromAssemblyContaining<Program>();
    cfg.AddOpenStreamBehavior(typeof(StreamLogging<,>));
});
```

`AddStreamBehavior` registers a closed stream behavior. The [request pipeline](Pipeline-behaviors.md), its
pre- and post-processors and the [exception handlers](Exception-handling.md) do not apply to streams.
