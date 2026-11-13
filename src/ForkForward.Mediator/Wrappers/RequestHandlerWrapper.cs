using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection;

namespace ForkForward.Mediator.Wrappers;

public abstract class RequestHandlerBase
{
    public abstract Task<object?> Handle(object request, IServiceProvider serviceProvider,
        CancellationToken cancellationToken);
}

public abstract class RequestHandlerWrapper<TResponse> : RequestHandlerBase
{
    public abstract Task<TResponse> Handle(IRequest<TResponse> request, IServiceProvider serviceProvider,
        CancellationToken cancellationToken);
}

public abstract class RequestHandlerWrapper : RequestHandlerBase
{
    public abstract Task<Unit> Handle(IRequest request, IServiceProvider serviceProvider,
        CancellationToken cancellationToken);
}

public class RequestHandlerWrapperImpl<TRequest, TResponse> : RequestHandlerWrapper<TResponse>
    where TRequest : IRequest<TResponse>
{
    public override async Task<object?> Handle(object request, IServiceProvider serviceProvider,
        CancellationToken cancellationToken) =>
        await Handle((IRequest<TResponse>) request, serviceProvider, cancellationToken).ConfigureAwait(false);

    public override Task<TResponse> Handle(IRequest<TResponse> request, IServiceProvider serviceProvider,
        CancellationToken cancellationToken)
    {
        Task<TResponse> Handler(CancellationToken t = default) => serviceProvider.GetRequiredService<IRequestHandler<TRequest, TResponse>>()
            .Handle((TRequest) request, t == default ? cancellationToken : t);

        // The behaviors wrap the handler, the one registered first outermost. This is a loop and not
        // Reverse().Aggregate(): LINQ takes the step as a delegate, which is created on every call. A JIT can
        // remove that allocation once it has inlined everything; a program compiled ahead of time cannot.
        RequestHandlerDelegate<TResponse> next = Handler;
        var registered = serviceProvider.GetServices<IPipelineBehavior<TRequest, TResponse>>();
        var behaviors = registered as IPipelineBehavior<TRequest, TResponse>[] ?? registered.ToArray();
        for (var index = behaviors.Length - 1; index >= 0; index--)
        {
            var behavior = behaviors[index];
            var inner = next;
            next = t => behavior.Handle((TRequest) request, inner, t == default ? cancellationToken : t);
        }

        return next();
    }
}

public class RequestHandlerWrapperImpl<TRequest> : RequestHandlerWrapper
    where TRequest : IRequest
{
    public override async Task<object?> Handle(object request, IServiceProvider serviceProvider,
        CancellationToken cancellationToken) =>
        await Handle((IRequest) request, serviceProvider, cancellationToken).ConfigureAwait(false);

    public override Task<Unit> Handle(IRequest request, IServiceProvider serviceProvider,
        CancellationToken cancellationToken)
    {
        async Task<Unit> Handler(CancellationToken t = default)
        {
            await serviceProvider.GetRequiredService<IRequestHandler<TRequest>>()
                .Handle((TRequest) request, t == default ? cancellationToken : t);

            return Unit.Value;
        }

        // A loop and not Reverse().Aggregate(); see RequestHandlerWrapperImpl<TRequest, TResponse>.
        RequestHandlerDelegate<Unit> next = Handler;
        var registered = serviceProvider.GetServices<IPipelineBehavior<TRequest, Unit>>();
        var behaviors = registered as IPipelineBehavior<TRequest, Unit>[] ?? registered.ToArray();
        for (var index = behaviors.Length - 1; index >= 0; index--)
        {
            var behavior = behaviors[index];
            var inner = next;
            next = t => behavior.Handle((TRequest) request, inner, t == default ? cancellationToken : t);
        }

        return next();
    }
}
