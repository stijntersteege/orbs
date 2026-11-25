using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Data.Sqlite;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Orbs.Application.Abstractions;
using Orbs.Infrastructure.Models;

namespace Orbs.Testing;

/// <summary>
/// Hosts the real API in memory on a private SQLite database.
/// Each factory gets its own database, so tests never share state.
/// </summary>
public class OrbsApiFactory : WebApplicationFactory<Program>
{
    private readonly SqliteConnection _keepAlive;
    private readonly string _connectionString;

    public OrbsApiFactory()
    {
        _connectionString = $"Data Source=file:orbs-{Guid.NewGuid():N}?mode=memory&cache=shared";
        _keepAlive = new SqliteConnection(_connectionString);
        _keepAlive.Open();
    }

    /// <summary>Optional shared secret for <c>/api</c>; empty means no token is required.</summary>
    public string ApiToken { get; init; } = string.Empty;

    public bool SeedDemoData { get; init; }

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        ArgumentNullException.ThrowIfNull(builder);
        builder.UseEnvironment("Testing");
        builder.UseSetting("Database:Provider", "Sqlite");
        builder.UseSetting("ConnectionStrings:Orbs", _connectionString);
        builder.UseSetting("Orbs:SeedDemoData", SeedDemoData ? "true" : "false");
        builder.UseSetting("Orbs:ApiToken", ApiToken);

        builder.ConfigureTestServices(services =>
        {
            // Instant echo replies keep tests fast.
            services.RemoveAll<IModelAdapter>();
            services.AddSingleton<IModelAdapter>(new EchoModelAdapter { WordDelay = TimeSpan.Zero });
            services.AddSingleton<IModelAdapter, OpenAiCompatibleModelAdapter>();
            services.AddSingleton<IModelAdapter>(new FailingModelAdapter());
        });
    }

    protected override void Dispose(bool disposing)
    {
        base.Dispose(disposing);
        if (disposing)
        {
            _keepAlive.Dispose();
        }
    }
}
