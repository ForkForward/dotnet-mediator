using System;
using System.Collections.Concurrent;
using System.Diagnostics.CodeAnalysis;
using System.Linq;
using System.Runtime.CompilerServices;

namespace ForkForward.Mediator.Wrappers;

/// <summary>
/// The wrapper objects that connect a message type to its handlers, one per message type for the life of the process.
/// A wrapper is created either from the message type as a type argument, which works everywhere, or from the
/// message type as a <see cref="Type"/> found at run time, which needs dynamic code and so does not work under
/// Native AOT.
/// </summary>
internal static class WrapperCache
{
    internal static readonly ConcurrentDictionary<Type, RequestHandlerBase> Requests = new();
    internal static readonly ConcurrentDictionary<Type, NotificationHandlerWrapper> Notifications = new();
    internal static readonly ConcurrentDictionary<Type, StreamRequestHandlerBase> Streams = new();

    internal static void AddRequest<TRequest, TResponse>() where TRequest : IRequest<TResponse>
        => Requests.TryAdd(typeof(TRequest), new RequestHandlerWrapperImpl<TRequest, TResponse>());

    internal static void AddRequest<TRequest>() where TRequest : IRequest
        => Requests.TryAdd(typeof(TRequest), VoidRequest<TRequest>.Wrapper);

    internal static void AddNotification<TNotification>() where TNotification : INotification
        => Notifications.TryAdd(typeof(TNotification), Notification<TNotification>.Wrapper);

    internal static void AddStreamRequest<TRequest, TResponse>() where TRequest : IStreamRequest<TResponse>
        => Streams.TryAdd(typeof(TRequest), new StreamRequestHandlerWrapperImpl<TRequest, TResponse>());

    /// <summary>The wrapper for a request without a response whose type is known at compile time.</summary>
    internal static class VoidRequest<TRequest> where TRequest : IRequest
    {
        internal static readonly RequestHandlerWrapper Wrapper = new RequestHandlerWrapperImpl<TRequest>();
    }

    /// <summary>The wrapper for a notification whose type is known at compile time.</summary>
    internal static class Notification<TNotification> where TNotification : INotification
    {
        internal static readonly NotificationHandlerWrapper Wrapper = new NotificationHandlerWrapperImpl<TNotification>();
    }

    internal static RequestHandlerBase GetRequest(Type requestType, Type responseType)
        => Requests.GetOrAdd(requestType, static (type, response) => CreateRequest(type, response), responseType);

    internal static RequestHandlerBase GetVoidRequest(Type requestType)
        => Requests.GetOrAdd(requestType, static type => CreateVoidRequest(type));

    internal static RequestHandlerBase GetRequestOfUnknownShape(Type requestType)
        => Requests.GetOrAdd(requestType, static type => CreateRequestOfUnknownShape(type));

    internal static NotificationHandlerWrapper GetNotification(Type notificationType)
        => Notifications.GetOrAdd(notificationType, static type => CreateNotification(type));

    internal static StreamRequestHandlerBase GetStreamRequest(Type requestType, Type responseType)
        => Streams.GetOrAdd(requestType, static (type, response) => CreateStreamRequest(type, response), responseType);

    internal static StreamRequestHandlerBase GetStreamRequestOfUnknownShape(Type requestType)
        => Streams.GetOrAdd(requestType, static type => CreateStreamRequestOfUnknownShape(type));

    // The methods below build a wrapper type from Type objects. That needs code for a generic instantiation the
    // compiler has not seen, which a JIT produces on demand and Native AOT cannot. Each of them refuses under
    // Native AOT with a message that names the registration to add; MediatorServiceConfiguration.AddRequestHandler
    // and its siblings create the wrapper from type arguments instead.

    [DynamicDependency(DynamicallyAccessedMemberTypes.PublicParameterlessConstructor, typeof(RequestHandlerWrapperImpl<,>))]
    [UnconditionalSuppressMessage("Trimming", "IL2072", Justification = WrapperConstructorIsKept)]
    private static RequestHandlerBase CreateRequest(Type requestType, Type responseType)
    {
        if (!RuntimeFeature.IsDynamicCodeSupported) throw NotRegistered(requestType, "AddRequestHandler<TRequest, TResponse, THandler>()");
        var wrapperType = typeof(RequestHandlerWrapperImpl<,>).MakeGenericType(requestType, responseType);
        return (RequestHandlerBase) (Activator.CreateInstance(wrapperType) ?? throw new InvalidOperationException($"Could not create wrapper type for {requestType}"));
    }

