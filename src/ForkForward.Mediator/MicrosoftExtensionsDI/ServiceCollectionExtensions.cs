using System.Diagnostics.CodeAnalysis;
using System;
using System.Linq;
using ForkForward.Mediator;
using ForkForward.Mediator.Pipeline;
using ForkForward.Mediator.Registration;

namespace Microsoft.Extensions.DependencyInjection;

/// <summary>
/// Extensions to scan for ForkForward.Mediator handlers and registers them.
/// - Scans for any handler interface implementations and registers them as <see cref="ServiceLifetime.Transient"/>
/// - Scans for any <see cref="IRequestPreProcessor{TRequest}"/> and <see cref="IRequestPostProcessor{TRequest,TResponse}"/> implementations and registers them as transient instances
/// Registers <see cref="IMediator"/> as a transient instance
/// After calling AddMediator you can use the container to resolve an <see cref="IMediator"/> instance.
/// This does not scan for any <see cref="IPipelineBehavior{TRequest,TResponse}"/> instances including <see cref="RequestPreProcessorBehavior{TRequest,TResponse}"/> and <see cref="RequestPreProcessorBehavior{TRequest,TResponse}"/>.
/// To register behaviors, use the <see cref="ServiceCollectionServiceExtensions.AddTransient(IServiceCollection,Type,Type)"/> with the open generic or closed generic types.
/// </summary>
public static class ServiceCollectionExtensions
{
    /// <summary>
    /// Registers handlers and mediator types from the specified assemblies
    /// </summary>
    /// <param name="services">Service collection</param>
    /// <param name="configuration">The action used to configure the options</param>
    /// <returns>Service collection</returns>
    public static IServiceCollection AddMediator(this IServiceCollection services, 
        Action<MediatorServiceConfiguration> configuration)
    {
        var serviceConfig = new MediatorServiceConfiguration();

        configuration.Invoke(serviceConfig);

        return services.AddMediator(serviceConfig);
    }
    
    /// <summary>
    /// Registers handlers and mediator types from the specified assemblies
    /// </summary>
    /// <param name="services">Service collection</param>
    /// <param name="configuration">Configuration options</param>
    /// <returns>Service collection</returns>
    public static IServiceCollection AddMediator(this IServiceCollection services, 
        MediatorServiceConfiguration configuration)
    {
        if (!configuration.AssembliesToRegister.Any()
            && configuration.SingleHandlersToRegister.Count == 0
            && configuration.MultipleHandlersToRegister.Count == 0)
        {
            throw new ArgumentException("No assemblies found to scan. Supply at least one assembly to scan for handlers, or register handlers one by one with AddRequestHandler, AddNotificationHandler and their siblings.");
        }

        if (configuration.AssembliesToRegister.Any())
        {
            ScanAssemblies(services, configuration);
        }

        ServiceRegistrar.AddRequiredServices(services, configuration);

        return services;
    }

    [UnconditionalSuppressMessage("Trimming", "IL2026",
        Justification = "Only reached when assemblies were added with RegisterServicesFromAssembly and its siblings. Those methods carry RequiresUnreferencedCode, so the application has already been told.")]
    [UnconditionalSuppressMessage("AOT", "IL3050",
        Justification = "Only reached when assemblies were added with RegisterServicesFromAssembly and its siblings. Those methods carry RequiresDynamicCode, so the application has already been told.")]
    private static void ScanAssemblies(IServiceCollection services, MediatorServiceConfiguration configuration)
    {
        ServiceRegistrar.SetGenericRequestHandlerRegistrationLimitations(configuration);

        ServiceRegistrar.AddMediatorClassesWithTimeout(services, configuration);
    }
}
