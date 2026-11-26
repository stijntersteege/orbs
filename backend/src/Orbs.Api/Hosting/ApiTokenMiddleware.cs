using System.Security.Cryptography;
using System.Text;

namespace Orbs.Api.Hosting;

/// <summary>
/// Optional shared-secret protection for <c>/api</c>. Off when <c>Orbs:ApiToken</c> is empty,
/// which suits a server that only listens on a private network. Set it before exposing Orbs publicly.
/// </summary>
internal sealed class ApiTokenMiddleware(RequestDelegate next, IConfiguration configuration)
{
    private readonly byte[]? _token = string.IsNullOrWhiteSpace(configuration["Orbs:ApiToken"])
        ? null
        : Encoding.UTF8.GetBytes(configuration["Orbs:ApiToken"]!);

    public async Task InvokeAsync(HttpContext context)
    {
        if (_token is null || !context.Request.Path.StartsWithSegments("/api") || HttpMethods.IsOptions(context.Request.Method))
        {
            await next(context);
            return;
        }

        var header = context.Request.Headers.Authorization.ToString();
        const string prefix = "Bearer ";
        var presented = header.StartsWith(prefix, StringComparison.OrdinalIgnoreCase)
            ? Encoding.UTF8.GetBytes(header[prefix.Length..].Trim())
            : [];

        if (!CryptographicOperations.FixedTimeEquals(presented, _token))
        {
            context.Response.StatusCode = StatusCodes.Status401Unauthorized;
            context.Response.Headers.WWWAuthenticate = "Bearer";
            return;
        }

        await next(context);
    }
}