    [DynamicDependency(DynamicallyAccessedMemberTypes.PublicParameterlessConstructor, typeof(RequestHandlerWrapperImpl<>))]
    [UnconditionalSuppressMessage("Trimming", "IL2072", Justification = WrapperConstructorIsKept)]
    private static RequestHandlerBase CreateVoidRequest(Type requestType)
    {
        if (!RuntimeFeature.IsDynamicCodeSupported) throw NotRegistered(requestType, "AddRequestHandler<TRequest, THandler>()");
        var wrapperType = typeof(RequestHandlerWrapperImpl<>).MakeGenericType(requestType);
        return (RequestHandlerBase) (Activator.CreateInstance(wrapperType) ?? throw new InvalidOperationException($"Could not create wrapper type for {requestType}"));
    }

    [UnconditionalSuppressMessage("Trimming", "IL2070", Justification = MessageInterfacesAreKept)]
    private static RequestHandlerBase CreateRequestOfUnknownShape(Type requestType)
    {
        var requestInterfaceType = requestType.GetInterfaces().FirstOrDefault(static i => i.IsGenericType && i.GetGenericTypeDefinition() == typeof(IRequest<>));
        if (requestInterfaceType is not null)
        {
            return CreateRequest(requestType, requestInterfaceType.GetGenericArguments()[0]);
        }

        if (!requestType.GetInterfaces().Any(static i => i == typeof(IRequest)))
        {
            throw new ArgumentException($"{requestType.Name} does not implement {nameof(IRequest)}", "request");
        }

        return CreateVoidRequest(requestType);
    }

    [DynamicDependency(DynamicallyAccessedMemberTypes.PublicParameterlessConstructor, typeof(NotificationHandlerWrapperImpl<>))]
    [UnconditionalSuppressMessage("Trimming", "IL2072", Justification = WrapperConstructorIsKept)]
    private static NotificationHandlerWrapper CreateNotification(Type notificationType)
    {
        if (!RuntimeFeature.IsDynamicCodeSupported) throw NotRegistered(notificationType, "AddNotificationHandler<TNotification, THandler>()");
        var wrapperType = typeof(NotificationHandlerWrapperImpl<>).MakeGenericType(notificationType);
        return (NotificationHandlerWrapper) (Activator.CreateInstance(wrapperType) ?? throw new InvalidOperationException($"Could not create wrapper for type {notificationType}"));
    }

    [DynamicDependency(DynamicallyAccessedMemberTypes.PublicParameterlessConstructor, typeof(StreamRequestHandlerWrapperImpl<,>))]
    [UnconditionalSuppressMessage("Trimming", "IL2072", Justification = WrapperConstructorIsKept)]
    private static StreamRequestHandlerBase CreateStreamRequest(Type requestType, Type responseType)
    {
        if (!RuntimeFeature.IsDynamicCodeSupported) throw NotRegistered(requestType, "AddStreamRequestHandler<TRequest, TResponse, THandler>()");
        var wrapperType = typeof(StreamRequestHandlerWrapperImpl<,>).MakeGenericType(requestType, responseType);
        return (StreamRequestHandlerBase) (Activator.CreateInstance(wrapperType) ?? throw new InvalidOperationException($"Could not create wrapper for type {requestType}"));
    }

    [UnconditionalSuppressMessage("Trimming", "IL2070", Justification = MessageInterfacesAreKept)]
    private static StreamRequestHandlerBase CreateStreamRequestOfUnknownShape(Type requestType)
    {
        var requestInterfaceType = requestType.GetInterfaces().FirstOrDefault(static i => i.IsGenericType && i.GetGenericTypeDefinition() == typeof(IStreamRequest<>));
        if (requestInterfaceType is null)
        {
            throw new ArgumentException($"{requestType.Name} does not implement IStreamRequest<TResponse>", "request");
        }

        return CreateStreamRequest(requestType, requestInterfaceType.GetGenericArguments()[0]);
    }

    private const string WrapperConstructorIsKept =
        "The wrapper type is one of the generic wrapper classes of this assembly. DynamicDependency keeps their parameterless constructors, and their type parameters have no requirements.";

    private const string MessageInterfacesAreKept =
        "The type is the type of a message object the application created. The message interfaces it implements are kept, because the mediator's own code refers to them.";

    private static InvalidOperationException NotRegistered(Type messageType, string registration)
        => new(
            $"{messageType} was not registered, and its type is known only at run time. Under Native AOT the mediator cannot " +
            $"create the code for it on demand. Register its handler with {nameof(Microsoft.Extensions.DependencyInjection.MediatorServiceConfiguration)}.{registration}.");
}
