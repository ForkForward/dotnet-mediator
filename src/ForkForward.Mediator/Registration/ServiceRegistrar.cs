using System.Diagnostics.CodeAnalysis;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Threading;
using ForkForward.Mediator.Pipeline;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace ForkForward.Mediator.Registration;

public static class ServiceRegistrar
{
    /// <summary>
    /// The limits on closing generic request handlers. A registration takes them from the configuration it was
    /// given, never from shared state: two registrations that run at the same time with different limits must
    /// not see each other's.
    /// </summary>
    private sealed class Limits(int maxGenericTypeParameters, int maxTypesClosing, int maxGenericTypeRegistrations)
    {
        public static readonly Limits None = new(0, 0, 0);

        public Limits(MediatorServiceConfiguration configuration)
            : this(configuration.MaxGenericTypeParameters, configuration.MaxTypesClosing, configuration.MaxGenericTypeRegistrations)
        {
        }

        public int MaxGenericTypeParameters { get; } = maxGenericTypeParameters;
        public int MaxTypesClosing { get; } = maxTypesClosing;
        public int MaxGenericTypeRegistrations { get; } = maxGenericTypeRegistrations;
    }

    // Read only by the public GenerateCombinations, for code that calls it directly.
    private static Limits LimitsForDirectCalls = Limits.None;

    private const string ScanningNeedsReflection =
        "Scanning an assembly finds handlers by reflection, which trimming and Native AOT cannot follow. Register each handler with AddRequestHandler, AddNotificationHandler and their siblings on MediatorServiceConfiguration instead.";

    /// <summary>
    /// Sets the limits that <see cref="GenerateCombinations(Type, List{List{Type}}, int, CancellationToken)"/>
    /// applies when it is called directly. <see cref="AddMediatorClasses"/> and
    /// <see cref="AddMediatorClassesWithTimeout"/> do not need this call: they take the limits and the timeout
    /// from the configuration they are given.
    /// </summary>
    public static void SetGenericRequestHandlerRegistrationLimitations(MediatorServiceConfiguration configuration)
    {
        LimitsForDirectCalls = new Limits(configuration);
    }

    [RequiresUnreferencedCode(ScanningNeedsReflection)]
    [RequiresDynamicCode(ScanningNeedsReflection)]
    public static void AddMediatorClassesWithTimeout(IServiceCollection services, MediatorServiceConfiguration configuration)
    {
        using(var cts = new CancellationTokenSource(configuration.RegistrationTimeout))
        {
            try
            {
                AddMediatorClasses(services, configuration, cts.Token);
            }
            catch (OperationCanceledException)
            {
                throw new TimeoutException("The generic handler registration process timed out.");
            }
        }
    }

