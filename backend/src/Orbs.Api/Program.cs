using Orbs.Api.Endpoints;
using Orbs.Api.Hosting;
using Orbs.Application;
using Orbs.Application.Demo;
using Orbs.Infrastructure;

var builder = WebApplication.CreateBuilder(args);

builder.Services
    .AddApplication()
    .AddInfrastructure(builder.Configuration)
    .AddOrbsApi(builder.Configuration);

var app = builder.Build();

await app.Services.InitializeDatabaseAsync();
if (app.Configuration.GetValue("Orbs:SeedDemoData", defaultValue: false))
{
    await using var scope = app.Services.CreateAsyncScope();
    await scope.ServiceProvider.GetRequiredService<DemoSeeder>().SeedAsync();
}

app.UseExceptionHandler();
app.UseStatusCodePages();
app.UseCors();
app.UseDefaultFiles();
app.UseStaticFiles();
app.UseMiddleware<ApiTokenMiddleware>();

app.MapOpenApi();
app.MapScalarApiReference();
app.MapHealthChecks("/health");

var api = app.MapGroup("/api");
api.MapOrbEndpoints();
api.MapProviderEndpoints();
api.MapContextEndpoints();
api.MapConversationEndpoints();

// Single-page app: any other path that is not a file goes to the web client, when it is bundled.
app.MapFallbackToFile("index.html");

await app.RunAsync();

/// <summary>Entry point. Public so integration tests can host it.</summary>
public partial class Program;
