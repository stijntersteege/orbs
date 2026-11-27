using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Data.Sqlite;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Orbs.Application.Abstractions;
using Orbs.Application.Accounts;
using Orbs.Application.Jobs;
using Orbs.Application.Tools;
using Orbs.Infrastructure.Models;
using Orbs.Infrastructure.Security;

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

    /// <summary>The in-memory MCP client every MCP server talks to in tests.</summary>
    public FakeMcpClient McpClient { get; } = new();

    public const string OwnerEmail = "owner@orbs.test";
    public const string Password = "correct horse battery";

    private bool _anonymous;

    /// <summary>A client that is not signed in.</summary>
    public HttpClient CreateAnonymousClient()
    {
        _anonymous = true;
        try
        {
            return CreateClient();
        }
        finally
        {
            _anonymous = false;
        }
    }

    /// <summary>Puts a job in the queue as a user (or as the system when <paramref name="ownerId"/> is null).</summary>
    public async Task<Guid> EnqueueAsync(string kind, object payload, Guid? ownerId)
    {
        await using var scope = Services.CreateAsyncScope();
        scope.ServiceProvider.GetRequiredService<CurrentUser>().ActAsSystem();
        var job = await scope.ServiceProvider.GetRequiredService<JobScheduler>().EnqueueAsync(kind, payload, ownerId: ownerId);
        return job.Id;
    }

    /// <summary>Runs whatever background work is due, as the worker would.</summary>
    public Task<int> RunJobsAsync() => Services.GetRequiredService<JobRunner>().RunDueAsync();

    /// <summary>A client signed in as a second, non-admin account, created by the owner.</summary>
    public async Task<HttpClient> CreateClientForNewUserAsync(string email)
    {
        var owner = new ApiClient(CreateClient());
        await ApiClient.ReadAsync<UserDto>(await owner.Http.PostAsJsonAsync("/api/users", new CreateUserRequest(email, null, Password), ApiClient.Json));
        var client = CreateAnonymousClient();
        await SignInAsync(client, email);
        return client;
    }

    /// <summary>Every client is signed in as the owner (the first account), unless asked otherwise.</summary>
    protected override void ConfigureClient(HttpClient client)
    {
        base.ConfigureClient(client);
        ArgumentNullException.ThrowIfNull(client);
        if (!_anonymous)
        {
            // Sign in on a separate client: this one may not send anything before it is fully set up.
            client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", OwnerTokenAsync().GetAwaiter().GetResult());
        }
    }

    private async Task<string> OwnerTokenAsync()
    {
        using var raw = Server.CreateClient();
        var setup = await raw.PostAsJsonAsync("/api/auth/setup", new SetupRequest(OwnerEmail, "Owner", Password), ApiClient.Json);
        if (setup.StatusCode != HttpStatusCode.Conflict)
        {
            return (await ApiClient.ReadAsync<SignInResult>(setup)).Token;
        }

        var signIn = await raw.PostAsJsonAsync("/api/auth/sign-in", new SignInRequest(OwnerEmail, Password), ApiClient.Json);
        return (await ApiClient.ReadAsync<SignInResult>(signIn)).Token;
    }

    private static async Task SignInAsync(HttpClient client, string email)
    {
        var response = await client.PostAsJsonAsync("/api/auth/sign-in", new SignInRequest(email, Password), ApiClient.Json);
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", (await ApiClient.ReadAsync<SignInResult>(response)).Token);
    }

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        ArgumentNullException.ThrowIfNull(builder);
        builder.UseEnvironment("Testing");
        builder.UseSetting("Database:Provider", "Sqlite");
        builder.UseSetting("ConnectionStrings:Orbs", _connectionString);
        builder.UseSetting("Orbs:SeedDemoData", SeedDemoData ? "true" : "false");
        builder.UseSetting("Orbs:ApiToken", ApiToken);
        builder.UseSetting("Orbs:Jobs:Enabled", "false"); // tests run jobs by hand with JobRunner

        builder.ConfigureTestServices(services =>
        {
            // Instant echo replies keep tests fast.
            services.RemoveAll<IModelAdapter>();
            services.AddSingleton<IModelAdapter>(new EchoModelAdapter { WordDelay = TimeSpan.Zero });
            services.AddSingleton<IModelAdapter, OpenAiCompatibleModelAdapter>();
            services.AddSingleton<IModelAdapter>(new FailingModelAdapter());

            services.RemoveAll<IMcpClient>();
            services.AddSingleton<IMcpClient>(McpClient);
            services.AddScoped<IJobHandler, EchoJobHandler>();
            services.AddScoped<IJobHandler, FailingJobHandler>();

            // Fewer hashing rounds keep sign-in fast in tests.
            services.RemoveAll<IPasswordHasher>();
            services.AddSingleton<IPasswordHasher>(new Pbkdf2PasswordHasher(iterations: 1_000));
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
