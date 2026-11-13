namespace ForkForward.Mediator.Wrappers;

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection;

public abstract class NotificationHandlerWrapper
{
    public abstract Task Handle(INotification notification, IServiceProvider serviceFactory,
        Func<IEnumerable<NotificationHandlerExecutor>, INotification, CancellationToken, Task> publish,
        CancellationToken cancellationToken);
}

public class NotificationHandlerWrapperImpl<TNotification> : NotificationHandlerWrapper
    where TNotification : INotification
{
    public override Task Handle(INotification notification, IServiceProvider serviceFactory,
        Func<IEnumerable<NotificationHandlerExecutor>, INotification, CancellationToken, Task> publish,
        CancellationToken cancellationToken)
    {
        // An array and not Select(): what a LINQ iterator costs depends on which LINQ the application has.
        // An application published with Native AOT gets the one built for size, which allocates more per call.
        // The publishers that come with the library walk an array without an enumerator object.
        var registered = serviceFactory.GetServices<INotificationHandler<TNotification>>();
        var handlers = registered as INotificationHandler<TNotification>[] ?? registered.ToArray();
        if (handlers.Length == 0)
        {
            return publish(Array.Empty<NotificationHandlerExecutor>(), notification, cancellationToken);
        }

        var executors = new NotificationHandlerExecutor[handlers.Length];
        for (var index = 0; index < handlers.Length; index++)
        {
            var handler = handlers[index];
            executors[index] = new NotificationHandlerExecutor(handler, (theNotification, theToken) => handler.Handle((TNotification) theNotification, theToken));
        }

        return publish(executors, notification, cancellationToken);
    }
}