    [RequiresUnreferencedCode(ScanningNeedsReflection)]
    [RequiresDynamicCode(ScanningNeedsReflection)]
    public static void AddMediatorClasses(IServiceCollection services, MediatorServiceConfiguration configuration, CancellationToken cancellationToken = default)
    {   

        var assembliesToScan = configuration.AssembliesToRegister.Distinct().ToArray();

        ConnectImplementationsToTypesClosing(typeof(IRequestHandler<,>), services, assembliesToScan, false, configuration, cancellationToken);
        ConnectImplementationsToTypesClosing(typeof(IRequestHandler<>), services, assembliesToScan, false, configuration, cancellationToken);
        ConnectImplementationsToTypesClosing(typeof(INotificationHandler<>), services, assembliesToScan, true, configuration);
        ConnectImplementationsToTypesClosing(typeof(IStreamRequestHandler<,>), services, assembliesToScan, false, configuration);
        ConnectImplementationsToTypesClosing(typeof(IRequestExceptionHandler<,,>), services, assembliesToScan, true, configuration);
        ConnectImplementationsToTypesClosing(typeof(IRequestExceptionAction<,>), services, assembliesToScan, true, configuration);

        if (configuration.AutoRegisterRequestProcessors)
        {
            ConnectImplementationsToTypesClosing(typeof(IRequestPreProcessor<>), services, assembliesToScan, true, configuration);
            ConnectImplementationsToTypesClosing(typeof(IRequestPostProcessor<,>), services, assembliesToScan, true, configuration);
        }

        var multiOpenInterfaces = new List<Type>
        {
            typeof(INotificationHandler<>),
            typeof(IRequestExceptionHandler<,,>),
            typeof(IRequestExceptionAction<,>)
        };

        if (configuration.AutoRegisterRequestProcessors)
        {
            multiOpenInterfaces.Add(typeof(IRequestPreProcessor<>));
            multiOpenInterfaces.Add(typeof(IRequestPostProcessor<,>));
        }

        foreach (var multiOpenInterface in multiOpenInterfaces)
        {
            var arity = multiOpenInterface.GetGenericArguments().Length;

            var concretions = assembliesToScan
                .SelectMany(a => a.DefinedTypes)
                .Where(type => type.FindInterfacesThatClose(multiOpenInterface).Any())
                .Where(type => type.IsConcrete() && type.IsOpenGeneric())
                .Where(type => type.GetGenericArguments().Length == arity)
                .Where(configuration.TypeEvaluator)
                .ToList();

            foreach (var type in concretions)
            {
                services.AddTransient(multiOpenInterface, type);
            }
        }
    }

    [RequiresUnreferencedCode(ScanningNeedsReflection)]
    [RequiresDynamicCode(ScanningNeedsReflection)]
    private static void ConnectImplementationsToTypesClosing(Type openRequestInterface,
        IServiceCollection services,
        IEnumerable<Assembly> assembliesToScan,
        bool addIfAlreadyExists,
        MediatorServiceConfiguration configuration,
        CancellationToken cancellationToken = default)
    {
        var limits = new Limits(configuration);
        var concretions = new List<Type>();
        var interfaces = new List<Type>();
        var genericConcretions = new List<Type>();
        var genericInterfaces = new List<Type>();

        var types = assembliesToScan
            .SelectMany(a => a.DefinedTypes)
            .Where(t => !t.ContainsGenericParameters || configuration.RegisterGenericHandlers)
            .Where(t => t.IsConcrete() && t.FindInterfacesThatClose(openRequestInterface).Any())
            .Where(configuration.TypeEvaluator)
            .ToList();        

        foreach (var type in types)
        {
            var interfaceTypes = type.FindInterfacesThatClose(openRequestInterface).ToArray();

            if (!type.IsOpenGeneric())
            {
                concretions.Add(type);

                foreach (var interfaceType in interfaceTypes)
                {
                    interfaces.Fill(interfaceType);
                }
            }
            else
            {
                genericConcretions.Add(type);
                foreach (var interfaceType in interfaceTypes)
                {
                    genericInterfaces.Fill(interfaceType);
                }
            }
        }

        foreach (var @interface in interfaces)
        {
            var exactMatches = concretions.Where(x => x.CanBeCastTo(@interface)).ToList();
            if (addIfAlreadyExists)
            {
                foreach (var type in exactMatches)
                {
                    services.AddTransient(@interface, type);
                }
            }
            else
            {
                if (exactMatches.Count > 1)
                {
                    exactMatches.RemoveAll(m => !IsMatchingWithInterface(m, @interface));
                }

                foreach (var type in exactMatches)
                {
                    services.TryAddTransient(@interface, type);
                }
            }

            if (!@interface.IsOpenGeneric())
            {
                AddConcretionsThatCouldBeClosed(@interface, concretions, services);
            }
        }

        foreach (var @interface in genericInterfaces)
        {
            var exactMatches = genericConcretions.Where(x => x.CanBeCastTo(@interface)).ToList();
            AddAllConcretionsThatClose(@interface, exactMatches, services, assembliesToScan, limits, cancellationToken);
        }
    }

