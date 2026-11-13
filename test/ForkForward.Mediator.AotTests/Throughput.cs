// How fast the calls are once everything is warm. Program.cs checks that the mediator works without a JIT; this
// file measures what a call costs there, so that it can be compared with the same program run by a JIT.
// It starts with the argument --throughput. AotThroughput.ps1 publishes the program both ways, runs the two in
// turns on one machine and prints the comparison.
//
// It is a measurement and not a test: it fails only when a call fails. The calls are the ones in
// test/ForkForward.Mediator.Benchmarks, with every handler registered one by one.
using System.Diagnostics;
using System.Globalization;
using System.Runtime.CompilerServices;
using ForkForward.Mediator;
using ForkForward.Mediator.Pipeline;
using Microsoft.Extensions.DependencyInjection;

namespace Throughput;

public static class Measurement
{
    // A JIT compiles a method quickly at first and again, optimised with what it has observed, once the method
    // has run for a while; the container compiles the way it creates a service in the background as well.
    // A second of calls is enough for all of that. A program compiled ahead of time gets the same second.
    private const int WarmupMilliseconds = 1000;
    private const int RoundMilliseconds = 200;
    private const int Rounds = 5;

    // Keeps results alive, so that no compiler can drop the work that produced them.
    private static long _sink;

    public static async Task<int> Run()
    {
        Console.WriteLine($".NET {Environment.Version}; dynamic code {(RuntimeFeature.IsDynamicCodeSupported ? "supported" : "not supported")}; compiled {(RuntimeFeature.IsDynamicCodeCompiled ? "by a JIT" : "ahead of time")}");

        var plain = Plain().GetRequiredService<IMediator>();
        var pipeline = WithPipeline().GetRequiredService<IMediator>();
        var container = Container();

        var signal = new Signal();
        var echo = new Echo("Hello World");
        object echoAsObject = new Echo("Hello World");
        var noted = new Noted();
        var broadcast = new Broadcast();
        var count = new Count(10);

        await Measure("Send", async calls =>
        {
            for (var call = 0; call < calls; call++) await plain.Send(signal);
        });
        await Measure("SendWithResponse", async calls =>
        {
            for (var call = 0; call < calls; call++) _sink += (await plain.Send(echo)).Length;
        });
        // The overload that takes the request as an object and finds its type at run time.
        await Measure("SendAsObject", async calls =>
        {
            for (var call = 0; call < calls; call++) _sink += await plain.Send(echoAsObject) is string text ? text.Length : 0;
        });
        // A behavior, a pre-processor and a post-processor around the handler.
        await Measure("SendThroughPipeline", async calls =>
        {
            for (var call = 0; call < calls; call++) _sink += (await pipeline.Send(echo)).Length;
        });
        await Measure("Publish", async calls =>
        {
            for (var call = 0; call < calls; call++) await plain.Publish(noted);
        });
        await Measure("PublishToThreeHandlers", async calls =>
        {
            for (var call = 0; call < calls; call++) await plain.Publish(broadcast);
        });
        // A stream of ten items, read to the end.
        await Measure("Stream", async calls =>
        {
            for (var call = 0; call < calls; call++)
                await foreach (var number in plain.CreateStream(count))
                    _sink += number;
        });
        // No mediator: the handler taken from the container and called. The mediator does this for every request,
        // so this row says how much of a difference between the two programs is the container's.
        await Measure("Reference: resolve the handler and call it", async calls =>
        {
            for (var call = 0; call < calls; call++)
                _sink += (await container.GetRequiredService<IRequestHandler<Echo, string>>().Handle(echo, CancellationToken.None)).Length;
        });

        return 0;
    }

    /// <summary>
    /// Prints THROUGHPUT|name|median ns per call|fastest ns per call|bytes allocated per call.
    /// </summary>
    private static async Task Measure(string name, Func<int, Task> loop)
    {
        var warmup = Stopwatch.StartNew();
        while (warmup.ElapsedMilliseconds < WarmupMilliseconds)
            await loop(500);

        // What a call costs now says how many of them fit into a round.
        const int sample = 20_000;
        var started = Stopwatch.GetTimestamp();
        await loop(sample);
        var perCall = Stopwatch.GetElapsedTime(started).TotalMilliseconds / sample;
        var calls = (int) Math.Clamp(RoundMilliseconds / perCall, 1_000, 50_000_000);

        var times = new double[Rounds];
        var allocated = double.MaxValue;
        for (var round = 0; round < Rounds; round++)
        {
            GC.Collect();
            var before = GC.GetTotalAllocatedBytes(precise: true);
            started = Stopwatch.GetTimestamp();
            await loop(calls);
            var elapsed = Stopwatch.GetElapsedTime(started);
            var after = GC.GetTotalAllocatedBytes(precise: true);

            times[round] = elapsed.TotalNanoseconds / calls;
            allocated = Math.Min(allocated, (double) (after - before) / calls);
        }

        Array.Sort(times);
        Console.WriteLine(string.Create(CultureInfo.InvariantCulture,
            $"THROUGHPUT|{name}|{times[Rounds / 2]:F1}|{times[0]:F1}|{allocated:F0}"));
    }

