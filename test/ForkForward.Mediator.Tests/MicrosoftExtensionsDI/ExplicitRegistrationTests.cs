using Microsoft.Extensions.DependencyInjection;

namespace ForkForward.Mediator.Extensions.Microsoft.DependencyInjection.Tests;

using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Threading.Tasks;
using ForkForward.Mediator.Pipeline;
using Shouldly;
using Xunit;

/// <summary>
/// Handlers added one by one with AddRequestHandler and its siblings, without scanning an assembly.
/// The same scenarios run under Native AOT in test/ForkForward.Mediator.AotTests; here they run with a JIT.
/// The handlers only depend on <see cref="Logger"/>, which every test that scans this assembly registers.
/// </summary>
public class ExplicitRegistrationTests
{
    public record Greet(string Name) : IRequest<string>;

    public class GreetHandler : IRequestHandler<Greet, string>
    {
        public Task<string> Handle(Greet request, CancellationToken cancellationToken) => Task.FromResult($"Hello, {request.Name}");
    }

    public class OtherGreetHandler : IRequestHandler<Greet, string>
    {
        public Task<string> Handle(Greet request, CancellationToken cancellationToken) => Task.FromResult("other");
    }

    public record Square(int Number) : IRequest<int>;

    public class SquareHandler : IRequestHandler<Square, int>
    {
        public Task<int> Handle(Square request, CancellationToken cancellationToken) => Task.FromResult(request.Number * request.Number);
    }

    public record Touch : IRequest;

    public class TouchHandler : IRequestHandler<Touch>
    {
        private readonly Logger _trace;
        public TouchHandler(Logger trace) => _trace = trace;
        public Task Handle(Touch request, CancellationToken cancellationToken)
        {
            _trace.Messages.Add("touched");
            return Task.CompletedTask;
        }
    }

    public record Happened : INotification;

    public class FirstListener : INotificationHandler<Happened>
    {
        private readonly Logger _trace;
        public FirstListener(Logger trace) => _trace = trace;
        public Task Handle(Happened notification, CancellationToken cancellationToken)
        {
            _trace.Messages.Add("first");
            return Task.CompletedTask;
        }
    }

    public class SecondListener : INotificationHandler<Happened>
    {
        private readonly Logger _trace;
        public SecondListener(Logger trace) => _trace = trace;
        public Task Handle(Happened notification, CancellationToken cancellationToken)
        {
            _trace.Messages.Add("second");
            return Task.CompletedTask;
        }
    }

    public record Numbers(int Count) : IStreamRequest<int>;

    public class NumbersHandler : IStreamRequestHandler<Numbers, int>
    {
        public async IAsyncEnumerable<int> Handle(Numbers request, [EnumeratorCancellation] CancellationToken cancellationToken)
        {
            for (var number = 1; number <= request.Count; number++)
            {
                await Task.Yield();
                yield return number;
            }
        }
    }

    public record Breaks(bool Handle) : IRequest<string>;

    public class BreaksHandler : IRequestHandler<Breaks, string>
    {
        public Task<string> Handle(Breaks request, CancellationToken cancellationToken) => throw new TimeoutException("too slow");
    }

    public class TimeoutFallback : IRequestExceptionHandler<Breaks, string, TimeoutException>
    {
        private readonly Logger _trace;
        public TimeoutFallback(Logger trace) => _trace = trace;
        public Task Handle(Breaks request, TimeoutException exception, RequestExceptionHandlerState<string> state, CancellationToken cancellationToken)
        {
            _trace.Messages.Add("timeout handler");
            if (request.Handle)
            {
                state.SetHandled("fallback");
            }

            return Task.CompletedTask;
        }
    }

    public class AnyFailure : IRequestExceptionHandler<Breaks, string, Exception>
    {
        private readonly Logger _trace;
        public AnyFailure(Logger trace) => _trace = trace;
        public Task Handle(Breaks request, Exception exception, RequestExceptionHandlerState<string> state, CancellationToken cancellationToken)
        {
            _trace.Messages.Add("exception handler");
            return Task.CompletedTask;
        }
    }

    public class NoteFailure : IRequestExceptionAction<Breaks, Exception>
    {
        private readonly Logger _trace;
        public NoteFailure(Logger trace) => _trace = trace;
        public Task Execute(Breaks request, Exception exception, CancellationToken cancellationToken)
        {
            _trace.Messages.Add("action");
            return Task.CompletedTask;
        }
    }

    public class Around<TRequest, TResponse> : IPipelineBehavior<TRequest, TResponse>
        where TRequest : notnull
    {
        private readonly Logger _trace;
        public Around(Logger trace) => _trace = trace;
        public async Task<TResponse> Handle(TRequest request, RequestHandlerDelegate<TResponse> next, CancellationToken cancellationToken)
        {
            _trace.Messages.Add("before");
            var response = await next(cancellationToken);
            _trace.Messages.Add("after");
            return response;
        }
    }

    private static (IMediator Mediator, Logger Trace, IServiceCollection Services) Build(Action<MediatorServiceConfiguration> configure)
    {
        var trace = new Logger();
        IServiceCollection services = new ServiceCollection();
        services.AddSingleton(trace);
        services.AddMediator(configure);
        return (services.BuildServiceProvider().GetRequiredService<IMediator>(), trace, services);
    }

    [Fact]
    public async Task Should_send_a_request_with_a_reference_type_response()
    {
        var (mediator, _, _) = Build(cfg => cfg.AddRequestHandler<Greet, string, GreetHandler>());

        (await mediator.Send(new Greet("Ada"))).ShouldBe("Hello, Ada");
        (await mediator.Send((object) new Greet("Ada"))).ShouldBe("Hello, Ada");
    }