    [RequiresUnreferencedCode(ScanningNeedsReflection)]
    [RequiresDynamicCode(ScanningNeedsReflection)]
    private static bool IsMatchingWithInterface(Type? handlerType, Type handlerInterface)
    {
        if (handlerType == null || handlerInterface == null)
        {
            return false;
        }

        if (handlerType.IsInterface)
        {
            if (handlerType.GenericTypeArguments.SequenceEqual(handlerInterface.GenericTypeArguments))
            {
                return true;
            }
        }
        else
        {
            return IsMatchingWithInterface(handlerType.GetInterface(handlerInterface.Name), handlerInterface);
        }

        return false;
    }

    [RequiresUnreferencedCode(ScanningNeedsReflection)]
    [RequiresDynamicCode(ScanningNeedsReflection)]
    private static void AddConcretionsThatCouldBeClosed(Type @interface, List<Type> concretions, IServiceCollection services)
    {
        foreach (var type in concretions
                     .Where(x => x.IsOpenGeneric() && x.CouldCloseTo(@interface)))
        {
            try
            {
                services.TryAddTransient(@interface, type.MakeGenericType(@interface.GenericTypeArguments));
            }
            catch (Exception)
            {
            }
        }
    }

    [RequiresUnreferencedCode(ScanningNeedsReflection)]
    [RequiresDynamicCode(ScanningNeedsReflection)]
    private static (Type Service, Type Implementation) GetConcreteRegistrationTypes(Type openRequestHandlerInterface, Type concreteGenericTRequest, Type openRequestHandlerImplementation)
    {
        var closingTypes = concreteGenericTRequest.GetGenericArguments();

        var concreteTResponse = concreteGenericTRequest.GetInterfaces()
            .FirstOrDefault(x => x.IsGenericType && x.GetGenericTypeDefinition() == typeof(IRequest<>))
            ?.GetGenericArguments()
            .FirstOrDefault();

        var typeDefinition = openRequestHandlerInterface.GetGenericTypeDefinition();

        var serviceType = concreteTResponse != null ?
            typeDefinition.MakeGenericType(concreteGenericTRequest, concreteTResponse) :
            typeDefinition.MakeGenericType(concreteGenericTRequest);

        return (serviceType, openRequestHandlerImplementation.MakeGenericType(closingTypes));
    }

    [RequiresUnreferencedCode(ScanningNeedsReflection)]
    [RequiresDynamicCode(ScanningNeedsReflection)]
    private static List<Type>? GetConcreteRequestTypes(Type openRequestHandlerInterface, Type openRequestHandlerImplementation, IEnumerable<Assembly> assembliesToScan, Limits limits, CancellationToken cancellationToken)
    {
        //request generic type constraints       
        var constraintsForEachParameter = openRequestHandlerImplementation
            .GetGenericArguments()
            .Select(x => x.GetGenericParameterConstraints())
            .ToList();

        var typesThatCanCloseForEachParameter = constraintsForEachParameter
            .Select(constraints => assembliesToScan
                .SelectMany(assembly => assembly.GetTypes())
                .Where(type => type.IsClass && !type.IsAbstract && constraints.All(constraint => constraint.IsAssignableFrom(type))).ToList()
            ).ToList();

        var requestType = openRequestHandlerInterface.GenericTypeArguments.First();

        if (requestType.IsGenericParameter)
            return null;

        var requestGenericTypeDefinition = requestType.GetGenericTypeDefinition();
              
        var combinations = GenerateCombinations(requestType, typesThatCanCloseForEachParameter, 0, limits, cancellationToken);

        return combinations.Select(types => requestGenericTypeDefinition.MakeGenericType(types.ToArray())).ToList();
    }

