using System.Text.Json;
using System.Text.Json.Serialization;
using Orbs.Api.Endpoints;
using Orbs.Application.Accounts;
using Orbs.Infrastructure.Persistence;
using Orbs.Infrastructure.Security;
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
        services.AddOrbsAuth(configuration);

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

    /// <summary>
    /// Sign-in settings. Passkeys are bound to <c>Orbs:PublicUrl</c> (the address people open Orbs on).
    /// OpenID Connect is on when <c>Orbs:Auth:Oidc:Authority</c> and <c>ClientId</c> are set.
    /// </summary>
    private static void AddOrbsAuth(this IServiceCollection services, IConfiguration configuration)
    {
        var publicUrl = Uri.TryCreate(configuration["Orbs:PublicUrl"], UriKind.Absolute, out var url) ? url : new Uri("http://localhost:8080");
        var origins = new HashSet<string>(StringComparer.OrdinalIgnoreCase) { publicUrl.GetLeftPart(UriPartial.Authority) };
        if (publicUrl.IsLoopback)
        {
            // Local development: the Vite dev server and the API's own port.
            origins.UnionWith(["http://localhost:5173", "http://localhost:5080", "http://localhost:8080"]);
        }

        services.AddSingleton(new PasskeyOptions(publicUrl.Host, "Orbs", origins));

        var oidc = configuration.GetSection("Orbs:Auth:Oidc");
        var oidcEnabled = !string.IsNullOrWhiteSpace(oidc["Authority"]) && !string.IsNullOrWhiteSpace(oidc["ClientId"]);
        services.AddSingleton(new AuthSettings(
            OidcEnabled: oidcEnabled,
            OidcDisplayName: oidc["DisplayName"] is { Length: > 0 } name ? name : "Single sign-on",
            OidcCreatesAccounts: oidc.GetValue("CreateAccounts", false)));

        var authentication = services.AddAuthentication(AuthEndpoints.ExternalCookieScheme)
            .AddCookie(AuthEndpoints.ExternalCookieScheme, o =>
            {
                o.Cookie.Name = "orbs_external";
                o.ExpireTimeSpan = TimeSpan.FromMinutes(10);
            });
        if (oidcEnabled)
        {
            authentication.AddOpenIdConnect(AuthEndpoints.OidcScheme, o =>
            {
                o.SignInScheme = AuthEndpoints.ExternalCookieScheme;
                o.Authority = oidc["Authority"];
                o.ClientId = oidc["ClientId"];
                o.ClientSecret = oidc["ClientSecret"];
                o.ResponseType = "code";
                o.UsePkce = true;
                o.CallbackPath = "/api/auth/oidc/callback";
                o.Scope.Clear();
                foreach (var scope in (oidc["Scopes"] ?? "openid profile email").Split(' ', StringSplitOptions.RemoveEmptyEntries))
                {
                    o.Scope.Add(scope);
                }

                o.MapInboundClaims = false;
                o.GetClaimsFromUserInfoEndpoint = true;
                o.RequireHttpsMetadata = !o.Authority!.StartsWith("http://localhost", StringComparison.OrdinalIgnoreCase);
            });
        }
    }

    public static void MapScalarApiReference(this WebApplication app) =>
        ScalarEndpointRouteBuilderExtensions.MapScalarApiReference(app, o => o.WithTitle("Orbs API"));
}
