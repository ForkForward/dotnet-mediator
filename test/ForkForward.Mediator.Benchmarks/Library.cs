// The only source file that knows what the library's registration is called. The baseline project defines
// ORIGINAL_NAMES when it compiles these sources against a package that still has the original names.
#if ORIGINAL_NAMES
global using LibraryConfiguration = Microsoft.Extensions.DependencyInjection.MediatRServiceConfiguration;
#else
global using LibraryConfiguration = Microsoft.Extensions.DependencyInjection.MediatorServiceConfiguration;
#endif

using System;
using System.IO;
using Microsoft.Extensions.DependencyInjection;

namespace Benchmarks;

public static class Library
{
    public static IServiceCollection AddLibrary(this IServiceCollection services, Action<LibraryConfiguration> configure)
#if ORIGINAL_NAMES
        => services.AddMediatR(configure);
#else
        => services.AddMediator(configure);
#endif

    /// <summary>Handlers found by scanning this assembly, and nothing around them.</summary>
    public static IServiceProvider Plain()
    {
        var services = new ServiceCollection();
        services.AddLibrary(cfg => cfg.RegisterServicesFromAssemblyContaining(typeof(Ping)));
        return services.BuildServiceProvider();
    }

    /// <summary>The same handlers behind a behavior, a pre-processor and a post-processor.</summary>
    public static IServiceProvider WithPipeline()
    {
        var services = new ServiceCollection();
        services.AddSingleton(TextWriter.Null);
        services.AddLibrary(cfg =>
        {
            cfg.RegisterServicesFromAssemblyContaining(typeof(Ping));
            cfg.AddOpenBehavior(typeof(GenericPipelineBehavior<,>));
            cfg.AddOpenRequestPreProcessor(typeof(GenericRequestPreProcessor<>));
            cfg.AddOpenRequestPostProcessor(typeof(GenericRequestPostProcessor<,>));
        });
        return services.BuildServiceProvider();
    }
}