    [Fact]
    public async Task Should_send_a_request_with_a_value_type_response()
    {
        var (mediator, _, _) = Build(cfg => cfg.AddRequestHandler<Square, int, SquareHandler>());

        (await mediator.Send(new Square(7))).ShouldBe(49);
    }

    [Fact]
    public async Task Should_send_a_request_without_a_response()
    {
        var (mediator, trace, _) = Build(cfg => cfg.AddRequestHandler<Touch, TouchHandler>());

        await mediator.Send(new Touch());
        (await mediator.Send((object) new Touch())).ShouldBe(Unit.Value);

        trace.Messages.ShouldBe(new[] { "touched", "touched" });
    }

    [Fact]
    public async Task Should_keep_the_first_handler_of_a_request()
    {
        var (mediator, _, services) = Build(cfg =>
        {
            cfg.AddRequestHandler<Greet, string, GreetHandler>();
            cfg.AddRequestHandler<Greet, string, OtherGreetHandler>();
        });

        services.Count(descriptor => descriptor.ServiceType == typeof(IRequestHandler<Greet, string>)).ShouldBe(1);
        (await mediator.Send(new Greet("Ada"))).ShouldBe("Hello, Ada");
    }

    [Fact]
    public async Task Should_publish_to_every_handler_in_the_order_they_were_added()
    {
        var (mediator, trace, _) = Build(cfg =>
        {
            cfg.AddNotificationHandler<Happened, FirstListener>();
            cfg.AddNotificationHandler<Happened, SecondListener>();
        });

        await mediator.Publish(new Happened());
        await mediator.Publish((object) new Happened());

        trace.Messages.ShouldBe(new[] { "first", "second", "first", "second" });
    }

    [Fact]
    public void Should_not_register_a_notification_handler_twice()
    {
        var (_, _, services) = Build(cfg =>
        {
            cfg.AddNotificationHandler<Happened, FirstListener>();
            cfg.AddNotificationHandler<Happened, FirstListener>();
        });

        services.Count(descriptor => descriptor.ServiceType == typeof(INotificationHandler<Happened>)).ShouldBe(1);
    }

    [Fact]
    public void Should_not_register_twice_what_scanning_has_found()
    {
        var (_, _, services) = Build(cfg =>
        {
            cfg.RegisterServicesFromAssemblyContaining<ExplicitRegistrationTests>();
            cfg.AddNotificationHandler<Happened, FirstListener>();
            cfg.AddRequestHandler<Greet, string, OtherGreetHandler>();
        });

        services.Count(descriptor => descriptor.ServiceType == typeof(INotificationHandler<Happened>) && descriptor.ImplementationType == typeof(FirstListener)).ShouldBe(1);
        services.Count(descriptor => descriptor.ServiceType == typeof(IRequestHandler<Greet, string>)).ShouldBe(1);
    }

    [Fact]
    public async Task Should_create_a_stream()
    {
        var (mediator, _, _) = Build(cfg => cfg.AddStreamRequestHandler<Numbers, int, NumbersHandler>());

        var items = new List<int>();
        await foreach (var item in mediator.CreateStream(new Numbers(3)))
        {
            items.Add(item);
        }

        var boxed = new List<object?>();
        await foreach (var item in mediator.CreateStream((object) new Numbers(2)))
        {
            boxed.Add(item);
        }

        items.ShouldBe(new[] { 1, 2, 3 });
        boxed.ShouldBe(new object?[] { 1, 2 });
    }

    [Fact]
    public async Task Should_run_exception_handlers_from_the_exact_type_to_the_base_type_and_then_the_action()
    {
        var (mediator, trace, _) = Build(cfg =>
        {
            cfg.AddRequestHandler<Breaks, string, BreaksHandler>();
            cfg.AddRequestExceptionHandler<Breaks, string, Exception, AnyFailure>();
            cfg.AddRequestExceptionHandler<Breaks, string, TimeoutException, TimeoutFallback>();
            cfg.AddRequestExceptionAction<Breaks, Exception, NoteFailure>();
        });

        await Should.ThrowAsync<TimeoutException>(() => mediator.Send(new Breaks(Handle: false)));

        trace.Messages.ShouldBe(new[] { "timeout handler", "exception handler", "action" });
    }

    [Fact]
    public async Task Should_return_the_response_of_an_exception_handler()
    {
        var (mediator, trace, _) = Build(cfg =>
        {
            cfg.AddRequestHandler<Breaks, string, BreaksHandler>();
            cfg.AddRequestExceptionHandler<Breaks, string, TimeoutException, TimeoutFallback>();
            cfg.AddRequestExceptionAction<Breaks, Exception, NoteFailure>();
        });

        (await mediator.Send(new Breaks(Handle: true))).ShouldBe("fallback");

        trace.Messages.ShouldBe(new[] { "timeout handler" });
    }

    [Fact]
    public async Task Should_run_behaviors_around_a_handler_added_one_by_one()
    {
        var (mediator, trace, _) = Build(cfg =>
        {
            cfg.AddRequestHandler<Touch, TouchHandler>();
            cfg.AddOpenBehavior(typeof(Around<,>));
        });

        await mediator.Send(new Touch());

        trace.Messages.ShouldBe(new[] { "before", "touched", "after" });
    }

    [Fact]
    public void Should_not_need_an_assembly_when_handlers_are_added_one_by_one()
    {
        Should.NotThrow(() => new ServiceCollection().AddMediator(cfg => cfg.AddRequestHandler<Greet, string, GreetHandler>()));
    }

    [Fact]
    public void Should_require_an_assembly_or_a_handler()
    {
        Should.Throw<ArgumentException>(() => new ServiceCollection().AddMediator(_ => { }));
    }
}
