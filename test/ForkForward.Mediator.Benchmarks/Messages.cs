using System.Collections.Generic;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Threading.Tasks;

namespace Benchmarks;

/// <summary>A request without a response.</summary>
public class Ping : IRequest
{
    public string Message { get; set; }
}

public class PingHandler : IRequestHandler<Ping>
{
    public Task Handle(Ping request, CancellationToken cancellationToken) => Task.CompletedTask;
}

/// <summary>A request with a response.</summary>
public class Echo : IRequest<string>
{
    public string Message { get; set; }
}

public class EchoHandler : IRequestHandler<Echo, string>
{
    public Task<string> Handle(Echo request, CancellationToken cancellationToken) => Task.FromResult(request.Message);
}

/// <summary>A notification with one handler.</summary>
public class Pinged : INotification
{
}

public class PingedHandler : INotificationHandler<Pinged>
{
    public Task Handle(Pinged notification, CancellationToken cancellationToken) => Task.CompletedTask;
}

/// <summary>A notification with three handlers.</summary>
public class Broadcast : INotification
{
}

public class FirstBroadcastHandler : INotificationHandler<Broadcast>
{
    public Task Handle(Broadcast notification, CancellationToken cancellationToken) => Task.CompletedTask;
}

public class SecondBroadcastHandler : INotificationHandler<Broadcast>
{
    public Task Handle(Broadcast notification, CancellationToken cancellationToken) => Task.CompletedTask;
}

public class ThirdBroadcastHandler : INotificationHandler<Broadcast>
{
    public Task Handle(Broadcast notification, CancellationToken cancellationToken) => Task.CompletedTask;
}

/// <summary>A stream request: the numbers from 1 to <see cref="To"/>.</summary>
public class Count : IStreamRequest<int>
{
    public int To { get; set; }
}

public class CountHandler : IStreamRequestHandler<Count, int>
{
    public async IAsyncEnumerable<int> Handle(Count request, [EnumeratorCancellation] CancellationToken cancellationToken)
    {
        await Task.CompletedTask;
        for (var number = 1; number <= request.To; number++)
            yield return number;
    }
}
