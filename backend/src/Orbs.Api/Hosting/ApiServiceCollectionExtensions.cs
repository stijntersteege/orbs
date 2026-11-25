using System.Text.Json;
using System.Text.Json.Serialization;
using Orbs.Infrastructure.Persistence;
using Scalar.AspNetCore;

namespace Orbs.Api.Hosting;

internal static class ApiServiceCollectionExtensions
{
    public static IServiceCollection AddOrbsApi(this IServiceCollection services, IConfiguration configuration)
    {
        services.ConfigureHttpJsonOptions(o =>
        {
            o.SerializerOptions.Converters.Add(new JsonStringEnumConverter(JsonNamingPolicy.CamelCase));
            o.SerializerOptions.DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull;
        });

        services.AddProblemDetails();
        services.AddExceptionHandler<ProblemExceptionHandler>();

        services.AddOpenApi(o => o.AddDocumentTransformer((doc, _, _) =>
        {
            doc.Info.Title = "Orbs API";
            doc.Info.Description = "Self-hosted API for AI companions: orbs, model providers, context and chat.";
            doc.Info.Version = "v1";
            return Task.CompletedTask;
        }));

        services.AddHealthChecks().AddDbContextCheck<OrbsDbContext>("database");

        var origins = configuration.GetSection("Orbs:Cors:Origins").Get<string[]>() ?? [];
        services.AddCors(o => o.AddDefaultPolicy(p =>
        {
            if (origins.Length > 0)
            {
                p.WithOrigins(origins).AllowAnyHeader().AllowAnyMethod();
            }
        }));

        return services;
    }

    public static void MapScalarApiReference(this WebApplication app) =>
        ScalarEndpointRouteBuilderExtensions.MapScalarApiReference(app, o => o.WithTitle("Orbs API"));
}
