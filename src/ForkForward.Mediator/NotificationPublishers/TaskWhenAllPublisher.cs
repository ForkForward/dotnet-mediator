using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace ForkForward.Mediator.NotificationPublishers;

/// <summary>
/// Uses Task.WhenAll with the list of Handler tasks:
/// <code>
/// var tasks = handlers
///                .Select(handler => handler.Handle(notification, cancellationToken))
///                .ToList();
/// 
/// return Task.WhenAll(tasks);
/// </code>
/// </summary>
public class TaskWhenAllPublisher : INotificationPublisher
{
    public Task Publish(IEnumerable<NotificationHandlerExecutor> handlerExecutors, INotification notification, CancellationToken cancellationToken)
    {
        // The mediator passes an array. The handlers start in the same order as below, and one that throws
        // before its first await still ends the loop, as it ends ToArray().
        if (handlerExecutors is NotificationHandlerExecutor[] array)
        {
            var started = new Task[array.Length];
            for (var index = 0; index < array.Length; index++)
            {
                started[index] = array[index].HandlerCallback(notification, cancellationToken);
            }

            return Task.WhenAll(started);
        }

        var tasks = handlerExecutors
            .Select(handler => handler.HandlerCallback(notification, cancellationToken))
            .ToArray();

        return Task.WhenAll(tasks);
    }
}