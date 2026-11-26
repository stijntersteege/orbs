using Microsoft.Extensions.DependencyInjection;
using Orbs.Application.Common;
using Orbs.Application.Context;
using Orbs.Application.Conversations;
using Orbs.Application.Demo;
using Orbs.Application.Orbs;
using Orbs.Application.Providers;

namespace Orbs.Application;

public static class DependencyInjection
{
    /// <summary>Registers the use cases. Adapters and repositories come from the infrastructure layer.</summary>
    public static IServiceCollection AddApplication(this IServiceCollection services)
    {
        services.AddSingleton(TimeProvider.System);
        services.AddSingleton(typeof(PluginRegistry<>));

        services.AddScoped<OrbService>();
        services.AddScoped<ProviderService>();
        services.AddScoped<ContextService>();
        services.AddScoped<ChatService>();
        services.AddScoped<DemoSeeder>();
        return services;
    }
}
