using System.Security.Cryptography;
using System.Text;
using Orbs.Application.Accounts;

namespace Orbs.Api.Hosting;

/// <summary>
/// Signs every <c>/api</c> request in as a user, or answers 401.
/// Accepted credentials, in order: the shared API token (acts as the first admin, for scripts),
/// a session token as a bearer token (mobile app, other clients), or the session cookie (browsers).
/// The sign-in endpoints themselves are open.
/// </summary>
internal sealed class AuthMiddleware(RequestDelegate next, IConfiguration configuration)
{
    public const string SessionCookie = "orbs_session";

    private static readonly string[] OpenPaths =
    [
        "/api/auth/status",
        "/api/auth/setup",
        "/api/auth/sign-in",
        "/api/auth/passkeys/sign-in",
        "/api/auth/oidc",
        "/api/hooks",
    ];

    private readonly byte[]? _apiToken = string.IsNullOrWhiteSpace(configuration["Orbs:ApiToken"])
        ? null
        : Encoding.UTF8.GetBytes(configuration["Orbs:ApiToken"]!);

    public async Task InvokeAsync(HttpContext context, AccountService accounts, CurrentUser currentUser)
    {
        var path = context.Request.Path;
        if (!path.StartsWithSegments("/api") || HttpMethods.IsOptions(context.Request.Method))
        {
            await next(context);
            return;
        }

        var token = ReadToken(context);
        if (token is not null)
        {
            var user = IsApiToken(token)
                ? await accounts.FirstAdminAsync(context.RequestAborted)
                : await accounts.AuthenticateAsync(token, context.RequestAborted);
            if (user is not null)
            {
                currentUser.SignIn(user.Id, user.IsAdmin);
            }
        }

        if (!currentUser.IsSignedIn && !OpenPaths.Any(p => path.StartsWithSegments(p, StringComparison.OrdinalIgnoreCase)))
        {
            context.Response.StatusCode = StatusCodes.Status401Unauthorized;
            context.Response.Headers.WWWAuthenticate = "Bearer";
            return;
        }

        await next(context);
    }

    /// <summary>The bearer token, else the session cookie.</summary>
    public static string? ReadToken(HttpContext context)
    {
        var header = context.Request.Headers.Authorization.ToString();
        const string prefix = "Bearer ";
        if (header.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
        {
            return header[prefix.Length..].Trim();
        }

        return context.Request.Cookies.TryGetValue(SessionCookie, out var cookie) && !string.IsNullOrWhiteSpace(cookie) ? cookie : null;
    }

    private bool IsApiToken(string token) =>
        _apiToken is not null && CryptographicOperations.FixedTimeEquals(Encoding.UTF8.GetBytes(token), _apiToken);
}