    // Method to generate combinations recursively
    public static List<List<Type>> GenerateCombinations(Type requestType, List<List<Type>> lists, int depth = 0, CancellationToken cancellationToken = default)
        => GenerateCombinations(requestType, lists, depth, LimitsForDirectCalls, cancellationToken);

    private static List<List<Type>> GenerateCombinations(Type requestType, List<List<Type>> lists, int depth, Limits limits, CancellationToken cancellationToken)
    {
        if (depth == 0)
        {
            // Initial checks
            if (limits.MaxGenericTypeParameters > 0 && lists.Count > limits.MaxGenericTypeParameters)
                throw new ArgumentException($"Error registering the generic type: {requestType.FullName}. The number of generic type parameters exceeds the maximum allowed ({limits.MaxGenericTypeParameters}).");

            foreach (var list in lists)
            {
                if (limits.MaxTypesClosing > 0 && list.Count > limits.MaxTypesClosing)
                    throw new ArgumentException($"Error registering the generic type: {requestType.FullName}. One of the generic type parameter's count of types that can close exceeds the maximum length allowed ({limits.MaxTypesClosing}).");
            }

            // Calculate the total number of combinations
            long totalCombinations = 1;
            foreach (var list in lists)
            {
                totalCombinations *= list.Count;
                if (limits.MaxGenericTypeParameters > 0 && totalCombinations > limits.MaxGenericTypeRegistrations)
                    throw new ArgumentException($"Error registering the generic type: {requestType.FullName}. The total number of generic type registrations exceeds the maximum allowed ({limits.MaxGenericTypeRegistrations}).");
            }
        }

        if (depth >= lists.Count)
            return new List<List<Type>> { new List<Type>() };

        cancellationToken.ThrowIfCancellationRequested();

        var currentList = lists[depth];
        var childCombinations = GenerateCombinations(requestType, lists, depth + 1, limits, cancellationToken);
        var combinations = new List<List<Type>>();

        foreach (var item in currentList)
        {
            foreach (var childCombination in childCombinations)
            {
                var currentCombination = new List<Type> { item };
                currentCombination.AddRange(childCombination);
                combinations.Add(currentCombination);
            }
        }

        return combinations;
    }

    [RequiresUnreferencedCode(ScanningNeedsReflection)]
    [RequiresDynamicCode(ScanningNeedsReflection)]
    private static void AddAllConcretionsThatClose(Type openRequestInterface, List<Type> concretions, IServiceCollection services, IEnumerable<Assembly> assembliesToScan, Limits limits, CancellationToken cancellationToken)
    {
        foreach (var concretion in concretions)
        {
            var concreteRequests = GetConcreteRequestTypes(openRequestInterface, concretion, assembliesToScan, limits, cancellationToken);

            if (concreteRequests is null)
                continue;

            var registrationTypes = concreteRequests
                .Select(concreteRequest => GetConcreteRegistrationTypes(openRequestInterface, concreteRequest, concretion));

            foreach (var (Service, Implementation) in registrationTypes)
            {
                cancellationToken.ThrowIfCancellationRequested();
                services.AddTransient(Service, Implementation);
            }
        }
    }

    internal static bool CouldCloseTo(this Type openConcretion, Type closedInterface)
    {
        var openInterface = closedInterface.GetGenericTypeDefinition();
        var arguments = closedInterface.GenericTypeArguments;

        var concreteArguments = openConcretion.GenericTypeArguments;
        return arguments.Length == concreteArguments.Length && openConcretion.CanBeCastTo(openInterface);
    }

    private static bool CanBeCastTo(this Type pluggedType, Type pluginType)
    {
        if (pluggedType == null) return false;

        if (pluggedType == pluginType) return true;

        return pluginType.IsAssignableFrom(pluggedType);
    }

    private static bool IsOpenGeneric(this Type type)
    {
        return type.IsGenericTypeDefinition || type.ContainsGenericParameters;
    }

