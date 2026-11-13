using System;
using Microsoft.Extensions.DependencyInjection;

namespace ForkForward.Mediator.Wrappers;

using System.Collections.Generic;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Threading.Tasks;

internal abstract class StreamRequestHandlerBase
{
    public abstract IAsyncEnumerable<object?> Handle(object request, IServiceProvider serviceProvider, CancellationToken cancellationToken);
}

internal abstract class StreamRequestHandlerWrapper<TResponse> : StreamRequestHandlerBase
{
    public abstract IAsyncEnumerable<TResponse> Handle(
        IStreamRequest<TResponse> request, 
        IServiceProvider serviceProvider,
        CancellationToken cancellationToken);
}

internal class StreamRequestHandlerWrapperImpl<TRequest, TResponse> 
    : StreamRequestHandlerWrapper<TResponse>
    where TRequest : IStreamRequest<TResponse>
{
    public override async IAsyncEnumerable<object?> Handle(object request, IServiceProvider serviceProvider, [EnumeratorCancellation] CancellationToken cancellationToken)
    {
        await foreach (var item in Handle((IStreamRequest<TResponse>) request, serviceProvider, cancellationToken))
        {
            yield return item;
        }
    }

    public override async IAsyncEnumerable<TResponse> Handle(IStreamRequest<TResponse> request, 
        IServiceProvider serviceProvider, 
        [EnumeratorCancellation] CancellationToken cancellationToken)
    {
        IAsyncEnumerable<TResponse> Handler() => serviceProvider
            .GetRequiredService<IStreamRequestHandler<TRequest, TResponse>>()
            .Handle((TRequest) request, cancellationToken);

        // A loop and not Reverse().Aggregate(); see RequestHandlerWrapperImpl<TRequest, TResponse>.
        StreamHandlerDelegate<TResponse> next = Handler;
        var registered = serviceProvider.GetServices<IStreamPipelineBehavior<TRequest, TResponse>>();
        var behaviors = registered as IStreamPipelineBehavior<TRequest, TResponse>[] ?? registered.ToArray();
        for (var index = behaviors.Length - 1; index >= 0; index--)
        {
            var behavior = behaviors[index];
            var inner = next;
            next = () => behavior.Handle(
                (TRequest) request,
                () => NextWrapper(inner(), cancellationToken),
                cancellationToken
            );
        }

        var items = next();

        await foreach ( var item in items.WithCancellation(cancellationToken) )
        {
            yield return item;
        }
    }


    private static async IAsyncEnumerable<T> NextWrapper<T>(
        IAsyncEnumerable<T> items,
        [EnumeratorCancellation] CancellationToken cancellationToken)
    {
        var cancellable = items
            .WithCancellation(cancellationToken)
            .ConfigureAwait(false);
        await foreach (var item in cancellable)
        {
            yield return item;
        }
    }

}