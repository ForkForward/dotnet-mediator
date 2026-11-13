using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Threading.Tasks;
using ForkForward.Mediator.NotificationPublishers;
using Shouldly;
using Xunit;

namespace ForkForward.Mediator.Tests;

/// <summary>
/// The mediator walks what the container returns for IEnumerable&lt;T&gt; as an array when it is one, which is what
/// Microsoft.Extensions.DependencyInjection returns. A container may return a list or a lazy sequence instead,
/// and a publisher may be given something that is not an array. These tests hold the other path.
/// </summary>
public class NonArrayContainerTests
{
    // The handlers and behaviors have no constructor parameters and write to the message they get: other tests
    // scan this assembly and validate the container, and must not meet a type they cannot create.
    public class Question : IRequest<string>
    {
        public List<string> Log { get; } = new();
    }

    public class QuestionHandler : IRequestHandler<Question, string>
    {
        public Task<string> Handle(Question request, CancellationToken cancellationToken)
        {
            request.Log.Add("handler");
            return Task.FromResult("answer");
        }
    }

    public class OuterQuestionBehavior : IPipelineBehavior<Question, string>
    {
        public async Task<string> Handle(Question request, RequestHandlerDelegate<string> next, CancellationToken cancellationToken)
        {
            request.Log.Add("outer in");
            var response = await next(cancellationToken);
            request.Log.Add("outer out");
            return response;
        }
    }

    public class InnerQuestionBehavior : IPipelineBehavior<Question, string>
    {
        public async Task<string> Handle(Question request, RequestHandlerDelegate<string> next, CancellationToken cancellationToken)
        {
            request.Log.Add("inner in");
            var response = await next(cancellationToken);
            request.Log.Add("inner out");
            return response;
        }
    }

    public class Command : IRequest
    {
        public List<string> Log { get; } = new();
    }

    public class CommandHandler : IRequestHandler<Command>
    {
        public Task Handle(Command request, CancellationToken cancellationToken)
        {
            request.Log.Add("handler");
            return Task.CompletedTask;
        }
    }

    public class OuterCommandBehavior : IPipelineBehavior<Command, Unit>
    {
        public async Task<Unit> Handle(Command request, RequestHandlerDelegate<Unit> next, CancellationToken cancellationToken)
        {
            request.Log.Add("outer in");
            var response = await next(cancellationToken);
            request.Log.Add("outer out");
            return response;
        }
    }

    public class InnerCommandBehavior : IPipelineBehavior<Command, Unit>
    {
        public async Task<Unit> Handle(Command request, RequestHandlerDelegate<Unit> next, CancellationToken cancellationToken)
        {
            request.Log.Add("inner in");
            var response = await next(cancellationToken);
            request.Log.Add("inner out");
            return response;
        }
    }

    public class Numbers : IStreamRequest<int>
    {
        public List<string> Log { get; } = new();
    }

    public class NumbersHandler : IStreamRequestHandler<Numbers, int>
    {
        public async IAsyncEnumerable<int> Handle(Numbers request, [EnumeratorCancellation] CancellationToken cancellationToken)
        {
            await Task.CompletedTask;
            request.Log.Add("handler");
            yield return 1;
            yield return 2;
        }
    }

    public class AddTen : IStreamPipelineBehavior<Numbers, int>
    {
        public async IAsyncEnumerable<int> Handle(Numbers request, StreamHandlerDelegate<int> next, [EnumeratorCancellation] CancellationToken cancellationToken)
        {
            request.Log.Add("add ten");
            await foreach (var item in next().WithCancellation(cancellationToken)) yield return item + 10;
        }
    }

    public class Double : IStreamPipelineBehavior<Numbers, int>
    {
        public async IAsyncEnumerable<int> Handle(Numbers request, StreamHandlerDelegate<int> next, [EnumeratorCancellation] CancellationToken cancellationToken)
        {
            request.Log.Add("double");
            await foreach (var item in next().WithCancellation(cancellationToken)) yield return item * 2;
        }
    }

    public class Happened : INotification
    {
        public List<string> Log { get; } = new();
    }

    public class FirstListener : INotificationHandler<Happened>
    {
        public Task Handle(Happened notification, CancellationToken cancellationToken)
        {
            notification.Log.Add("first");
            return Task.CompletedTask;
        }
    }

    public class SecondListener : INotificationHandler<Happened>
    {
        public Task Handle(Happened notification, CancellationToken cancellationToken)
        {
            notification.Log.Add("second");
            return Task.CompletedTask;
        }
    }

    /// <summary>A container that returns a list for IEnumerable&lt;T&gt;, never an array.</summary>
    private sealed class ListContainer : IServiceProvider
    {
        private readonly Dictionary<Type, object> _services = new();

        public ListContainer Add<TService>(TService service) where TService : notnull
        {
            _services[typeof(TService)] = service;
            return this;
        }

