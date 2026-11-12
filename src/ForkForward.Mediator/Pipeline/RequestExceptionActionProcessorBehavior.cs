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
/// Behavior for executing all <see cref="IRequestExceptionAction{TRequest,TException}"/> instances
///     after an exception is thrown by the following pipeline steps
/// </summary>
/// <typeparam name="TRequest">Request type</typeparam>
/// <typeparam name="TResponse">Response type</typeparam>
public class RequestExceptionActionProcessorBehavior<TRequest, TResponse> : IPipelineBehavior<TRequest, TResponse>
    where TRequest : notnull
{
    private delegate Task ExceptionAction(TRequest request, Exception exception, CancellationToken cancellationToken);

    private readonly IServiceProvider _serviceProvider;

    public RequestExceptionActionProcessorBehavior(IServiceProvider serviceProvider) => _serviceProvider = serviceProvider;

    public async Task<TResponse> Handle(TRequest request, RequestHandlerDelegate<TResponse> next, CancellationToken cancellationToken)
    {
        try
        {
            return await next(cancellationToken).ConfigureAwait(false);
        }
        catch (Exception exception)
        {
            // Two ways to the same actions, in the same order. With a JIT the action interface is closed over the
            // exception types at run time. Native AOT cannot do that, so there the actions come from the
            // registrations that AddRequestExceptionAction made, which know their exception type at compile time.
            List<ExceptionAction> actionsForException;
            if (RuntimeFeature.IsDynamicCodeSupported)
            {
                actionsForException = FindActionsByReflection(request, exception.GetType());
            }
            else
            {
                actionsForException = FindRegisteredActions(request, exception.GetType());
            }

            foreach (var actionForException in actionsForException)
            {
                await actionForException(request, exception, cancellationToken).ConfigureAwait(false);
            }

            throw;
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

    [RequiresDynamicCode("Closes IRequestExceptionAction<,> over the type of the exception that was thrown.")]
    private List<ExceptionAction> FindActionsByReflection(TRequest request, Type thrownExceptionType)
        => GetExceptionTypes(thrownExceptionType)
            .SelectMany(exceptionType => GetActionsForException(exceptionType, request))
            .GroupBy(static actionForException => actionForException.Action.GetType())
            .Select(static actionForException => actionForException.First())
            .Select(static actionForException => InvokeByReflection(GetMethodInfoForAction(actionForException.ExceptionType), actionForException.Action))
            .ToList();

    private static ExceptionAction InvokeByReflection(MethodInfo methodInfo, object action)
        => (request, exception, cancellationToken) =>
        {
            try
            {
                return (Task) (methodInfo.Invoke(action, new object[] { request, exception, cancellationToken })
                               ?? throw new InvalidOperationException($"Could not create task for action method {methodInfo}."));
            }
            catch (TargetInvocationException invocationException) when (invocationException.InnerException != null)
            {
                // Unwrap invocation exception to throw the actual error
                ExceptionDispatchInfo.Capture(invocationException.InnerException).Throw();
                throw;
            }
        };

    [RequiresDynamicCode("Closes IRequestExceptionAction<,> over the type of the exception that was thrown.")]
    private IEnumerable<(Type ExceptionType, object Action)> GetActionsForException(Type exceptionType, TRequest request)
    {
        var exceptionActionInterfaceType = typeof(IRequestExceptionAction<,>).MakeGenericType(typeof(TRequest), exceptionType);
        var enumerableExceptionActionInterfaceType = typeof(IEnumerable<>).MakeGenericType(exceptionActionInterfaceType);

        var actionsForException = (IEnumerable<object>)_serviceProvider.GetRequiredService(enumerableExceptionActionInterfaceType);

        return HandlersOrderer.Prioritize(actionsForException.ToList(), request)
            .Select(action => (exceptionType, action));
    }

    [RequiresDynamicCode("Closes IRequestExceptionAction<,> over the type of the exception that was thrown.")]
    private static MethodInfo GetMethodInfoForAction(Type exceptionType)
    {
        var exceptionActionInterfaceType = typeof(IRequestExceptionAction<,>).MakeGenericType(typeof(TRequest), exceptionType);

        var actionMethodInfo =
            exceptionActionInterfaceType.GetMethod(nameof(IRequestExceptionAction<TRequest, Exception>.Execute))
            ?? throw new InvalidOperationException(
                $"Could not find method {nameof(IRequestExceptionAction<TRequest, Exception>.Execute)} on type {exceptionActionInterfaceType}");

        return actionMethodInfo;
    }

    // The same search as FindActionsByReflection: exception types from the thrown one up to Exception, for each of
    // them the actions registered for exactly that type in priority order, and each action class only once.
    private List<ExceptionAction> FindRegisteredActions(TRequest request, Type thrownExceptionType)
    {
        var registrations = _serviceProvider.GetServices<IRegisteredRequestExceptionActions<TRequest>>().ToList();

        return GetExceptionTypes(thrownExceptionType)
            .SelectMany(exceptionType => registrations
                .Where(registration => registration.ExceptionType == exceptionType)
                .SelectMany(registration => HandlersOrderer.Prioritize(registration.Actions.ToList(), request)
                    .Select(action => (Registration: registration, Action: action))))
            .GroupBy(static actionForException => actionForException.Action.GetType())
            .Select(static actionForException => actionForException.First())
            .Select(static actionForException => (ExceptionAction) ((request, exception, cancellationToken)
                => actionForException.Registration.Execute(actionForException.Action, request, exception, cancellationToken)))
            .ToList();
    }
}

/// <summary>
/// The exception actions that were registered for one request type and one exception type, behind an interface
/// that does not name the exception type. <see cref="RequestExceptionActionProcessorBehavior{TRequest,TResponse}"/>
/// uses it under Native AOT, where it cannot close <see cref="IRequestExceptionAction{TRequest,TException}"/> over
/// an exception type it meets at run time.
/// </summary>
internal interface IRegisteredRequestExceptionActions<in TRequest>
    where TRequest : notnull
{
    Type ExceptionType { get; }

    IEnumerable<object> Actions { get; }

    Task Execute(object action, TRequest request, Exception exception, CancellationToken cancellationToken);
}

internal sealed class RegisteredRequestExceptionActions<TRequest, TException> : IRegisteredRequestExceptionActions<TRequest>
    where TRequest : notnull
    where TException : Exception
{
    private readonly IEnumerable<IRequestExceptionAction<TRequest, TException>> _actions;

    public RegisteredRequestExceptionActions(IEnumerable<IRequestExceptionAction<TRequest, TException>> actions) => _actions = actions;

    public Type ExceptionType => typeof(TException);

    public IEnumerable<object> Actions => _actions;

    public Task Execute(object action, TRequest request, Exception exception, CancellationToken cancellationToken)
        => ((IRequestExceptionAction<TRequest, TException>) action).Execute(request, (TException) exception, cancellationToken);
}
