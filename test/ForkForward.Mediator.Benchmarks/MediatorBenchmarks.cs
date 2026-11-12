using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using BenchmarkDotNet.Attributes;
using Microsoft.Extensions.DependencyInjection;

namespace Benchmarks;

/// <summary>
/// The cost of one call once the container is built: the hot path of every application.
/// </summary>
public class MediatorBenchmarks
{
    IMediator _plain;
    IMediator _pipeline;
    readonly Ping _ping = new Ping { Message = "Hello World" };
    readonly Echo _echo = new Echo { Message = "Hello World" };
    readonly object _echoAsObject = new Echo { Message = "Hello World" };
    readonly Pinged _pinged = new Pinged();
    readonly Broadcast _broadcast = new Broadcast();
    readonly Count _count = new Count { To = 10 };

    [GlobalSetup]
    public void Setup()
    {
        _plain = Library.Plain().GetRequiredService<IMediator>();
        _pipeline = Library.WithPipeline().GetRequiredService<IMediator>();
        // The first call for each message type builds and caches its handler wrapper.
        // Do that now, so that the measurement does not include it.
        Send().GetAwaiter().GetResult();
        SendWithResponse().GetAwaiter().GetResult();
        SendAsObject().GetAwaiter().GetResult();
        SendThroughPipeline().GetAwaiter().GetResult();
        Publish().GetAwaiter().GetResult();
        PublishToThreeHandlers().GetAwaiter().GetResult();
        Stream().GetAwaiter().GetResult();
    }

    [Benchmark]
    public Task Send() => _plain.Send(_ping);

    [Benchmark]
    public Task<string> SendWithResponse() => _plain.Send(_echo);

    /// <summary>The overload that takes the request as <see cref="object"/> and finds its type at run time.</summary>
    [Benchmark]
    public Task<object> SendAsObject() => _plain.Send(_echoAsObject);

    /// <summary>A behavior, a pre-processor and a post-processor around the handler.</summary>
    [Benchmark]
    public Task<string> SendThroughPipeline() => _pipeline.Send(_echo);

    [Benchmark]
    public Task Publish() => _plain.Publish(_pinged);

    [Benchmark]
    public Task PublishToThreeHandlers() => _plain.Publish(_broadcast);

    /// <summary>A stream of ten items, read to the end.</summary>
    [Benchmark]
    public async Task<int> Stream()
    {
        var sum = 0;
        await foreach (var number in _plain.CreateStream(_count))
            sum += number;
        return sum;
    }
}

/// <summary>
/// What an application pays once: registering the handlers of an assembly, building the container and
/// resolving the mediator. Benchmark.ps1 checks these for allocations only; their timings vary too much
/// from run to run to be a gate.
/// </summary>
public class StartupBenchmarks
{
    [Benchmark]
    public IMediator Register()
    {
        var services = new ServiceCollection();
        services.AddSingleton(TextWriter.Null);
        services.AddLibrary(cfg =>
        {
            cfg.RegisterServicesFromAssemblyContaining(typeof(Ping));
            cfg.AddOpenBehavior(typeof(GenericPipelineBehavior<,>));
        });
        using var provider = services.BuildServiceProvider();
        return provider.GetRequiredService<IMediator>();
    }
}

/// <summary>
/// The same shape of work written by hand: look up by type, resolve from the container, call. They do not
/// touch the library, so baseline and current must measure the same. Benchmark.ps1 uses them to tell how
/// noisy the machine was during a run.
/// Only work that takes about as long as the library calls belongs here: something that takes a few
/// nanoseconds differs by several percent between two builds for reasons unrelated to noise.
/// </summary>
public class ReferenceBenchmarks
{
    IServiceProvider _provider;
    readonly ConcurrentDictionary<Type, Func<IServiceProvider, object, CancellationToken, Task<string>>> _calls = new();
    readonly PlainRequest _request = new PlainRequest { Message = "Hello World" };
    readonly PlainNote _note = new PlainNote();

    [GlobalSetup]
    public void Setup()
    {
        var services = new ServiceCollection();
        services.AddTransient<PlainHandler>();
        services.AddTransient<IPlainListener, FirstPlainListener>();
        services.AddTransient<IPlainListener, SecondPlainListener>();
        services.AddTransient<IPlainListener, ThirdPlainListener>();
        _provider = services.BuildServiceProvider();
        _calls[typeof(PlainRequest)] = (provider, request, cancellationToken) =>
            provider.GetRequiredService<PlainHandler>().Handle((PlainRequest) request, cancellationToken);
        ResolveAndCall().GetAwaiter().GetResult();
        ResolveManyAndCall().GetAwaiter().GetResult();
    }

    // One round of this work takes 15 to 25 ns, a quarter of a library call, and at that size two builds
    // of the same code differ by more than the noise limit. Repeated, it takes about as long as a library call.
    const int Repeat = 5;

    [Benchmark]
    public async Task<string> ResolveAndCall()
    {
        string response = null;
        for (var round = 0; round < Repeat; round++)
            response = await _calls[_request.GetType()](_provider, _request, CancellationToken.None);
        return response;
    }

    [Benchmark]
    public async Task ResolveManyAndCall()
    {
        for (var round = 0; round < Repeat; round++)
            foreach (var listener in _provider.GetServices<IPlainListener>())
                await listener.Handle(_note, CancellationToken.None);
    }

    public class PlainRequest
    {
        public string Message { get; set; }
    }

    public class PlainHandler
    {
        public Task<string> Handle(PlainRequest request, CancellationToken cancellationToken) => Task.FromResult(request.Message);
    }

    public class PlainNote
    {
    }

    public interface IPlainListener
    {
        Task Handle(PlainNote note, CancellationToken cancellationToken);
    }

    public class FirstPlainListener : IPlainListener
    {
        public Task Handle(PlainNote note, CancellationToken cancellationToken) => Task.CompletedTask;
    }

    public class SecondPlainListener : IPlainListener
    {
        public Task Handle(PlainNote note, CancellationToken cancellationToken) => Task.CompletedTask;
    }

    public class ThirdPlainListener : IPlainListener
    {
        public Task Handle(PlainNote note, CancellationToken cancellationToken) => Task.CompletedTask;
    }
}