        public ListContainer AddMany<TService>(params TService[] services)
        {
            _services[typeof(IEnumerable<TService>)] = services.ToList();
            return this;
        }

        public object? GetService(Type serviceType)
        {
            if (_services.TryGetValue(serviceType, out var service)) return service;
            if (serviceType.IsGenericType && serviceType.GetGenericTypeDefinition() == typeof(IEnumerable<>))
                return Activator.CreateInstance(typeof(List<>).MakeGenericType(serviceType.GetGenericArguments()));
            return null;
        }
    }

    [Fact]
    public async Task Behaviors_from_a_list_wrap_a_request_with_a_response_in_registration_order()
    {
        var container = new ListContainer()
            .Add<IRequestHandler<Question, string>>(new QuestionHandler())
            .AddMany<IPipelineBehavior<Question, string>>(new OuterQuestionBehavior(), new InnerQuestionBehavior());
        var question = new Question();

        var response = await new Mediator(container).Send(question);

        response.ShouldBe("answer");
        question.Log.ShouldBe(new[] { "outer in", "inner in", "handler", "inner out", "outer out" });
    }

    [Fact]
    public async Task Behaviors_from_a_list_wrap_a_request_without_a_response_in_registration_order()
    {
        var container = new ListContainer()
            .Add<IRequestHandler<Command>>(new CommandHandler())
            .AddMany<IPipelineBehavior<Command, Unit>>(new OuterCommandBehavior(), new InnerCommandBehavior());
        var command = new Command();

        await new Mediator(container).Send(command);

        command.Log.ShouldBe(new[] { "outer in", "inner in", "handler", "inner out", "outer out" });
    }

    [Fact]
    public async Task A_request_without_behaviors_reaches_its_handler()
    {
        var container = new ListContainer().Add<IRequestHandler<Question, string>>(new QuestionHandler());
        var question = new Question();

        var response = await new Mediator(container).Send(question);

        response.ShouldBe("answer");
        question.Log.ShouldBe(new[] { "handler" });
    }

    [Fact]
    public async Task Stream_behaviors_from_a_list_wrap_the_handler_in_registration_order()
    {
        var container = new ListContainer()
            .Add<IStreamRequestHandler<Numbers, int>>(new NumbersHandler())
            .AddMany<IStreamPipelineBehavior<Numbers, int>>(new AddTen(), new Double());
        var numbers = new Numbers();

        var items = new List<int>();
        await foreach (var item in new Mediator(container).CreateStream(numbers)) items.Add(item);

        // The handler gives 1 and 2; Double is nearer to it and AddTen is outermost.
        items.ShouldBe(new[] { 12, 14 });
        numbers.Log.ShouldBe(new[] { "add ten", "double", "handler" });
    }

    [Fact]
    public async Task Notification_handlers_from_a_list_are_called_in_registration_order()
    {
        var container = new ListContainer()
            .AddMany<INotificationHandler<Happened>>(new FirstListener(), new SecondListener());
        var happened = new Happened();

        await new Mediator(container).Publish(happened);

        happened.Log.ShouldBe(new[] { "first", "second" });
    }

    [Fact]
    public async Task A_notification_without_handlers_is_published_without_an_error()
    {
        var happened = new Happened();

        await new Mediator(new ListContainer()).Publish(happened);

        happened.Log.ShouldBeEmpty();
    }

    [Theory]
    [InlineData(typeof(ForeachAwaitPublisher), true)]
    [InlineData(typeof(ForeachAwaitPublisher), false)]
    [InlineData(typeof(TaskWhenAllPublisher), true)]
    [InlineData(typeof(TaskWhenAllPublisher), false)]
    public async Task A_publisher_calls_every_handler_in_order_whether_it_gets_an_array_or_not(Type publisherType, bool asArray)
    {
        var publisher = (INotificationPublisher) Activator.CreateInstance(publisherType)!;
        var calls = new List<string>();
        NotificationHandlerExecutor Executor(string name) => new(name, (_, _) =>
        {
            calls.Add(name);
            return Task.CompletedTask;
        });
        var executors = new[] { Executor("first"), Executor("second"), Executor("third") };

        await publisher.Publish(asArray ? executors : executors.Where(_ => true), new Happened(), CancellationToken.None);

        calls.ShouldBe(new[] { "first", "second", "third" });
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void TaskWhenAllPublisher_starts_no_later_handler_after_one_throws_before_its_first_await(bool asArray)
    {
        var calls = new List<string>();
        var executors = new[]
        {
            new NotificationHandlerExecutor("first", (_, _) => throw new InvalidOperationException("first")),
            new NotificationHandlerExecutor("second", (_, _) =>
            {
                calls.Add("second");
                return Task.CompletedTask;
            })
        };

        Should.Throw<InvalidOperationException>(() =>
        {
            _ = new TaskWhenAllPublisher().Publish(asArray ? executors : executors.Where(_ => true), new Happened(), CancellationToken.None);
        });

        calls.ShouldBeEmpty();
    }
}