    [RequiresUnreferencedCode(ScanningNeedsReflection)]
    [RequiresDynamicCode(ScanningNeedsReflection)]
    internal static IEnumerable<Type> FindInterfacesThatClose(this Type pluggedType, Type templateType)
    {
        return FindInterfacesThatClosesCore(pluggedType, templateType).Distinct();
    }

    [RequiresUnreferencedCode(ScanningNeedsReflection)]
    [RequiresDynamicCode(ScanningNeedsReflection)]
    private static IEnumerable<Type> FindInterfacesThatClosesCore(Type pluggedType, Type templateType)
    {
        if (pluggedType == null) yield break;

        if (!pluggedType.IsConcrete()) yield break;

        if (templateType.IsInterface)
        {
            foreach (
                var interfaceType in
                pluggedType.GetInterfaces()
                    .Where(type => type.IsGenericType && (type.GetGenericTypeDefinition() == templateType)))
            {
                yield return interfaceType;
            }
        }
        else if (pluggedType.BaseType!.IsGenericType &&
                 (pluggedType.BaseType!.GetGenericTypeDefinition() == templateType))
        {
            yield return pluggedType.BaseType!;
        }

        if (pluggedType.BaseType == typeof(object)) yield break;

        foreach (var interfaceType in FindInterfacesThatClosesCore(pluggedType.BaseType!, templateType))
        {
            yield return interfaceType;
        }
    }

    private static bool IsConcrete(this Type type)
    {
        return !type.IsAbstract && !type.IsInterface;
    }

    private static void Fill<T>(this IList<T> list, T value)
    {
        if (list.Contains(value)) return;
        list.Add(value);
    }

    public static void AddRequiredServices(IServiceCollection services, MediatorServiceConfiguration serviceConfiguration)
    {
        // Use TryAdd, so any existing ServiceFactory/IMediator registration doesn't get overridden
        services.TryAdd(new ServiceDescriptor(typeof(IMediator), serviceConfiguration.MediatorImplementationType, serviceConfiguration.Lifetime));
        services.TryAdd(new ServiceDescriptor(typeof(ISender), sp => sp.GetRequiredService<IMediator>(), serviceConfiguration.Lifetime));
        services.TryAdd(new ServiceDescriptor(typeof(IPublisher), sp => sp.GetRequiredService<IMediator>(), serviceConfiguration.Lifetime));

        var notificationPublisherServiceDescriptor = serviceConfiguration.NotificationPublisherType != null
            ? new ServiceDescriptor(typeof(INotificationPublisher), serviceConfiguration.NotificationPublisherType, serviceConfiguration.Lifetime)
            : new ServiceDescriptor(typeof(INotificationPublisher), serviceConfiguration.NotificationPublisher);

        services.TryAdd(notificationPublisherServiceDescriptor);

        // Handlers added one by one, after the ones found by scanning: a request keeps its first handler, and a
        // handler that scanning has already registered is not registered twice.
        foreach (var serviceDescriptor in serviceConfiguration.SingleHandlersToRegister)
        {
            services.TryAdd(serviceDescriptor);
        }

        services.TryAddEnumerable(serviceConfiguration.MultipleHandlersToRegister);

        // Register pre processors, then post processors, then behaviors
        if (serviceConfiguration.RequestExceptionActionProcessorStrategy == RequestExceptionActionProcessorStrategy.ApplyForUnhandledExceptions)
        {
            RegisterBehaviorIfImplementationsExist(services, typeof(RequestExceptionActionProcessorBehavior<,>), typeof(IRequestExceptionAction<,>), serviceConfiguration.ClosedExceptionActionBehaviors);
            RegisterBehaviorIfImplementationsExist(services, typeof(RequestExceptionProcessorBehavior<,>), typeof(IRequestExceptionHandler<,,>), serviceConfiguration.ClosedExceptionHandlerBehaviors);
        }
        else
        {
            RegisterBehaviorIfImplementationsExist(services, typeof(RequestExceptionProcessorBehavior<,>), typeof(IRequestExceptionHandler<,,>), serviceConfiguration.ClosedExceptionHandlerBehaviors);
            RegisterBehaviorIfImplementationsExist(services, typeof(RequestExceptionActionProcessorBehavior<,>), typeof(IRequestExceptionAction<,>), serviceConfiguration.ClosedExceptionActionBehaviors);
        }

        if (serviceConfiguration.RequestPreProcessorsToRegister.Any())
        {
            AddBuiltInBehavior(services, typeof(RequestPreProcessorBehavior<,>), serviceConfiguration.ClosedPreProcessorBehaviors);
            services.TryAddEnumerable(serviceConfiguration.RequestPreProcessorsToRegister);
        }

        if (serviceConfiguration.RequestPostProcessorsToRegister.Any())
        {
            AddBuiltInBehavior(services, typeof(RequestPostProcessorBehavior<,>), serviceConfiguration.ClosedPostProcessorBehaviors);
            services.TryAddEnumerable(serviceConfiguration.RequestPostProcessorsToRegister);
        }

        foreach (var serviceDescriptor in serviceConfiguration.BehaviorsToRegister)
        {
            services.TryAddEnumerable(serviceDescriptor);
        }

        foreach (var serviceDescriptor in serviceConfiguration.StreamBehaviorsToRegister)
        {
            services.TryAddEnumerable(serviceDescriptor);
        }
    }

