// What the mediator does when it is published with Native AOT: no JIT, and no code for a generic instantiation
// that the compiler has not seen. Every handler is registered one by one; nothing scans an assembly.
// The program prints one line per scenario and exits with the number of failures.
//
// The same program also runs with a JIT (dotnet run, or published with PublishTrimmed instead of PublishAot).
// A few scenarios have another right answer there, because a JIT can create code on demand; "aot" below picks it.
using System.Runtime.CompilerServices;
using ForkForward.Mediator;
using ForkForward.Mediator.NotificationPublishers;
using ForkForward.Mediator.Pipeline;
using Microsoft.Extensions.DependencyInjection;

var failures = 0;

void Report(string name, bool ok, string detail)
{
    if (!ok) failures++;
    Console.WriteLine($"{(ok ? "ok  " : "FAIL")} {name}{(detail.Length > 0 ? " -> " + detail : "")}");
}

// A scenario passes when the value it returns is the expected one.
async Task Expect(string name, string expected, Func<Task<string>> run)
{
    try
    {
        var actual = await run();
        Report(name, actual == expected, actual == expected ? actual : $"expected '{expected}', got '{actual}'");
    }
    catch (Exception e)
    {
        Report(name, false, $"{e.GetType().Name}: {e.Message.Split('\n')[0]}");
    }
}

// A scenario passes when it is refused with the expected exception; the text says why that is the right outcome.
async Task Refused<TException>(string name, string messagePart, Func<Task> run) where TException : Exception
{
    try
    {
        await run();
        Report(name, false, "was not refused");
    }
    catch (TException e) when (e.Message.Contains(messagePart))
    {
        Report(name, true, "refused: " + e.Message.Split('\n')[0][..Math.Min(90, e.Message.Split('\n')[0].Length)]);
    }
    catch (Exception e)
    {
        Report(name, false, $"{e.GetType().Name}: {e.Message.Split('\n')[0]}");
    }
}

(IServiceProvider Provider, Log Log) Build(Action<MediatorServiceConfiguration>? configure = null, Action<IServiceCollection>? register = null)
{
    var log = new Log();
    var services = new ServiceCollection();
    services.AddSingleton(log);
    register?.Invoke(services);
    services.AddMediator(cfg =>
    {
        cfg.AddRequestHandler<Ping, string, PingHandler>();
        cfg.AddRequestHandler<Add, int, AddHandler>();
        cfg.AddRequestHandler<Ring, RingHandler>();
        cfg.AddRequestHandler<Fragile, string, FragileHandler>();
        cfg.AddRequestHandler<FragileNumber, int, FragileNumberHandler>();
        cfg.AddRequestHandler<FragileRing, FragileRingHandler>();
        cfg.AddNotificationHandler<Pinged, FirstPingedHandler>();
        cfg.AddNotificationHandler<Pinged, SecondPingedHandler>();
        cfg.AddNotificationHandler<Failing, FailingFirst>();
        cfg.AddNotificationHandler<Failing, FailingSecond>();
        cfg.AddStreamRequestHandler<CountTo, int, CountToHandler>();
        configure?.Invoke(cfg);
    });
    return (services.BuildServiceProvider(), log);
}

IMediator Mediator(Action<MediatorServiceConfiguration>? configure = null) => Build(configure).Provider.GetRequiredService<IMediator>();

// False under Native AOT. Also false under dotnet run and dotnet test for a project with PublishAot: the SDK
// switches dynamic code off there, so that the application behaves in development as it will when published.
var aot = !RuntimeFeature.IsDynamicCodeSupported;
Console.WriteLine($".NET {Environment.Version}; dynamic code {(aot ? "not supported" : "supported")}; compiled {(RuntimeFeature.IsDynamicCodeCompiled ? "by a JIT" : "ahead of time")}");

