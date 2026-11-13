namespace ForkForward.Mediator.Pipeline;

using Internal;
using Microsoft.Extensions.DependencyInjection;
using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Linq;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Runtime.ExceptionServices;
using System.Threading;
using System.Threading.Tasks;

/// <summary>
/// Behavior for executing all <see cref="IRequestExceptionHandler{TRequest,TResponse,TException}"/> instances
///     after an exception is thrown by the following pipeline steps
/// </summary>
/// <typeparam name="TRequest">Request type</typeparam>
/// <typeparam name="TResponse">Response type</typeparam>
public class RequestExceptionProcessorBehavior<TRequest, TResponse> : IPipelineBehavior<TRequest, TResponse>
    where TRequest : notnull
{
    private delegate Task ExceptionHandler(TRequest request, Exception exception, RequestExceptionHandlerState<TResponse> state, CancellationToken cancellationToken);

    private readonly IServiceProvider _serviceProvider;

    public RequestExceptionProcessorBehavior(IServiceProvider serviceProvider) => _serviceProvider = serviceProvider;

    public async Task<TResponse> Handle(TRequest request, RequestHandlerDelegate<TResponse> next, CancellationToken cancellationToken)
    {
        try
        {
            return await next(cancellationToken).ConfigureAwait(false);
        }
        catch (Exception exception)
        {
            var state = new RequestExceptionHandlerState<TResponse>();

            // Two ways to the same handlers, in the same order. With a JIT the handler interfaces are closed over
            // the exception types at run time. Native AOT cannot do that, so there the handlers come from the
            // registrations that AddRequestExceptionHandler made, which know their exception type at compile time.
            List<ExceptionHandler> handlersForException;
            if (RuntimeFeature.IsDynamicCodeSupported)
            {
                handlersForException = FindHandlersByReflection(request, exception.GetType());
            }
            else
            {
                handlersForException = FindRegisteredHandlers(request, exception.GetType());
            }

            foreach (var handlerForException in handlersForException)
            {
                await handlerForException(request, exception, state, cancellationToken).ConfigureAwait(false);

                if (state.Handled)
                {
                    break;
                }
            }

            if (!state.Handled)
            {
                throw;
            }

            if (state.Response is null)
            {
                throw;
            }

            return state.Response; //cannot be null if Handled
        }
    }

    private static IEnumerable<Type> GetExceptionTypes(Type? exceptionType)
    {
        while (exceptionType != null && exceptionType != typeof(object))
        {
            yield return exceptionType;
            exceptionType = exceptionType.BaseType;
        }
    }

    [RequiresDynamicCode("Closes IRequestExceptionHandler<,,> over the type of the exception that was thrown.")]
    private List<ExceptionHandler> FindHandlersByReflection(TRequest request, Type thrownExceptionType)
        => GetExceptionTypes(thrownExceptionType)
            .SelectMany(exceptionType => GetHandlersForException(exceptionType, request))
            .GroupBy(static handlerForException => handlerForException.Handler.GetType())
            .Select(static handlerForException => handlerForException.First())
            .Select(static handlerForException => InvokeByReflection(GetMethodInfoForHandler(handlerForException.ExceptionType), handlerForException.Handler))
            .ToList();

    private static ExceptionHandler InvokeByReflection(MethodInfo methodInfo, object handler)
        => (request, exception, state, cancellationToken) =>
        {
            try
            {
                return (Task) (methodInfo.Invoke(handler, new object[] { request, exception, state, cancellationToken })
                               ?? throw new InvalidOperationException("Did not return a Task from the exception handler."));
            }
            catch (TargetInvocationException invocationException) when (invocationException.InnerException != null)
            {
                // Unwrap invocation exception to throw the actual error
                ExceptionDispatchInfo.Capture(invocationException.InnerException).Throw();
                throw;
            }
        };

    [RequiresDynamicCode("Closes IRequestExceptionHandler<,,> over the type of the exception that was thrown.")]
    private IEnumerable<(Type ExceptionType, object Handler)> GetHandlersForException(Type exceptionType, TRequest request)
    {
        var exceptionHandlerInterfaceType = typeof(IRequestExceptionHandler<,,>).MakeGenericType(typeof(TRequest), typeof(TResponse), exceptionType);
        var enumerableExceptionHandlerInterfaceType = typeof(IEnumerable<>).MakeGenericType(exceptionHandlerInterfaceType);

        var exceptionHandlers = (IEnumerable<object>) _serviceProvider.GetRequiredService(enumerableExceptionHandlerInterfaceType);

        return HandlersOrderer.Prioritize(exceptionHandlers.ToList(), request)
            .Select(handler => (exceptionType, action: handler));
    }

    [RequiresDynamicCode("Closes IRequestExceptionHandler<,,> over the type of the exception that was thrown.")]
    private static MethodInfo GetMethodInfoForHandler(Type exceptionType)
    {
        var exceptionHandlerInterfaceType = typeof(IRequestExceptionHandler<,,>).MakeGenericType(typeof(TRequest), typeof(TResponse), exceptionType);

        var handleMethodInfo = exceptionHandlerInterfaceType.GetMethod(nameof(IRequestExceptionHandler<TRequest, TResponse, Exception>.Handle))
                           ?? throw new InvalidOperationException($"Could not find method {nameof(IRequestExceptionHandler<TRequest, TResponse, Exception>.Handle)} on type {exceptionHandlerInterfaceType}");

        return handleMethodInfo;
    }

    // The same search as FindHandlersByReflection: exception types from the thrown one up to Exception, for each of
    // them the handlers registered for exactly that type in priority order, and each handler class only once.
    private List<ExceptionHandler> FindRegisteredHandlers(TRequest request, Type thrownExceptionType)
    {
        var registrations = _serviceProvider.GetServices<IRegisteredRequestExceptionHandlers<TRequest, TResponse>>().ToList();

        return GetExceptionTypes(thrownExceptionType)
            .SelectMany(exceptionType => registrations
                .Where(registration => registration.ExceptionType == exceptionType)
                .SelectMany(registration => HandlersOrderer.Prioritize(registration.Handlers.ToList(), request)
                    .Select(handler => (Registration: registration, Handler: handler))))
            .GroupBy(static handlerForException => handlerForException.Handler.GetType())
            .Select(static handlerForException => handlerForException.First())
            .Select(static handlerForException => (ExceptionHandler) ((request, exception, state, cancellationToken)
                => handlerForException.Registration.Handle(handlerForException.Handler, request, exception, state, cancellationToken)))
            .ToList();
    }
}

