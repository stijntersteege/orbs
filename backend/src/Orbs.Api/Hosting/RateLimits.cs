using System.Globalization;
using System.Threading.RateLimiting;
using Microsoft.AspNetCore.RateLimiting;

namespace Orbs.Api.Hosting;

/// <summary>
/// Limits on endpoints anyone can call without signing in, per client address, so passwords and webhook secrets
/// cannot be guessed at speed. Behind a reverse proxy, set <c>Orbs:TrustForwardedHeaders</c> so each person
/// counts separately instead of everyone sharing the proxy's address.
/// </summary>
internal static class RateLimits
{
    /// <summary>Sign-in, first-run setup, passkey sign-in and password changes.</summary>
    public const string SignIn = "sign-in";

    /// <summary>Webhook calls from other apps.</summary>
    public const string Hooks = "hooks";

    public static IServiceCollection AddOrbsRateLimits(this IServiceCollection services, IConfiguration configuration)
    {
        var signInPerMinute = configuration.GetValue("Orbs:RateLimits:SignInPerMinute", 10);
        var hooksPerMinute = configuration.GetValue("Orbs:RateLimits:HooksPerMinute", 60);

        return services.AddRateLimiter(o =>
        {
            o.RejectionStatusCode = StatusCodes.Status429TooManyRequests;
            o.AddPolicy(SignIn, context => PerClient(context, SignIn, signInPerMinute));
            o.AddPolicy(Hooks, context => PerClient(context, Hooks, hooksPerMinute));
            o.OnRejected = async (rejected, cancellationToken) =>
            {
                var response = rejected.HttpContext.Response;
                if (rejected.Lease.TryGetMetadata(MetadataName.RetryAfter, out var retryAfter))
                {
                    response.Headers.RetryAfter = ((int)Math.Ceiling(retryAfter.TotalSeconds)).ToString(CultureInfo.InvariantCulture);
                }

                await Results.Problem("Too many attempts. Wait a minute and try again.", statusCode: StatusCodes.Status429TooManyRequests)
                    .ExecuteAsync(rejected.HttpContext);
            };
        });
    }

    private static RateLimitPartition<string> PerClient(HttpContext context, string policy, int perMinute) =>
        RateLimitPartition.GetFixedWindowLimiter(
            $"{policy}:{context.Connection.RemoteIpAddress}",
            _ => new FixedWindowRateLimiterOptions { PermitLimit = perMinute, Window = TimeSpan.FromMinutes(1), QueueLimit = 0 });
}
