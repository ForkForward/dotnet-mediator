// The containers the benchmarks use. AddLibrary and LibraryConfiguration come from one of the two files in Names/,
// so that the same sources also compile against a package that still has the original registration names.
using System;
using System.IO;
using Microsoft.Extensions.DependencyInjection;

namespace Benchmarks;

public static class Library
{
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