/// <summary>
/// The exception handlers that were registered for one request type and one exception type, behind an interface
/// that does not name the exception type. <see cref="RequestExceptionProcessorBehavior{TRequest,TResponse}"/> uses
/// it under Native AOT, where it cannot close <see cref="IRequestExceptionHandler{TRequest,TResponse,TException}"/>
/// over an exception type it meets at run time.
/// </summary>
internal interface IRegisteredRequestExceptionHandlers<in TRequest, TResponse>
    where TRequest : notnull
{
    Type ExceptionType { get; }

    IEnumerable<object> Handlers { get; }

    Task Handle(object handler, TRequest request, Exception exception, RequestExceptionHandlerState<TResponse> state, CancellationToken cancellationToken);
}

internal sealed class RegisteredRequestExceptionHandlers<TRequest, TResponse, TException> : IRegisteredRequestExceptionHandlers<TRequest, TResponse>
    where TRequest : notnull
    where TException : Exception
{
    private readonly IEnumerable<IRequestExceptionHandler<TRequest, TResponse, TException>> _handlers;

    public RegisteredRequestExceptionHandlers(IEnumerable<IRequestExceptionHandler<TRequest, TResponse, TException>> handlers) => _handlers = handlers;

    public Type ExceptionType => typeof(TException);

    public IEnumerable<object> Handlers => _handlers;

    public Task Handle(object handler, TRequest request, Exception exception, RequestExceptionHandlerState<TResponse> state, CancellationToken cancellationToken)
        => ((IRequestExceptionHandler<TRequest, TResponse, TException>) handler).Handle(request, (TException) exception, state, cancellationToken);
}