// ---------------------------------------------------------------- requests
await Expect("Send, reference-type response", "Ping Pong", async () => await Mediator().Send(new Ping("Ping")));
await Expect("Send, value-type response", "5", async () => (await Mediator().Send(new Add(2, 3))).ToString());
await Expect("Send, no response", "handler", async () => { var (p, log) = Build(); await p.GetRequiredService<IMediator>().Send(new Ring()); return log.Text; });
await Expect("Send(object), with a response", "Hi Pong", async () => (await Mediator().Send((object) new Ping("Hi")))?.ToString() ?? "null");
await Expect("Send(object), no response, returns Unit", "Unit", async () => (await Mediator().Send((object) new Ring()))?.GetType().Name ?? "null");
await Expect("ISender and IPublisher are the mediator", "True", () =>
{
    var provider = Build().Provider;
    var mediator = provider.GetRequiredService<IMediator>();
    return Task.FromResult((mediator is Mediator && provider.GetRequiredService<ISender>() is Mediator && provider.GetRequiredService<IPublisher>() is Mediator).ToString());
});
// Under Native AOT the mediator refuses before it looks for a handler; with a JIT the container reports that there is none.
await Refused<InvalidOperationException>("a request that was never registered, sent with its type known only at run time", aot ? "was not registered" : "No service for type",
    async () => await Mediator().Send((object) new Unregistered()));
await Expect("a request without a response whose handler was put into the container directly", "handler", async () =>
{
    var (p, log) = Build(register: services => services.AddTransient<IRequestHandler<Direct>, DirectHandler>());
    await p.GetRequiredService<IMediator>().Send(new Direct());
    return log.Text;
});

// ---------------------------------------------------------------- notifications
await Expect("Publish, two handlers", "pinged 1 > pinged 2", async () => { var (p, log) = Build(); await p.GetRequiredService<IMediator>().Publish(new Pinged()); return log.Text; });
await Expect("Publish, a notification without handlers", "", async () => { var (p, log) = Build(); await p.GetRequiredService<IMediator>().Publish(new Lonely()); return log.Text; });
await Expect("Publish(object), registered notification", "pinged 1 > pinged 2", async () => { var (p, log) = Build(); await p.GetRequiredService<IMediator>().Publish((object) new Pinged()); return log.Text; });
if (aot)
{
    await Refused<InvalidOperationException>("Publish(object), a notification that was never registered", "was not registered",
        async () => await Mediator().Publish((object) new Lonely()));
}
else
{
    await Expect("Publish(object), a notification that was never registered", "", async () => { var (p, log) = Build(); await p.GetRequiredService<IMediator>().Publish((object) new Lonely()); return log.Text; });
}
await Expect("a notification handler that was put into the container directly", "direct listener", async () =>
{
    var (p, log) = Build(register: services => services.AddTransient<INotificationHandler<DirectNote>, DirectListener>());
    await p.GetRequiredService<IMediator>().Publish(new DirectNote());
    return log.Text;
});
await Expect("a mediator class of your own", "custom mediator > pinged 1 > pinged 2", async () =>
{
    var (p, log) = Build(cfg => cfg.MediatorImplementationType = typeof(LoggingMediator));
    await p.GetRequiredService<IMediator>().Publish(new Pinged());
    return log.Text;
});
await Expect("ForeachAwaitPublisher stops at the first failing handler", "InvalidOperationException; ", async () =>
{
    var (p, log) = Build();
    try { await p.GetRequiredService<IMediator>().Publish(new Failing()); return "no exception"; }
    catch (InvalidOperationException) { return "InvalidOperationException; " + log.Text; }
});
await Expect("TaskWhenAllPublisher runs every handler", "InvalidOperationException; failing 2", async () =>
{
    var (p, log) = Build(cfg => cfg.NotificationPublisher = new TaskWhenAllPublisher());
    try { await p.GetRequiredService<IMediator>().Publish(new Failing()); return "no exception"; }
    catch (InvalidOperationException) { return "InvalidOperationException; " + log.Text; }
});
await Expect("a publisher type created by the container", "custom publisher > pinged 1 > pinged 2", async () =>
{
    var (p, log) = Build(cfg => cfg.NotificationPublisherType = typeof(LoggingPublisher));
    await p.GetRequiredService<IMediator>().Publish(new Pinged());
    return log.Text;
});

