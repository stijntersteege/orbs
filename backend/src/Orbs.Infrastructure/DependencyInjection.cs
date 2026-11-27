using Microsoft.AspNetCore.DataProtection;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Orbs.Application.Abstractions;
using Orbs.Application.Accounts;
using Orbs.Application.Jobs;
using Orbs.Infrastructure.Context;
using Orbs.Infrastructure.Models;
using Orbs.Infrastructure.Persistence;
using Orbs.Infrastructure.Persistence.Repositories;
using Orbs.Infrastructure.Persistence.Sqlite;
using Orbs.Infrastructure.Security;
using Orbs.Infrastructure.Tools;

namespace Orbs.Infrastructure;

public static class DependencyInjection
{
    /// <summary>
    /// Registers the database, repositories, model adapters, context connectors and secret storage.
    /// <c>Database:Provider</c> picks <c>Sqlite</c> (default, zero setup) or <c>Postgres</c>.
    /// </summary>
    public static IServiceCollection AddInfrastructure(this IServiceCollection services, IConfiguration configuration)
    {
        ArgumentNullException.ThrowIfNull(configuration);
        var options = DatabaseOptions.From(configuration);
        services.AddSingleton(options);

        if (options.IsPostgres)
        {
            services.AddDbContext<OrbsDbContext>(db =>
                db.UseNpgsql(options.ConnectionString, npgsql => npgsql.MigrationsHistoryTable("__ef_migrations")));
        }
        else
        {
            // SQLite keeps its own migrations on a subclass; everything else asks for OrbsDbContext.
            services.AddDbContext<SqliteOrbsDbContext>(db => db.UseSqlite(options.ConnectionString));
            services.AddScoped<OrbsDbContext>(sp => sp.GetRequiredService<SqliteOrbsDbContext>());
        }

        services.AddScoped<IUnitOfWork>(sp => sp.GetRequiredService<OrbsDbContext>());
        services.AddScoped<IOrbRepository, OrbRepository>();
        services.AddScoped<IModelProviderRepository, ModelProviderRepository>();
        services.AddScoped<IContextSourceRepository, ContextSourceRepository>();
        services.AddScoped<IConversationRepository, ConversationRepository>();
        services.AddScoped<IJobRepository, JobRepository>();
        services.AddScoped<IUserRepository, UserRepository>();
        services.AddScoped<ISessionRepository, SessionRepository>();
        services.AddScoped<IOwnershipMaintenance, OwnershipMaintenance>();
        services.AddSingleton<IPasswordHasher>(new Pbkdf2PasswordHasher());
        services.AddSingleton<IPasskeyCeremonies, Fido2PasskeyCeremonies>();

        services.AddDataProtection().SetApplicationName("Orbs").PersistKeysToDbContext<OrbsDbContext>();
        services.AddSingleton<ISecretProtector, DataProtectionSecretProtector>();

        services.AddHttpClient(OpenAiCompatibleModelAdapter.HttpClientName, c => c.Timeout = TimeSpan.FromMinutes(5));
        // Fetching on behalf of users goes through the network policy (no private addresses by default).
        services.AddSingleton(NetworkPolicy.From(configuration));
        services.AddHttpClient(UrlConnector.HttpClientName, c =>
            {
                c.Timeout = TimeSpan.FromSeconds(30);
                c.MaxResponseContentBufferSize = SafeHttp.MaxResponseBytes;
                c.DefaultRequestHeaders.UserAgent.ParseAdd("Orbs/0.2.0 (+https://github.com/stijntersteege/orbs)");
            })
            .ConfigurePrimaryHttpMessageHandler(sp => SafeHttp.CreateHandler(sp.GetRequiredService<NetworkPolicy>()));

        // Plugins: add a class here to support a new provider or context kind.
        services.AddSingleton<IModelAdapter, EchoModelAdapter>();
        services.AddSingleton<IModelAdapter, OpenAiCompatibleModelAdapter>();
        services.AddSingleton<IContextConnector, TextConnector>();
        services.AddSingleton<IContextConnector, UrlConnector>();
        services.AddSingleton<ITool, CurrentTimeTool>();
        services.AddSingleton<ITool, CalculatorTool>();

        return services;
    }

    /// <summary>Brings the schema up to date by applying migrations, on PostgreSQL and SQLite alike.</summary>
    public static async Task InitializeDatabaseAsync(this IServiceProvider services, CancellationToken cancellationToken = default)
    {
        await using var scope = services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<OrbsDbContext>();
        if (db is SqliteOrbsDbContext sqlite)
        {
            await SqliteBaseline.EnsureAsync(sqlite, cancellationToken);
        }

        await db.Database.MigrateAsync(cancellationToken);
    }
}

public sealed record DatabaseOptions(string Provider, string ConnectionString)
{
    public bool IsPostgres => Provider.Equals("Postgres", StringComparison.OrdinalIgnoreCase)
                              || Provider.Equals("PostgreSQL", StringComparison.OrdinalIgnoreCase);

    public static DatabaseOptions From(IConfiguration configuration)
    {
        ArgumentNullException.ThrowIfNull(configuration);
        var provider = configuration["Database:Provider"] ?? "Sqlite";
        var connectionString = configuration.GetConnectionString("Orbs") ?? "Data Source=orbs.db";
        return new DatabaseOptions(provider, connectionString);
    }
}
