// What the library's registration is called in MediatR itself and in ForkForward.Mediator 12.6.0-preview.1.
// Only the baseline project compiles this file, when it is pointed at a package with the original names.
global using LibraryConfiguration = Microsoft.Extensions.DependencyInjection.MediatRServiceConfiguration;

using System;
using Microsoft.Extensions.DependencyInjection;

namespace Benchmarks;

public static class LibraryNames
{
    public static IServiceCollection AddLibrary(this IServiceCollection services, Action<LibraryConfiguration> configure)
        => services.AddMediatR(configure);
}