// ---------------------------------------------------------------- streams
await Expect("CreateStream", "1,2,3", async () => { var items = new List<int>(); await foreach (var n in Mediator().CreateStream(new CountTo(3))) items.Add(n); return string.Join(",", items); });
await Expect("CreateStream(object)", "1,2,3", async () => { var items = new List<object?>(); await foreach (var n in Mediator().CreateStream((object) new CountTo(3))) items.Add(n); return string.Join(",", items); });
await Expect("CreateStream, cancelled after two items", "2 items, cancelled", async () =>
{
    using var cts = new CancellationTokenSource();
    var seen = 0;
    try { await foreach (var _ in Mediator().CreateStream(new CountTo(1000), cts.Token)) { if (++seen == 2) cts.Cancel(); } return $"{seen} items, not cancelled"; }
    catch (OperationCanceledException) { return $"{seen} items, cancelled"; }
});
await Expect("closed stream behavior", "2,4,6", async () =>
{
    var items = new List<int>();
    await foreach (var n in Mediator(cfg => cfg.AddStreamBehavior<IStreamPipelineBehavior<CountTo, int>, Doubling>()).CreateStream(new CountTo(3))) items.Add(n);
    return string.Join(",", items);
});

// ---------------------------------------------------------------- behaviors
await Expect("closed behaviors run in registration order, value-type response", "outer in > inner in > handler > inner out > outer out", async () =>
{
    var (p, log) = Build(cfg =>
    {
        cfg.AddBehavior<IPipelineBehavior<Add, int>, Outer<Add, int>>();
        cfg.AddBehavior<IPipelineBehavior<Add, int>, Inner<Add, int>>();
    });
    await p.GetRequiredService<IMediator>().Send(new Add(2, 3));
    return log.Text;
});
await Expect("closed behavior, request without a response", "outer in > handler > outer out", async () =>
{
    var (p, log) = Build(cfg => cfg.AddBehavior<IPipelineBehavior<Ring, Unit>, Outer<Ring, Unit>>());
    await p.GetRequiredService<IMediator>().Send(new Ring());
    return log.Text;
});
await Expect("open behavior, reference-type response", "outer in > handler > outer out", async () =>
{
    var (p, log) = Build(cfg => cfg.AddOpenBehavior(typeof(Outer<,>)));
    await p.GetRequiredService<IMediator>().Send(new Ping("Ping"));
    return log.Text;
});
// The container cannot close an open generic type over a value type under Native AOT, and says so.
// Documented on the Native AOT page: register such behaviors closed.
if (aot)
{
    await Refused<InvalidOperationException>("open behavior, value-type response", "ValueType",
        async () => await Mediator(cfg => cfg.AddOpenBehavior(typeof(Outer<,>))).Send(new Add(2, 3)));
    await Refused<InvalidOperationException>("open behavior, request without a response", "ValueType",
        async () => await Mediator(cfg => cfg.AddOpenBehavior(typeof(Outer<,>))).Send(new Ring()));
}
else
{
    await Expect("open behavior, value-type response", "outer in > handler > outer out", async () =>
    {
        var (p, log) = Build(cfg => cfg.AddOpenBehavior(typeof(Outer<,>)));
        await p.GetRequiredService<IMediator>().Send(new Add(2, 3));
        return log.Text;
    });
    await Expect("open behavior, request without a response", "outer in > handler > outer out", async () =>
    {
        var (p, log) = Build(cfg => cfg.AddOpenBehavior(typeof(Outer<,>)));
        await p.GetRequiredService<IMediator>().Send(new Ring());
        return log.Text;
    });
}

