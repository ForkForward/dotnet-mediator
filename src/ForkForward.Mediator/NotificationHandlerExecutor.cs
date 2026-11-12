using System;
using System.Threading;
using System.Threading.Tasks;

namespace ForkForward.Mediator;

public record NotificationHandlerExecutor(object HandlerInstance, Func<INotification, CancellationToken, Task> HandlerCallback);