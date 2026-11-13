using ForkForward.Mediator.NotificationPublishers;

namespace ForkForward.Mediator;

using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Wrappers;

/// <summary>
/// Default mediator implementation relying on single- and multi instance delegates for resolving handlers.
/// </summary>
public class Mediator : IMediator
{
    private readonly IServiceProvider _serviceProvider;
    private readonly INotificationPublisher _publisher;

    /// <summary>
    /// Initializes a new instance of the <see cref="Mediator"/> class.
    /// </summary>
    /// <param name="serviceProvider">Service provider. Can be a scoped or root provider</param>
    public Mediator(IServiceProvider serviceProvider) 
        : this(serviceProvider, new ForeachAwaitPublisher()) { }

    /// <summary>
    /// Initializes a new instance of the <see cref="Mediator"/> class.
    /// </summary>
    /// <param name="serviceProvider">Service provider. Can be a scoped or root provider</param>
    /// <param name="publisher">Notification publisher. Defaults to <see cref="ForeachAwaitPublisher"/>.</param>
    public Mediator(IServiceProvider serviceProvider, INotificationPublisher publisher)
    {
        _serviceProvider = serviceProvider;
        _publisher = publisher;
    }

    public Task<TResponse> Send<TResponse>(IRequest<TResponse> request, CancellationToken cancellationToken = default)
    {
        if (request == null)
        {
            throw new ArgumentNullException(nameof(request));
        }

        var handler = (RequestHandlerWrapper<TResponse>) WrapperCache.GetRequest(request.GetType(), typeof(TResponse));

        return handler.Handle(request, _serviceProvider, cancellationToken);
    }

    public Task Send<TRequest>(TRequest request, CancellationToken cancellationToken = default)
        where TRequest : IRequest
    {
        if (request == null)
        {
            throw new ArgumentNullException(nameof(request));
        }

        // When the request is exactly a TRequest, its wrapper is known at compile time: no lookup, and nothing
        // for Native AOT to create at run time. A request of a derived type goes through the cache.
        var handler = request.GetType() == typeof(TRequest)
            ? WrapperCache.VoidRequest<TRequest>.Wrapper
            : (RequestHandlerWrapper) WrapperCache.GetVoidRequest(request.GetType());

        return handler.Handle(request, _serviceProvider, cancellationToken);
    }

    public Task<object?> Send(object request, CancellationToken cancellationToken = default)
    {
        if (request == null)
        {
            throw new ArgumentNullException(nameof(request));
        }

        var handler = WrapperCache.GetRequestOfUnknownShape(request.GetType());

        // call via dynamic dispatch to avoid calling through reflection for performance reasons
        return handler.Handle(request, _serviceProvider, cancellationToken);
    }

    public Task Publish<TNotification>(TNotification notification, CancellationToken cancellationToken = default)
        where TNotification : INotification
    {
        if (notification == null)
        {
            throw new ArgumentNullException(nameof(notification));
        }

        // When the notification is exactly a TNotification, its wrapper is known at compile time: no lookup, and
        // nothing for Native AOT to create at run time. A notification of a derived type goes through the cache.
        var handler = notification.GetType() == typeof(TNotification)
            ? WrapperCache.Notification<TNotification>.Wrapper
            : WrapperCache.GetNotification(notification.GetType());

        return handler.Handle(notification, _serviceProvider, PublishCore, cancellationToken);
    }

    public Task Publish(object notification, CancellationToken cancellationToken = default) =>
        notification switch
        {
            null => throw new ArgumentNullException(nameof(notification)),
            INotification instance => PublishNotification(instance, cancellationToken),
            _ => throw new ArgumentException($"{nameof(notification)} does not implement ${nameof(INotification)}")
        };

    /// <summary>
    /// Override in a derived class to control how the tasks are awaited. By default the implementation calls the <see cref="INotificationPublisher"/>.
    /// </summary>
    /// <param name="handlerExecutors">Enumerable of tasks representing invoking each notification handler</param>
    /// <param name="notification">The notification being published</param>
    /// <param name="cancellationToken">The cancellation token</param>
    /// <returns>A task representing invoking all handlers</returns>
    protected virtual Task PublishCore(IEnumerable<NotificationHandlerExecutor> handlerExecutors, INotification notification, CancellationToken cancellationToken) 
        => _publisher.Publish(handlerExecutors, notification, cancellationToken);

    private Task PublishNotification(INotification notification, CancellationToken cancellationToken = default)
    {
        var handler = WrapperCache.GetNotification(notification.GetType());

        return handler.Handle(notification, _serviceProvider, PublishCore, cancellationToken);
    }


    public IAsyncEnumerable<TResponse> CreateStream<TResponse>(IStreamRequest<TResponse> request, CancellationToken cancellationToken = default)
    {
        if (request == null)
        {
            throw new ArgumentNullException(nameof(request));
        }

        var streamHandler = (StreamRequestHandlerWrapper<TResponse>) WrapperCache.GetStreamRequest(request.GetType(), typeof(TResponse));

        var items = streamHandler.Handle(request, _serviceProvider, cancellationToken);

        return items;
    }


    public IAsyncEnumerable<object?> CreateStream(object request, CancellationToken cancellationToken = default)
    {
        if (request == null)
        {
            throw new ArgumentNullException(nameof(request));
        }

        var handler = WrapperCache.GetStreamRequestOfUnknownShape(request.GetType());

        var items = handler.Handle(request, _serviceProvider, cancellationToken);

        return items;
    }
}