// ---------------------------------------------------------------- pre- and post-processors
await Expect("open pre- and post-processor, reference-type response", "pre > handler > post", async () =>
{
    var (p, log) = Build(cfg => { cfg.AddOpenRequestPreProcessor(typeof(Pre<>)); cfg.AddOpenRequestPostProcessor(typeof(Post<,>)); });
    await p.GetRequiredService<IMediator>().Send(new Ping("Ping"));
    return log.Text;
});
await Expect("open pre-processor, value-type response", "pre > handler", async () =>
{
    var (p, log) = Build(cfg => cfg.AddOpenRequestPreProcessor(typeof(Pre<>)));
    await p.GetRequiredService<IMediator>().Send(new Add(2, 3));
    return log.Text;
});
await Expect("open pre-processor, request without a response", "pre > handler", async () =>
{
    var (p, log) = Build(cfg => cfg.AddOpenRequestPreProcessor(typeof(Pre<>)));
    await p.GetRequiredService<IMediator>().Send(new Ring());
    return log.Text;
});
await Expect("closed pre- and post-processor, request without a response", "ring pre > handler > ring post", async () =>
{
    var (p, log) = Build(cfg => { cfg.AddRequestPreProcessor<RingPre>(); cfg.AddRequestPostProcessor<RingPost>(); });
    await p.GetRequiredService<IMediator>().Send(new Ring());
    return log.Text;
});
await Expect("processors around behaviors", "pre > outer in > handler > outer out > post", async () =>
{
    var (p, log) = Build(cfg =>
    {
        cfg.AddBehavior<IPipelineBehavior<Ping, string>, Outer<Ping, string>>();
        cfg.AddOpenRequestPreProcessor(typeof(Pre<>));
        cfg.AddOpenRequestPostProcessor(typeof(Post<,>));
    });
    await p.GetRequiredService<IMediator>().Send(new Ping("Ping"));
    return log.Text;
});

// ---------------------------------------------------------------- exceptions
void Exceptions(MediatorServiceConfiguration cfg)
{
    cfg.AddRequestExceptionHandler<Fragile, string, Exception, AnyExceptionHandler>();
    cfg.AddRequestExceptionHandler<Fragile, string, TimeoutException, TimeoutHandler>();
    cfg.AddRequestExceptionAction<Fragile, Exception, FragileAction>();
}
await Expect("exception handler returns a response", "fallback; timeout handler", async () =>
{
    var (p, log) = Build(Exceptions);
    return await p.GetRequiredService<IMediator>().Send(new Fragile(Handle: true)) + "; " + log.Text;
});
await Expect("unhandled exception: exact type first, then base type, then the action, then rethrown", "TimeoutException; timeout handler > exception handler > action", async () =>
{
    var (p, log) = Build(Exceptions);
    try { await p.GetRequiredService<IMediator>().Send(new Fragile(Handle: false)); return "no exception"; }
    catch (Exception e) { return e.GetType().Name + "; " + log.Text; }
});
await Expect("ApplyForAllExceptions: the action runs before the handlers", "fallback; action > timeout handler", async () =>
{
    var (p, log) = Build(cfg => { Exceptions(cfg); cfg.RequestExceptionActionProcessorStrategy = RequestExceptionActionProcessorStrategy.ApplyForAllExceptions; });
    return await p.GetRequiredService<IMediator>().Send(new Fragile(Handle: true)) + "; " + log.Text;
});
await Expect("exception handler, value-type response", "-1", async () =>
    (await Mediator(cfg => cfg.AddRequestExceptionHandler<FragileNumber, int, InvalidOperationException, NumberFallback>()).Send(new FragileNumber())).ToString());
await Expect("exception action, request without a response", "InvalidOperationException; ring action", async () =>
{
    var (p, log) = Build(cfg => cfg.AddRequestExceptionAction<FragileRing, InvalidOperationException, RingAction>());
    try { await p.GetRequiredService<IMediator>().Send(new FragileRing()); return "no exception"; }
    catch (Exception e) { return e.GetType().Name + "; " + log.Text; }
});
await Expect("exception handler does not run for another request", "5", async () => (await Mediator(Exceptions).Send(new Add(2, 3))).ToString());

// ---------------------------------------------------------------- lifetime
await Expect("scoped lifetime: one mediator per scope", "True, False", () =>
{
    var services = new ServiceCollection();
    services.AddSingleton<Log>();
    services.AddMediator(cfg => { cfg.AddRequestHandler<Ping, string, PingHandler>(); cfg.Lifetime = ServiceLifetime.Scoped; });
    using var provider = services.BuildServiceProvider();
    using var first = provider.CreateScope();
    using var second = provider.CreateScope();
    var a = first.ServiceProvider.GetRequiredService<IMediator>();
    return Task.FromResult($"{ReferenceEquals(a, first.ServiceProvider.GetRequiredService<ISender>())}, {ReferenceEquals(a, second.ServiceProvider.GetRequiredService<IMediator>())}");
});

