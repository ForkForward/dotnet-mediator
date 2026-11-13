using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Linq;
using System.Reflection;
using System.Runtime.CompilerServices;
using ForkForward.Mediator;
using ForkForward.Mediator.Entities;
using ForkForward.Mediator.NotificationPublishers;
using ForkForward.Mediator.Pipeline;
using ForkForward.Mediator.Registration;
using ForkForward.Mediator.Wrappers;

namespace Microsoft.Extensions.DependencyInjection;

public class MediatorServiceConfiguration
{
    // What the trimmer has to keep of a type that is registered as an implementation and inspected for the
    // interfaces it implements.
    private const DynamicallyAccessedMemberTypes ConstructorsAndInterfaces =
        DynamicallyAccessedMemberTypes.PublicConstructors | DynamicallyAccessedMemberTypes.Interfaces;

    private const string ScanningNeedsReflection =
        "Scanning an assembly finds handlers by reflection, which trimming and Native AOT cannot follow. Register each handler with AddRequestHandler, AddNotificationHandler and their siblings instead.";

    /// <summary>
    /// Optional filter for types to register. Default value is a function returning true.
    /// </summary>
    public Func<Type, bool> TypeEvaluator { get; set; } = t => true;
    
    /// <summary>
    /// Mediator implementation type to register. Default is <see cref="Mediator"/>
    /// </summary>
    [DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicConstructors)]
    public Type MediatorImplementationType { get; set; } = typeof(Mediator);

    /// <summary>
    /// Strategy for publishing notifications. Defaults to <see cref="ForeachAwaitPublisher"/>
    /// </summary>
    public INotificationPublisher NotificationPublisher { get; set; } = new ForeachAwaitPublisher();

    /// <summary>
    /// Type of notification publisher strategy to register. If set, overrides <see cref="NotificationPublisher"/>
    /// </summary>
    [DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicConstructors)]
    public Type? NotificationPublisherType { get; set; }

    /// <summary>
    /// Service lifetime to register services under. Default value is <see cref="ServiceLifetime.Transient"/>
    /// </summary>
    public ServiceLifetime Lifetime { get; set; } = ServiceLifetime.Transient;

    /// <summary>
    /// Request exception action processor strategy. Default value is <see cref="DependencyInjection.RequestExceptionActionProcessorStrategy.ApplyForUnhandledExceptions"/>
    /// </summary>
    public RequestExceptionActionProcessorStrategy RequestExceptionActionProcessorStrategy { get; set; }
        = RequestExceptionActionProcessorStrategy.ApplyForUnhandledExceptions;

    internal List<Assembly> AssembliesToRegister { get; } = new();

    /// <summary>
    /// Request and stream request handlers added one by one. A request has one handler: the first registration wins.
    /// </summary>
    internal List<ServiceDescriptor> SingleHandlersToRegister { get; } = new();

    /// <summary>
    /// Notification handlers, exception handlers and exception actions added one by one. There can be any number of them.
    /// </summary>
    internal List<ServiceDescriptor> MultipleHandlersToRegister { get; } = new();

    /// <summary>
    /// The built-in behaviors, closed over each request that was registered one by one.
    /// Only filled and only used under Native AOT; see <see cref="RegisterBuiltInBehaviorsClosed"/>.
    /// </summary>
    internal List<ServiceDescriptor> ClosedExceptionActionBehaviors { get; } = new();
    internal List<ServiceDescriptor> ClosedExceptionHandlerBehaviors { get; } = new();
    internal List<ServiceDescriptor> ClosedPreProcessorBehaviors { get; } = new();
    internal List<ServiceDescriptor> ClosedPostProcessorBehaviors { get; } = new();

    /// <summary>
    /// The behaviors that run pre-processors, post-processors, exception handlers and exception actions are generic
    /// in the request and the response. With a JIT they are registered open, and the container closes them for
    /// whatever request arrives. Under Native AOT the container cannot close an open generic type over a value
    /// type, and a request without a response has the response type <see cref="Unit"/>, a value type. So there
    /// they are registered closed, for each request added with AddRequestHandler, where both types are type
    /// arguments the compiler has seen.
    /// </summary>
    internal static bool RegisterBuiltInBehaviorsClosed => !RuntimeFeature.IsDynamicCodeSupported;

    /// <summary>
    /// List of behaviors to register in specific order
    /// </summary>
    public List<ServiceDescriptor> BehaviorsToRegister { get; } = new();

    /// <summary>
    /// List of stream behaviors to register in specific order
    /// </summary>
    public List<ServiceDescriptor> StreamBehaviorsToRegister { get; } = new();

    /// <summary>
    /// List of request pre processors to register in specific order
    /// </summary>
    public List<ServiceDescriptor> RequestPreProcessorsToRegister { get; } = new();

    /// <summary>
    /// List of request post processors to register in specific order
    /// </summary>
    public List<ServiceDescriptor> RequestPostProcessorsToRegister { get; } = new();

    /// <summary>
    /// Automatically register processors during assembly scanning
    /// </summary>
    public bool AutoRegisterRequestProcessors { get; set; }

    /// <summary>
    /// Configure the maximum number of type parameters that a generic request handler can have. To Disable this constraint, set the value to 0.
    /// </summary>
    public int MaxGenericTypeParameters { get; set; } = 10;

    /// <summary>
    /// Configure the maximum number of types that can close a generic request type parameter constraint.  To Disable this constraint, set the value to 0.
    /// </summary>
    public int MaxTypesClosing { get; set; } = 100;

    /// <summary>
    /// Configure the Maximum Amount of Generic RequestHandler Types ForkForward.Mediator will try to register.  To Disable this constraint, set the value to 0.
    /// </summary>
    public int MaxGenericTypeRegistrations { get; set; } = 125000;

    /// <summary>
    /// Configure the Timeout in Milliseconds that the GenericHandler Registration Process will exit with error.  To Disable this constraint, set the value to 0.
    /// </summary>
    public int RegistrationTimeout { get; set; } = 15000;

    /// <summary>
    /// Flag that controlls whether ForkForward.Mediator will attempt to register handlers that containg generic type parameters.
    /// </summary>
    public bool RegisterGenericHandlers { get; set; } = false;

    /// <summary>
    /// Register various handlers from assembly containing given type
    /// </summary>
    /// <typeparam name="T">Type from assembly to scan</typeparam>
    /// <returns>This</returns>
    [RequiresUnreferencedCode(ScanningNeedsReflection)]
    [RequiresDynamicCode(ScanningNeedsReflection)]
    public MediatorServiceConfiguration RegisterServicesFromAssemblyContaining<T>()
        => RegisterServicesFromAssemblyContaining(typeof(T));

    /// <summary>
    /// Register various handlers from assembly containing given type
    /// </summary>
    /// <param name="type">Type from assembly to scan</param>
    /// <returns>This</returns>
    [RequiresUnreferencedCode(ScanningNeedsReflection)]
    [RequiresDynamicCode(ScanningNeedsReflection)]
    public MediatorServiceConfiguration RegisterServicesFromAssemblyContaining(Type type)
        => RegisterServicesFromAssembly(type.Assembly);

    /// <summary>
    /// Register various handlers from assembly
    /// </summary>
    /// <param name="assembly">Assembly to scan</param>
    /// <returns>This</returns>
    [RequiresUnreferencedCode(ScanningNeedsReflection)]
    [RequiresDynamicCode(ScanningNeedsReflection)]
    public MediatorServiceConfiguration RegisterServicesFromAssembly(Assembly assembly)
    {
        AssembliesToRegister.Add(assembly);

        return this;
    }

    /// <summary>
    /// Register various handlers from assemblies
    /// </summary>
    /// <param name="assemblies">Assemblies to scan</param>
    /// <returns>This</returns>
    [RequiresUnreferencedCode(ScanningNeedsReflection)]
    [RequiresDynamicCode(ScanningNeedsReflection)]
    public MediatorServiceConfiguration RegisterServicesFromAssemblies(
        params Assembly[] assemblies)
    {
        AssembliesToRegister.AddRange(assemblies);

        return this;
    }

    /// <summary>
    /// Register a closed behavior type
    /// </summary>
    /// <typeparam name="TServiceType">Closed behavior interface type</typeparam>
    /// <typeparam name="TImplementationType">Closed behavior implementation type</typeparam>
    /// <param name="serviceLifetime">Optional service lifetime, defaults to <see cref="ServiceLifetime.Transient"/>.</param>
    /// <returns>This</returns>
    public MediatorServiceConfiguration AddBehavior<TServiceType, [DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicConstructors)] TImplementationType>(ServiceLifetime serviceLifetime = ServiceLifetime.Transient)
        => AddBehavior(typeof(TServiceType), typeof(TImplementationType), serviceLifetime);

    /// <summary>
    /// Register a closed behavior type against all <see cref="IPipelineBehavior{TRequest,TResponse}"/> implementations
    /// </summary>
    /// <typeparam name="TImplementationType">Closed behavior implementation type</typeparam>
    /// <param name="serviceLifetime">Optional service lifetime, defaults to <see cref="ServiceLifetime.Transient"/>.</param>
    /// <returns>This</returns>
    public MediatorServiceConfiguration AddBehavior<[DynamicallyAccessedMembers(ConstructorsAndInterfaces)] TImplementationType>(ServiceLifetime serviceLifetime = ServiceLifetime.Transient)
    {
        return AddBehavior(typeof(TImplementationType), serviceLifetime);
    }

    /// <summary>
    /// Register a closed behavior type against all <see cref="IPipelineBehavior{TRequest,TResponse}"/> implementations
    /// </summary>
    /// <param name="implementationType">Closed behavior implementation type</param>
    /// <param name="serviceLifetime">Optional service lifetime, defaults to <see cref="ServiceLifetime.Transient"/>.</param>
    /// <returns>This</returns>
    public MediatorServiceConfiguration AddBehavior([DynamicallyAccessedMembers(ConstructorsAndInterfaces)] Type implementationType, ServiceLifetime serviceLifetime = ServiceLifetime.Transient)
    {
        var implementedGenericInterfaces = ClosedInterfaces(implementationType, typeof(IPipelineBehavior<,>)).ToList();

        if (implementedGenericInterfaces.Count == 0)
        {
            throw new InvalidOperationException($"{implementationType.Name} must implement {typeof(IPipelineBehavior<,>).FullName}");
        }

        foreach (var implementedBehaviorType in implementedGenericInterfaces)
        {
            BehaviorsToRegister.Add(new ServiceDescriptor(implementedBehaviorType, implementationType, serviceLifetime));
        }

        return this;
    }

    /// <summary>
    /// Register a closed behavior type
    /// </summary>
    /// <param name="serviceType">Closed behavior interface type</param>
    /// <param name="implementationType">Closed behavior implementation type</param>
    /// <param name="serviceLifetime">Optional service lifetime, defaults to <see cref="ServiceLifetime.Transient"/>.</param>
    /// <returns>This</returns>
    public MediatorServiceConfiguration AddBehavior(Type serviceType, [DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicConstructors)] Type implementationType, ServiceLifetime serviceLifetime = ServiceLifetime.Transient)
    {
        BehaviorsToRegister.Add(new ServiceDescriptor(serviceType, implementationType, serviceLifetime));

        return this;
    }

    /// <summary>
    /// Registers an open behavior type against the <see cref="IPipelineBehavior{TRequest,TResponse}"/> open generic interface type
    /// </summary>
    /// <param name="openBehaviorType">An open generic behavior type</param>
    /// <param name="serviceLifetime">Optional service lifetime, defaults to <see cref="ServiceLifetime.Transient"/>.</param>
    /// <returns>This</returns>
    public MediatorServiceConfiguration AddOpenBehavior([DynamicallyAccessedMembers(ConstructorsAndInterfaces)] Type openBehaviorType, ServiceLifetime serviceLifetime = ServiceLifetime.Transient)
    {
        if (!openBehaviorType.IsGenericType)
        {
            throw new InvalidOperationException($"{openBehaviorType.Name} must be generic");
        }

        var implementedGenericInterfaces = openBehaviorType.GetInterfaces().Where(i => i.IsGenericType).Select(i => i.GetGenericTypeDefinition());
        var implementedOpenBehaviorInterfaces = new HashSet<Type>(implementedGenericInterfaces.Where(i => i == typeof(IPipelineBehavior<,>)));

        if (implementedOpenBehaviorInterfaces.Count == 0)
        {
            throw new InvalidOperationException($"{openBehaviorType.Name} must implement {typeof(IPipelineBehavior<,>).FullName}");
        }

        foreach (var openBehaviorInterface in implementedOpenBehaviorInterfaces)
        {
            BehaviorsToRegister.Add(new ServiceDescriptor(openBehaviorInterface, openBehaviorType, serviceLifetime));
        }

        return this;
    }

    /// <summary>
    /// Registers multiple open behavior types against the <see cref="IPipelineBehavior{TRequest,TResponse}"/> open generic interface type
    /// </summary>
    /// <param name="openBehaviorTypes">An open generic behavior type list includes multiple open generic behavior types.</param>
    /// <param name="serviceLifetime">Optional service lifetime, defaults to <see cref="ServiceLifetime.Transient"/>.</param>
    /// <returns>This</returns>
    [RequiresUnreferencedCode("The trimmer cannot see the types in a list. Call AddOpenBehavior for each type, or pass OpenBehavior objects.")]
    public MediatorServiceConfiguration AddOpenBehaviors(IEnumerable<Type> openBehaviorTypes, ServiceLifetime serviceLifetime = ServiceLifetime.Transient)
    {
        foreach (var openBehaviorType in openBehaviorTypes)
        {
            AddOpenBehavior(openBehaviorType, serviceLifetime);
        }

        return this;
    }

    /// <summary>
    /// Registers open behaviors against the <see cref="IPipelineBehavior{TRequest,TResponse}"/> open generic interface type
    /// </summary>
    /// <param name="openBehaviors">An open generic behavior list includes multiple <see cref="OpenBehavior"/> open generic behaviors.</param>
    /// <returns>This</returns>
    public MediatorServiceConfiguration AddOpenBehaviors(IEnumerable<OpenBehavior> openBehaviors)
    {
        foreach (var openBehavior in openBehaviors)
        {
            AddOpenBehavior(openBehavior.OpenBehaviorType!, openBehavior.ServiceLifetime);
        }

        return this;
    }
    
    /// <summary>
    /// Register a closed stream behavior type
    /// </summary>
    /// <typeparam name="TServiceType">Closed stream behavior interface type</typeparam>
    /// <typeparam name="TImplementationType">Closed stream behavior implementation type</typeparam>
    /// <param name="serviceLifetime">Optional service lifetime, defaults to <see cref="ServiceLifetime.Transient"/>.</param>
    /// <returns>This</returns>
    public MediatorServiceConfiguration AddStreamBehavior<TServiceType, [DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicConstructors)] TImplementationType>(ServiceLifetime serviceLifetime = ServiceLifetime.Transient)
        => AddStreamBehavior(typeof(TServiceType), typeof(TImplementationType), serviceLifetime);
    
    /// <summary>
    /// Register a closed stream behavior type
    /// </summary>
    /// <param name="serviceType">Closed stream behavior interface type</param>
    /// <param name="implementationType">Closed stream behavior implementation type</param>
    /// <param name="serviceLifetime">Optional service lifetime, defaults to <see cref="ServiceLifetime.Transient"/>.</param>
    /// <returns>This</returns>
    public MediatorServiceConfiguration AddStreamBehavior(Type serviceType, [DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicConstructors)] Type implementationType, ServiceLifetime serviceLifetime = ServiceLifetime.Transient)
    {
        StreamBehaviorsToRegister.Add(new ServiceDescriptor(serviceType, implementationType, serviceLifetime));

        return this;
    }
    
    /// <summary>
    /// Register a closed stream behavior type against all <see cref="IStreamPipelineBehavior{TRequest,TResponse}"/> implementations
    /// </summary>
    /// <typeparam name="TImplementationType">Closed stream behavior implementation type</typeparam>
    /// <param name="serviceLifetime">Optional service lifetime, defaults to <see cref="ServiceLifetime.Transient"/>.</param>
    /// <returns>This</returns>
    public MediatorServiceConfiguration AddStreamBehavior<[DynamicallyAccessedMembers(ConstructorsAndInterfaces)] TImplementationType>(ServiceLifetime serviceLifetime = ServiceLifetime.Transient)
        => AddStreamBehavior(typeof(TImplementationType), serviceLifetime);
    
    /// <summary>
    /// Register a closed stream behavior type against all <see cref="IStreamPipelineBehavior{TRequest,TResponse}"/> implementations
    /// </summary>
    /// <param name="implementationType">Closed stream behavior implementation type</param>
    /// <param name="serviceLifetime">Optional service lifetime, defaults to <see cref="ServiceLifetime.Transient"/>.</param>
    /// <returns>This</returns>
    public MediatorServiceConfiguration AddStreamBehavior([DynamicallyAccessedMembers(ConstructorsAndInterfaces)] Type implementationType, ServiceLifetime serviceLifetime = ServiceLifetime.Transient)
    {
        var implementedGenericInterfaces = ClosedInterfaces(implementationType, typeof(IStreamPipelineBehavior<,>)).ToList();

        if (implementedGenericInterfaces.Count == 0)
        {
            throw new InvalidOperationException($"{implementationType.Name} must implement {typeof(IStreamPipelineBehavior<,>).FullName}");
        }

        foreach (var implementedBehaviorType in implementedGenericInterfaces)
        {
            StreamBehaviorsToRegister.Add(new ServiceDescriptor(implementedBehaviorType, implementationType, serviceLifetime));
        }

        return this;
    }
    
    /// <summary>
    /// Registers an open stream behavior type against the <see cref="IStreamPipelineBehavior{TRequest,TResponse}"/> open generic interface type
    /// </summary>
    /// <param name="openBehaviorType">An open generic stream behavior type</param>
    /// <param name="serviceLifetime">Optional service lifetime, defaults to <see cref="ServiceLifetime.Transient"/>.</param>
    /// <returns>This</returns>
    public MediatorServiceConfiguration AddOpenStreamBehavior([DynamicallyAccessedMembers(ConstructorsAndInterfaces)] Type openBehaviorType, ServiceLifetime serviceLifetime = ServiceLifetime.Transient)
    {
        if (!openBehaviorType.IsGenericType)
        {
            throw new InvalidOperationException($"{openBehaviorType.Name} must be generic");
        }

        var implementedGenericInterfaces = openBehaviorType.GetInterfaces().Where(i => i.IsGenericType).Select(i => i.GetGenericTypeDefinition());
        var implementedOpenBehaviorInterfaces = new HashSet<Type>(implementedGenericInterfaces.Where(i => i == typeof(IStreamPipelineBehavior<,>)));

        if (implementedOpenBehaviorInterfaces.Count == 0)
        {
            throw new InvalidOperationException($"{openBehaviorType.Name} must implement {typeof(IStreamPipelineBehavior<,>).FullName}");
        }

        foreach (var openBehaviorInterface in implementedOpenBehaviorInterfaces)
        {
            StreamBehaviorsToRegister.Add(new ServiceDescriptor(openBehaviorInterface, openBehaviorType, serviceLifetime));
        }

        return this;
    }

    /// <summary>
    /// Register a closed request pre processor type
    /// </summary>
    /// <typeparam name="TServiceType">Closed request pre processor interface type</typeparam>
    /// <typeparam name="TImplementationType">Closed request pre processor implementation type</typeparam>
    /// <param name="serviceLifetime">Optional service lifetime, defaults to <see cref="ServiceLifetime.Transient"/>.</param>
    /// <returns>This</returns>
    public MediatorServiceConfiguration AddRequestPreProcessor<TServiceType, [DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicConstructors)] TImplementationType>(ServiceLifetime serviceLifetime = ServiceLifetime.Transient)
        => AddRequestPreProcessor(typeof(TServiceType), typeof(TImplementationType), serviceLifetime);
    
    /// <summary>
    /// Register a closed request pre processor type
    /// </summary>
    /// <param name="serviceType">Closed request pre processor interface type</param>
    /// <param name="implementationType">Closed request pre processor implementation type</param>
    /// <param name="serviceLifetime">Optional service lifetime, defaults to <see cref="ServiceLifetime.Transient"/>.</param>
    /// <returns>This</returns>
    public MediatorServiceConfiguration AddRequestPreProcessor(Type serviceType, [DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicConstructors)] Type implementationType, ServiceLifetime serviceLifetime = ServiceLifetime.Transient)
    {
        RequestPreProcessorsToRegister.Add(new ServiceDescriptor(serviceType, implementationType, serviceLifetime));

        return this;
    }

    /// <summary>
    /// Register a closed request pre processor type against all <see cref="IRequestPreProcessor{TRequest}"/> implementations
    /// </summary>
    /// <typeparam name="TImplementationType">Closed request pre processor implementation type</typeparam>
    /// <param name="serviceLifetime">Optional service lifetime, defaults to <see cref="ServiceLifetime.Transient"/>.</param>
    /// <returns>This</returns>
    public MediatorServiceConfiguration AddRequestPreProcessor<[DynamicallyAccessedMembers(ConstructorsAndInterfaces)] TImplementationType>(
        ServiceLifetime serviceLifetime = ServiceLifetime.Transient)
        => AddRequestPreProcessor(typeof(TImplementationType), serviceLifetime);

    /// <summary>
    /// Register a closed request pre processor type against all <see cref="IRequestPreProcessor{TRequest}"/> implementations
    /// </summary>
    /// <param name="implementationType">Closed request pre processor implementation type</param>
    /// <param name="serviceLifetime">Optional service lifetime, defaults to <see cref="ServiceLifetime.Transient"/>.</param>
    /// <returns>This</returns>
    public MediatorServiceConfiguration AddRequestPreProcessor([DynamicallyAccessedMembers(ConstructorsAndInterfaces)] Type implementationType, ServiceLifetime serviceLifetime = ServiceLifetime.Transient)
    {
        var implementedGenericInterfaces = ClosedInterfaces(implementationType, typeof(IRequestPreProcessor<>)).ToList();

        if (implementedGenericInterfaces.Count == 0)
        {
            throw new InvalidOperationException($"{implementationType.Name} must implement {typeof(IRequestPreProcessor<>).FullName}");
        }

        foreach (var implementedPreProcessorType in implementedGenericInterfaces)
        {
            RequestPreProcessorsToRegister.Add(new ServiceDescriptor(implementedPreProcessorType, implementationType, serviceLifetime));
        }
        
        return this;
    }
    
    /// <summary>
    /// Registers an open request pre processor type against the <see cref="IRequestPreProcessor{TRequest}"/> open generic interface type
    /// </summary>
    /// <param name="openBehaviorType">An open generic request pre processor type</param>
    /// <param name="serviceLifetime">Optional service lifetime, defaults to <see cref="ServiceLifetime.Transient"/>.</param>
    /// <returns>This</returns>
    public MediatorServiceConfiguration AddOpenRequestPreProcessor([DynamicallyAccessedMembers(ConstructorsAndInterfaces)] Type openBehaviorType, ServiceLifetime serviceLifetime = ServiceLifetime.Transient)
    {
        if (!openBehaviorType.IsGenericType)
        {
            throw new InvalidOperationException($"{openBehaviorType.Name} must be generic");
        }

        var implementedGenericInterfaces = openBehaviorType.GetInterfaces().Where(i => i.IsGenericType).Select(i => i.GetGenericTypeDefinition());
        var implementedOpenBehaviorInterfaces = new HashSet<Type>(implementedGenericInterfaces.Where(i => i == typeof(IRequestPreProcessor<>)));

        if (implementedOpenBehaviorInterfaces.Count == 0)
        {
            throw new InvalidOperationException($"{openBehaviorType.Name} must implement {typeof(IRequestPreProcessor<>).FullName}");
        }

        foreach (var openBehaviorInterface in implementedOpenBehaviorInterfaces)
        {
            RequestPreProcessorsToRegister.Add(new ServiceDescriptor(openBehaviorInterface, openBehaviorType, serviceLifetime));
        }

        return this;
    }
    
    /// <summary>
    /// Register a closed request post processor type
    /// </summary>
    /// <typeparam name="TServiceType">Closed request post processor interface type</typeparam>
    /// <typeparam name="TImplementationType">Closed request post processor implementation type</typeparam>
    /// <param name="serviceLifetime">Optional service lifetime, defaults to <see cref="ServiceLifetime.Transient"/>.</param>
    /// <returns>This</returns>
    public MediatorServiceConfiguration AddRequestPostProcessor<TServiceType, [DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicConstructors)] TImplementationType>(ServiceLifetime serviceLifetime = ServiceLifetime.Transient)
        => AddRequestPostProcessor(typeof(TServiceType), typeof(TImplementationType), serviceLifetime);
    
    /// <summary>
    /// Register a closed request post processor type
    /// </summary>
    /// <param name="serviceType">Closed request post processor interface type</param>
    /// <param name="implementationType">Closed request post processor implementation type</param>
    /// <param name="serviceLifetime">Optional service lifetime, defaults to <see cref="ServiceLifetime.Transient"/>.</param>
    /// <returns>This</returns>
    public MediatorServiceConfiguration AddRequestPostProcessor(Type serviceType, [DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicConstructors)] Type implementationType, ServiceLifetime serviceLifetime = ServiceLifetime.Transient)
    {
        RequestPostProcessorsToRegister.Add(new ServiceDescriptor(serviceType, implementationType, serviceLifetime));

        return this;
    }
 
    /// <summary>
    /// Register a closed request post processor type against all <see cref="IRequestPostProcessor{TRequest,TResponse}"/> implementations
    /// </summary>
    /// <typeparam name="TImplementationType">Closed request post processor implementation type</typeparam>
    /// <param name="serviceLifetime">Optional service lifetime, defaults to <see cref="ServiceLifetime.Transient"/>.</param>
    /// <returns>This</returns>
    public MediatorServiceConfiguration AddRequestPostProcessor<[DynamicallyAccessedMembers(ConstructorsAndInterfaces)] TImplementationType>(ServiceLifetime serviceLifetime = ServiceLifetime.Transient)
        => AddRequestPostProcessor(typeof(TImplementationType), serviceLifetime);
    
    /// <summary>
    /// Register a closed request post processor type against all <see cref="IRequestPostProcessor{TRequest,TResponse}"/> implementations
    /// </summary>
    /// <param name="implementationType">Closed request post processor implementation type</param>
    /// <param name="serviceLifetime">Optional service lifetime, defaults to <see cref="ServiceLifetime.Transient"/>.</param>
    /// <returns>This</returns>
    public MediatorServiceConfiguration AddRequestPostProcessor([DynamicallyAccessedMembers(ConstructorsAndInterfaces)] Type implementationType, ServiceLifetime serviceLifetime = ServiceLifetime.Transient)
    {
        var implementedGenericInterfaces = ClosedInterfaces(implementationType, typeof(IRequestPostProcessor<,>)).ToList();

        if (implementedGenericInterfaces.Count == 0)
        {
            throw new InvalidOperationException($"{implementationType.Name} must implement {typeof(IRequestPostProcessor<,>).FullName}");
        }

        foreach (var implementedPostProcessorType in implementedGenericInterfaces)
        {
            RequestPostProcessorsToRegister.Add(new ServiceDescriptor(implementedPostProcessorType, implementationType, serviceLifetime));
        }
        return this;
    }
    
    /// <summary>
    /// Registers an open request post processor type against the <see cref="IRequestPostProcessor{TRequest,TResponse}"/> open generic interface type
    /// </summary>
    /// <param name="openBehaviorType">An open generic request post processor type</param>
    /// <param name="serviceLifetime">Optional service lifetime, defaults to <see cref="ServiceLifetime.Transient"/>.</param>
    /// <returns>This</returns>
    public MediatorServiceConfiguration AddOpenRequestPostProcessor([DynamicallyAccessedMembers(ConstructorsAndInterfaces)] Type openBehaviorType, ServiceLifetime serviceLifetime = ServiceLifetime.Transient)
    {
        if (!openBehaviorType.IsGenericType)
        {
            throw new InvalidOperationException($"{openBehaviorType.Name} must be generic");
        }

        var implementedGenericInterfaces = openBehaviorType.GetInterfaces().Where(i => i.IsGenericType).Select(i => i.GetGenericTypeDefinition());
        var implementedOpenBehaviorInterfaces = new HashSet<Type>(implementedGenericInterfaces.Where(i => i == typeof(IRequestPostProcessor<,>)));

        if (implementedOpenBehaviorInterfaces.Count == 0)
        {
            throw new InvalidOperationException($"{openBehaviorType.Name} must implement {typeof(IRequestPostProcessor<,>).FullName}");
        }

        foreach (var openBehaviorInterface in implementedOpenBehaviorInterfaces)
        {
            RequestPostProcessorsToRegister.Add(new ServiceDescriptor(openBehaviorInterface, openBehaviorType, serviceLifetime));
        }

        return this;
    }

    /// <summary>
    /// Registers the handler of a request that has a response, without scanning an assembly.
    /// Nothing here uses reflection, so it also works in trimmed applications and under Native AOT.
    /// </summary>
    /// <typeparam name="TRequest">Request type</typeparam>
    /// <typeparam name="TResponse">Response type</typeparam>
    /// <typeparam name="THandler">Handler type</typeparam>
    /// <returns>This</returns>
    public MediatorServiceConfiguration AddRequestHandler<TRequest, TResponse, [DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicConstructors)] THandler>()
        where TRequest : IRequest<TResponse>
        where THandler : class, IRequestHandler<TRequest, TResponse>
    {
        WrapperCache.AddRequest<TRequest, TResponse>();
        SingleHandlersToRegister.Add(ServiceDescriptor.Transient<IRequestHandler<TRequest, TResponse>, THandler>());
        AddClosedBuiltInBehaviors<TRequest, TResponse>();

        return this;
    }

    /// <summary>
    /// Registers the handler of a request that has no response, without scanning an assembly.
    /// Nothing here uses reflection, so it also works in trimmed applications and under Native AOT.
    /// </summary>
    /// <typeparam name="TRequest">Request type</typeparam>
    /// <typeparam name="THandler">Handler type</typeparam>
    /// <returns>This</returns>
    public MediatorServiceConfiguration AddRequestHandler<TRequest, [DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicConstructors)] THandler>()
        where TRequest : IRequest
        where THandler : class, IRequestHandler<TRequest>
    {
        WrapperCache.AddRequest<TRequest>();
        SingleHandlersToRegister.Add(ServiceDescriptor.Transient<IRequestHandler<TRequest>, THandler>());
        AddClosedBuiltInBehaviors<TRequest, Unit>();

        return this;
    }

    /// <summary>
    /// Registers a handler of a notification, without scanning an assembly. A notification can have any number of handlers.
    /// Nothing here uses reflection, so it also works in trimmed applications and under Native AOT.
    /// </summary>
    /// <typeparam name="TNotification">Notification type</typeparam>
    /// <typeparam name="THandler">Handler type</typeparam>
    /// <returns>This</returns>
    public MediatorServiceConfiguration AddNotificationHandler<TNotification, [DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicConstructors)] THandler>()
        where TNotification : INotification
        where THandler : class, INotificationHandler<TNotification>
    {
        WrapperCache.AddNotification<TNotification>();
        MultipleHandlersToRegister.Add(ServiceDescriptor.Transient<INotificationHandler<TNotification>, THandler>());

        return this;
    }

    /// <summary>
    /// Registers the handler of a stream request, without scanning an assembly.
    /// Nothing here uses reflection, so it also works in trimmed applications and under Native AOT.
    /// </summary>
    /// <typeparam name="TRequest">Stream request type</typeparam>
    /// <typeparam name="TResponse">Type of the items of the stream</typeparam>
    /// <typeparam name="THandler">Handler type</typeparam>
    /// <returns>This</returns>
    public MediatorServiceConfiguration AddStreamRequestHandler<TRequest, TResponse, [DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicConstructors)] THandler>()
        where TRequest : IStreamRequest<TResponse>
        where THandler : class, IStreamRequestHandler<TRequest, TResponse>
    {
        WrapperCache.AddStreamRequest<TRequest, TResponse>();
        SingleHandlersToRegister.Add(ServiceDescriptor.Transient<IStreamRequestHandler<TRequest, TResponse>, THandler>());

        return this;
    }

    /// <summary>
    /// Registers a handler for exceptions of one type thrown while one request is handled, without scanning an assembly.
    /// Nothing here uses reflection, so it also works in trimmed applications and under Native AOT.
    /// </summary>
    /// <typeparam name="TRequest">Request type</typeparam>
    /// <typeparam name="TResponse">Response type; <see cref="Unit"/> for a request without a response</typeparam>
    /// <typeparam name="TException">Exception type</typeparam>
    /// <typeparam name="THandler">Exception handler type</typeparam>
    /// <returns>This</returns>
    public MediatorServiceConfiguration AddRequestExceptionHandler<TRequest, TResponse, TException, [DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicConstructors)] THandler>()
        where TRequest : notnull
        where TException : Exception
        where THandler : class, IRequestExceptionHandler<TRequest, TResponse, TException>
    {
        MultipleHandlersToRegister.Add(ServiceDescriptor.Transient<IRequestExceptionHandler<TRequest, TResponse, TException>, THandler>());
        MultipleHandlersToRegister.Add(ServiceDescriptor.Transient<IRegisteredRequestExceptionHandlers<TRequest, TResponse>, RegisteredRequestExceptionHandlers<TRequest, TResponse, TException>>());

        return this;
    }

    /// <summary>
    /// Registers an action for exceptions of one type thrown while one request is handled, without scanning an assembly.
    /// Nothing here uses reflection, so it also works in trimmed applications and under Native AOT.
    /// </summary>
    /// <typeparam name="TRequest">Request type</typeparam>
    /// <typeparam name="TException">Exception type</typeparam>
    /// <typeparam name="TAction">Exception action type</typeparam>
    /// <returns>This</returns>
    public MediatorServiceConfiguration AddRequestExceptionAction<TRequest, TException, [DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicConstructors)] TAction>()
        where TRequest : notnull
        where TException : Exception
        where TAction : class, IRequestExceptionAction<TRequest, TException>
    {
        MultipleHandlersToRegister.Add(ServiceDescriptor.Transient<IRequestExceptionAction<TRequest, TException>, TAction>());
        MultipleHandlersToRegister.Add(ServiceDescriptor.Transient<IRegisteredRequestExceptionActions<TRequest>, RegisteredRequestExceptionActions<TRequest, TException>>());

        return this;
    }

    private void AddClosedBuiltInBehaviors<TRequest, TResponse>()
        where TRequest : notnull
    {
        if (!RegisterBuiltInBehaviorsClosed)
        {
            return;
        }

        ClosedExceptionActionBehaviors.Add(ServiceDescriptor.Transient<IPipelineBehavior<TRequest, TResponse>, RequestExceptionActionProcessorBehavior<TRequest, TResponse>>());
        ClosedExceptionHandlerBehaviors.Add(ServiceDescriptor.Transient<IPipelineBehavior<TRequest, TResponse>, RequestExceptionProcessorBehavior<TRequest, TResponse>>());
        ClosedPreProcessorBehaviors.Add(ServiceDescriptor.Transient<IPipelineBehavior<TRequest, TResponse>, RequestPreProcessorBehavior<TRequest, TResponse>>());
        ClosedPostProcessorBehaviors.Add(ServiceDescriptor.Transient<IPipelineBehavior<TRequest, TResponse>, RequestPostProcessorBehavior<TRequest, TResponse>>());
    }

    // The closed interfaces of templateType that a concrete type implements: what ServiceRegistrar's
    // FindInterfacesThatClose returns for an interface template, written so that the trimmer can follow it.
    private static IEnumerable<Type> ClosedInterfaces(
        [DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.Interfaces)] Type type, Type templateType)
    {
        if (type.IsAbstract || type.IsInterface)
        {
            return Enumerable.Empty<Type>();
        }

        return type.GetInterfaces()
            .Where(interfaceType => interfaceType.IsGenericType && interfaceType.GetGenericTypeDefinition() == templateType)
            .Distinct();
    }
}
