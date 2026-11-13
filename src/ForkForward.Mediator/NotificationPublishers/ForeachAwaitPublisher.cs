using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace ForkForward.Mediator.NotificationPublishers;

/// <summary>
/// Awaits each notification handler in a single foreach loop:
/// <code>
/// foreach (var handler in handlers) {
///     await handler(notification, cancellationToken);
/// }
/// </code>
/// </summary>
public class ForeachAwaitPublisher : INotificationPublisher
{
    public async Task Publish(IEnumerable<NotificationHandlerExecutor> handlerExecutors, INotification notification, CancellationToken cancellationToken)
    {
        // The mediator passes an array. Walked as an array, it needs no enumerator object.
        if (handlerExecutors is NotificationHandlerExecutor[] array)
        {
            foreach (var handler in array)
            {
                await handler.HandlerCallback(notification, cancellationToken).ConfigureAwait(false);
            }

            return;
        }

        foreach (var handler in handlerExecutors)
        {
            await handler.HandlerCallback(notification, cancellationToken).ConfigureAwait(false);
        }
    }
}