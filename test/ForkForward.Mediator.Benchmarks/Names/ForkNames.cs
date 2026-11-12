// What the library's registration is called in ForkForward.Mediator 12.7.0 and later.
// The project picks this file or OriginalNames.cs; no other source file names the registration.
global using LibraryConfiguration = Microsoft.Extensions.DependencyInjection.MediatorServiceConfiguration;

using System;
using Microsoft.Extensions.DependencyInjection;

namespace Benchmarks;

public static class LibraryNames
{
    public static IServiceCollection AddLibrary(this IServiceCollection services, Action<LibraryConfiguration> configure)
        => services.AddMediator(configure);
}