Console.WriteLine(failures == 0 ? "ALL OK" : $"{failures} FAILED");
return failures;

public class Log
{
    private readonly List<string> _lines = new();
    public void Add(string line) { lock (_lines) _lines.Add(line); }
    public string Text { get { lock (_lines) return string.Join(" > ", _lines); } }
}

public record Ping(string Message) : IRequest<string>;
public class PingHandler(Log log) : IRequestHandler<Ping, string>
{
    public Task<string> Handle(Ping request, CancellationToken cancellationToken) { log.Add("handler"); return Task.FromResult($"{request.Message} Pong"); }
}

public record Add(int A, int B) : IRequest<int>;
public class AddHandler(Log log) : IRequestHandler<Add, int>
{
    public Task<int> Handle(Add request, CancellationToken cancellationToken) { log.Add("handler"); return Task.FromResult(request.A + request.B); }
}

public record Ring : IRequest;
public class RingHandler(Log log) : IRequestHandler<Ring>
{
    public Task Handle(Ring request, CancellationToken cancellationToken) { log.Add("handler"); return Task.CompletedTask; }
}

public record Unregistered : IRequest<string>;

public record Direct : IRequest;
public class DirectHandler(Log log) : IRequestHandler<Direct>
{
    public Task Handle(Direct request, CancellationToken cancellationToken) { log.Add("handler"); return Task.CompletedTask; }
}
public record DirectNote : INotification;
public class DirectListener(Log log) : INotificationHandler<DirectNote>
{
    public Task Handle(DirectNote notification, CancellationToken cancellationToken) { log.Add("direct listener"); return Task.CompletedTask; }
}
public class LoggingMediator(IServiceProvider serviceProvider, INotificationPublisher publisher, Log log) : Mediator(serviceProvider, publisher)
{
    protected override Task PublishCore(IEnumerable<NotificationHandlerExecutor> handlerExecutors, INotification notification, CancellationToken cancellationToken)
    {
        log.Add("custom mediator");
        return base.PublishCore(handlerExecutors, notification, cancellationToken);
    }
}

public record Pinged : INotification;
public class FirstPingedHandler(Log log) : INotificationHandler<Pinged>
{
    public Task Handle(Pinged notification, CancellationToken cancellationToken) { log.Add("pinged 1"); return Task.CompletedTask; }
}
public class SecondPingedHandler(Log log) : NotificationHandler<Pinged>
{
    protected override void Handle(Pinged notification) => log.Add("pinged 2");
}
public record Lonely : INotification;

public record Failing : INotification;
public class FailingFirst : INotificationHandler<Failing>
{
    public async Task Handle(Failing notification, CancellationToken cancellationToken) { await Task.Yield(); throw new InvalidOperationException("first"); }
}
public class FailingSecond(Log log) : INotificationHandler<Failing>
{
    public Task Handle(Failing notification, CancellationToken cancellationToken) { log.Add("failing 2"); return Task.CompletedTask; }
}
public class LoggingPublisher(Log log) : INotificationPublisher
{
    public async Task Publish(IEnumerable<NotificationHandlerExecutor> handlerExecutors, INotification notification, CancellationToken cancellationToken)
    {
        log.Add("custom publisher");
        foreach (var executor in handlerExecutors) await executor.HandlerCallback(notification, cancellationToken);
    }
}

public record CountTo(int To) : IStreamRequest<int>;
public class CountToHandler : IStreamRequestHandler<CountTo, int>
{
    public async IAsyncEnumerable<int> Handle(CountTo request, [EnumeratorCancellation] CancellationToken cancellationToken)
    {
        for (var n = 1; n <= request.To; n++)
        {
            cancellationToken.ThrowIfCancellationRequested();
            await Task.Yield();
            yield return n;
        }
    }
}
public class Doubling : IStreamPipelineBehavior<CountTo, int>
{
    public async IAsyncEnumerable<int> Handle(CountTo request, StreamHandlerDelegate<int> next, [EnumeratorCancellation] CancellationToken cancellationToken)
    {
        await foreach (var item in next().WithCancellation(cancellationToken)) yield return item * 2;
    }
}

