using Microsoft.Extensions.DependencyInjection;
using Orbs.Application.Abstractions;
using Orbs.Application.Accounts;
using Orbs.Application.Channels;
using Orbs.Application.Common;
using Orbs.Application.Context;
using Orbs.Application.Conversations;
using Orbs.Application.Demo;
using Orbs.Application.Jobs;
using Orbs.Application.Memories;
using Orbs.Application.Notifications;
using Orbs.Application.Orbs;
using Orbs.Application.Providers;
using Orbs.Application.Schedules;
using Orbs.Application.Tools;

namespace Orbs.Application;

public static class DependencyInjection
{
    /// <summary>Registers the use cases. Adapters and repositories come from the infrastructure layer.</summary>
    public static IServiceCollection AddApplication(this IServiceCollection services)
    {
        services.AddSingleton(TimeProvider.System);
        services.AddSingleton(typeof(PluginRegistry<>));

        services.AddScoped<CurrentUser>();
        services.AddScoped<AccountService>();
        services.AddScoped<OrbService>();
        services.AddScoped<ProviderService>();
        services.AddScoped<ContextService>();
        services.AddScoped<ChatService>();
        services.AddScoped<ToolCatalog>();
        services.AddScoped<ToolRunner>();
        services.AddScoped<ReplyEngine>();
        services.AddScoped<MemoryService>();
        services.AddScoped<EmbedderResolver>();
        services.AddScoped<ContextIndexer>();
        services.AddScoped<ContextRetriever>();
        services.AddScoped<FileContextService>();
        services.AddScoped<ContextSyncDispatcher>();
        services.AddScoped<IJobHandler, ContextSyncJob>();
        services.AddScoped<NotificationService>();
        services.AddScoped<Voice.VoiceService>();
        services.AddScoped<ChannelService>();
        services.AddSingleton<ChannelRelay>();
        services.AddScoped<IJobHandler, NotificationJob>();
        services.AddScoped<ScheduleService>();
        services.AddScoped<ScheduleDispatcher>();
        services.AddScoped<ScheduleRunner>();
        services.AddScoped<IJobHandler, ScheduleCheckJob>();
        services.AddScoped<IJobHandler, ScheduleRunJob>();
        services.AddScoped<IJobHandler, ContextIndexJob>();
        services.AddScoped<ITool, RememberTool>();
        services.AddScoped<ITool, ForgetTool>();
        services.AddScoped<IToolSource, BuiltInToolSource>();
        services.AddScoped<IToolSource, McpToolSource>();
        services.AddScoped<IToolSource, IntegrationToolSource>();
        services.AddScoped<IntegrationService>();
        services.AddScoped<McpServerService>();
        services.AddSingleton<McpToolCache>();
        services.AddScoped<DemoSeeder>();

        services.AddScoped<JobScheduler>();
        services.AddScoped<Housekeeping>();
        services.AddSingleton<JobRunner>();
        services.AddScoped<IJobHandler, SessionCleanupJob>();
        return services;
    }
}