    private static void RegisterBehaviorIfImplementationsExist(IServiceCollection services,
        [DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicConstructors)] Type behaviorType, Type subBehaviorType,
        List<ServiceDescriptor> closedForRegisteredRequests)
    {
        var hasAnyRegistrationsOfSubBehaviorType = services
            .Where(service => !service.IsKeyedService)
            .Any(service => IsRegistrationOf(service, subBehaviorType));

        if (hasAnyRegistrationsOfSubBehaviorType)
        {
            AddBuiltInBehavior(services, behaviorType, closedForRegisteredRequests);
        }
    }

    // With a JIT: the open generic behavior, which the container closes for every request.
    // Under Native AOT: the behavior closed for each request that was added with AddRequestHandler.
    // See MediatorServiceConfiguration.RegisterBuiltInBehaviorsClosed.
    private static void AddBuiltInBehavior(IServiceCollection services,
        [DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicConstructors)] Type openBehaviorType,
        List<ServiceDescriptor> closedForRegisteredRequests)
    {
        if (MediatorServiceConfiguration.RegisterBuiltInBehaviorsClosed)
        {
            services.TryAddEnumerable(closedForRegisteredRequests);
        }
        else
        {
            services.TryAddEnumerable(new ServiceDescriptor(typeof(IPipelineBehavior<,>), openBehaviorType, ServiceLifetime.Transient));
        }
    }

    // A registration counts when its service type is the interface, which is how AddMediator registers these
    // classes, or when its implementation type implements the interface, which also catches a class that the
    // application registered under another service type.
    private static bool IsRegistrationOf(ServiceDescriptor service, Type openInterface)
    {
        var serviceType = service.ServiceType;
        if (serviceType == openInterface || (serviceType.IsGenericType && serviceType.GetGenericTypeDefinition() == openInterface))
        {
            return true;
        }

        return service.ImplementationType is { } implementationType && ImplementsOpenInterface(implementationType, openInterface);
    }

    [UnconditionalSuppressMessage("Trimming", "IL2070",
        Justification = "A registration made through the mediator's own methods is recognised by its service type before this is reached. For any other registration, the interfaces of a type that is in use are kept as long as the interface itself is, and these interfaces are referenced by the behaviors that call them.")]
    private static bool ImplementsOpenInterface(Type implementationType, Type openInterface)
        => implementationType.GetInterfaces().Any(type => type.IsGenericType && type.GetGenericTypeDefinition() == openInterface);

}