    private static void Handlers(MediatorServiceConfiguration cfg)
    {
        cfg.AddRequestHandler<Signal, SignalHandler>();
        cfg.AddRequestHandler<Echo, string, EchoHandler>();
        cfg.AddNotificationHandler<Noted, NotedHandler>();
        cfg.AddNotificationHandler<Broadcast, FirstBroadcastHandler>();
        cfg.AddNotificationHandler<Broadcast, SecondBroadcastHandler>();
        cfg.AddNotificationHandler<Broadcast, ThirdBroadcastHandler>();
        cfg.AddStreamRequestHandler<Count, int, CountHandler>();
    }

    private static IServiceProvider Plain()
    {
        var services = new ServiceCollection();
        services.AddMediator(Handlers);
        return services.BuildServiceProvider();
    }

    private static IServiceProvider WithPipeline()
    {
        var services = new ServiceCollection();
        services.AddMediator(cfg =>
        {
            Handlers(cfg);
            cfg.AddBehavior<IPipelineBehavior<Echo, string>, PassThrough>();
            cfg.AddRequestPreProcessor<EchoPreProcessor>();
            cfg.AddRequestPostProcessor<EchoPostProcessor>();
        });
        return services.BuildServiceProvider();
    }

    private static IServiceProvider Container()
    {
        var services = new ServiceCollection();
        services.AddTransient<IRequestHandler<Echo, string>, EchoHandler>();
        return services.BuildServiceProvider();
    }
}

/// <summary>A request without a response.</summary>
public sealed class Signal : IRequest;

public sealed class SignalHandler : IRequestHandler<Signal>
{
    public Task Handle(Signal request, CancellationToken cancellationToken) => Task.CompletedTask;
}

/// <summary>A request with a response.</summary>
public sealed record Echo(string Message) : IRequest<string>;

public sealed class EchoHandler : IRequestHandler<Echo, string>
{
    public Task<string> Handle(Echo request, CancellationToken cancellationToken) => Task.FromResult(request.Message);
}

public sealed class PassThrough : IPipelineBehavior<Echo, string>
{
    public Task<string> Handle(Echo request, RequestHandlerDelegate<string> next, CancellationToken cancellationToken) => next(cancellationToken);
}

public sealed class EchoPreProcessor : IRequestPreProcessor<Echo>
{
    public Task Process(Echo request, CancellationToken cancellationToken) => Task.CompletedTask;
}

public sealed class EchoPostProcessor : IRequestPostProcessor<Echo, string>
{
    public Task Process(Echo request, string response, CancellationToken cancellationToken) => Task.CompletedTask;
}

/// <summary>A notification with one handler.</summary>
public sealed class Noted : INotification;

public sealed class NotedHandler : INotificationHandler<Noted>
{
    public Task Handle(Noted notification, CancellationToken cancellationToken) => Task.CompletedTask;
}

/// <summary>A notification with three handlers.</summary>
public sealed class Broadcast : INotification;

public sealed class FirstBroadcastHandler : INotificationHandler<Broadcast>
{
    public Task Handle(Broadcast notification, CancellationToken cancellationToken) => Task.CompletedTask;
}

public sealed class SecondBroadcastHandler : INotificationHandler<Broadcast>
{
    public Task Handle(Broadcast notification, CancellationToken cancellationToken) => Task.CompletedTask;
}

public sealed class ThirdBroadcastHandler : INotificationHandler<Broadcast>
{
    public Task Handle(Broadcast notification, CancellationToken cancellationToken) => Task.CompletedTask;
}

/// <summary>A stream request: the numbers from 1 to <see cref="To"/>.</summary>
public sealed record Count(int To) : IStreamRequest<int>;

public sealed class CountHandler : IStreamRequestHandler<Count, int>
{
    public async IAsyncEnumerable<int> Handle(Count request, [EnumeratorCancellation] CancellationToken cancellationToken)
    {
        await Task.CompletedTask;
        for (var number = 1; number <= request.To; number++)
            yield return number;
    }
}