public class Outer<TRequest, TResponse>(Log log) : IPipelineBehavior<TRequest, TResponse> where TRequest : notnull
{
    public async Task<TResponse> Handle(TRequest request, RequestHandlerDelegate<TResponse> next, CancellationToken cancellationToken)
    { log.Add("outer in"); var response = await next(cancellationToken); log.Add("outer out"); return response; }
}
public class Inner<TRequest, TResponse>(Log log) : IPipelineBehavior<TRequest, TResponse> where TRequest : notnull
{
    public async Task<TResponse> Handle(TRequest request, RequestHandlerDelegate<TResponse> next, CancellationToken cancellationToken)
    { log.Add("inner in"); var response = await next(cancellationToken); log.Add("inner out"); return response; }
}

public class Pre<TRequest>(Log log) : IRequestPreProcessor<TRequest> where TRequest : notnull
{
    public Task Process(TRequest request, CancellationToken cancellationToken) { log.Add("pre"); return Task.CompletedTask; }
}
public class Post<TRequest, TResponse>(Log log) : IRequestPostProcessor<TRequest, TResponse> where TRequest : notnull
{
    public Task Process(TRequest request, TResponse response, CancellationToken cancellationToken) { log.Add("post"); return Task.CompletedTask; }
}
public class RingPre(Log log) : IRequestPreProcessor<Ring>
{
    public Task Process(Ring request, CancellationToken cancellationToken) { log.Add("ring pre"); return Task.CompletedTask; }
}
public class RingPost(Log log) : IRequestPostProcessor<Ring, Unit>
{
    public Task Process(Ring request, Unit response, CancellationToken cancellationToken) { log.Add("ring post"); return Task.CompletedTask; }
}

public record Fragile(bool Handle) : IRequest<string>;
public class FragileHandler : IRequestHandler<Fragile, string>
{
    public Task<string> Handle(Fragile request, CancellationToken cancellationToken) => throw new TimeoutException("too slow");
}
public class TimeoutHandler(Log log) : IRequestExceptionHandler<Fragile, string, TimeoutException>
{
    public Task Handle(Fragile request, TimeoutException exception, RequestExceptionHandlerState<string> state, CancellationToken cancellationToken)
    { log.Add("timeout handler"); if (request.Handle) state.SetHandled("fallback"); return Task.CompletedTask; }
}
public class AnyExceptionHandler(Log log) : IRequestExceptionHandler<Fragile, string, Exception>
{
    public Task Handle(Fragile request, Exception exception, RequestExceptionHandlerState<string> state, CancellationToken cancellationToken)
    { log.Add("exception handler"); return Task.CompletedTask; }
}
public class FragileAction(Log log) : IRequestExceptionAction<Fragile, Exception>
{
    public Task Execute(Fragile request, Exception exception, CancellationToken cancellationToken) { log.Add("action"); return Task.CompletedTask; }
}

public record FragileNumber : IRequest<int>;
public class FragileNumberHandler : IRequestHandler<FragileNumber, int>
{
    public Task<int> Handle(FragileNumber request, CancellationToken cancellationToken) => throw new InvalidOperationException("no number");
}
public class NumberFallback : IRequestExceptionHandler<FragileNumber, int, InvalidOperationException>
{
    public Task Handle(FragileNumber request, InvalidOperationException exception, RequestExceptionHandlerState<int> state, CancellationToken cancellationToken)
    { state.SetHandled(-1); return Task.CompletedTask; }
}

public record FragileRing : IRequest;
public class FragileRingHandler : IRequestHandler<FragileRing>
{
    public Task Handle(FragileRing request, CancellationToken cancellationToken) => throw new InvalidOperationException("no ring");
}
public class RingAction(Log log) : IRequestExceptionAction<FragileRing, InvalidOperationException>
{
    public Task Execute(FragileRing request, InvalidOperationException exception, CancellationToken cancellationToken) { log.Add("ring action"); return Task.CompletedTask; }
}
