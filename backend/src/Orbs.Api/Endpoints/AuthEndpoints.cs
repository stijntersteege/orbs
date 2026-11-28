using System.Security.Claims;
using Microsoft.AspNetCore.Authentication;
using Orbs.Api.Hosting;
using Orbs.Application.Accounts;

namespace Orbs.Api.Endpoints;

internal static class AuthEndpoints
{
    public const string OidcScheme = "oidc";
    public const string ExternalCookieScheme = "orbs-external";

    public static void MapAuthEndpoints(this IEndpointRouteBuilder api)
    {
        var auth = api.MapGroup("/auth").WithTags("Sign-in");

        auth.MapGet("/status", (AccountService accounts, CancellationToken ct) => accounts.StatusAsync(ct))
            .WithSummary("How people can sign in, and whether the first account still has to be created");

        auth.MapPost("/setup", async (SetupRequest request, HttpContext http, AccountService accounts, CancellationToken ct) =>
                SignedIn(http, await accounts.SetupAsync(request, Device(http), ct)))
            .RequireRateLimiting(RateLimits.SignIn)
            .WithSummary("Create the first account (an admin). Only works once");

        auth.MapPost("/sign-in", async (SignInRequest request, HttpContext http, AccountService accounts, CancellationToken ct) =>
                SignedIn(http, await accounts.SignInWithPasswordAsync(request, Device(http), ct)))
            .RequireRateLimiting(RateLimits.SignIn)
            .WithSummary("Sign in with email and password");

        auth.MapPost("/passkeys/sign-in/options", (AccountService accounts) => accounts.BeginPasskeySignIn())
            .RequireRateLimiting(RateLimits.SignIn)
            .WithSummary("Start signing in with a passkey: WebAuthn request options");

        auth.MapPost("/passkeys/sign-in", async (CompletePasskeyRequest request, HttpContext http, AccountService accounts, CancellationToken ct) =>
                SignedIn(http, await accounts.CompletePasskeySignInAsync(request, Device(http), ct)))
            .RequireRateLimiting(RateLimits.SignIn)
            .WithSummary("Finish signing in with a passkey");

        auth.MapPost("/sign-out", async (HttpContext http, AccountService accounts, CancellationToken ct) =>
            {
                if (AuthMiddleware.ReadToken(http) is { } token)
                {
                    await accounts.SignOutAsync(token, ct);
                }

                http.Response.Cookies.Delete(AuthMiddleware.SessionCookie, CookieOptions(http));
                return TypedResults.NoContent();
            })
            .WithSummary("Sign out this device");

        auth.MapGet("/oidc/start", (string? returnTo, AuthSettings settings) =>
                settings.OidcEnabled
                    ? Results.Challenge(new AuthenticationProperties { RedirectUri = $"/api/auth/oidc/complete?returnTo={Uri.EscapeDataString(SafeReturn(returnTo))}" }, [OidcScheme])
                    : Results.NotFound())
            .WithSummary("Sign in with the configured OpenID Connect provider (redirects)");

        auth.MapGet("/oidc/complete", async (string? returnTo, HttpContext http, AccountService accounts, CancellationToken ct) =>
            {
                var result = await http.AuthenticateAsync(ExternalCookieScheme);
                await http.SignOutAsync(ExternalCookieScheme);
                if (!result.Succeeded || result.Principal is null)
                {
                    return Results.Redirect("/?signInError=oidc");
                }

                var identity = ToIdentity(result.Principal);
                try
                {
                    SignedIn(http, await accounts.SignInWithExternalAsync(identity, Device(http), ct));
                }
                catch (ForbiddenException)
                {
                    return Results.Redirect("/?signInError=no-account");
                }

                return Results.Redirect(SafeReturn(returnTo));
            })
            .ExcludeFromDescription();

        var account = api.MapGroup("/account").WithTags("Account");

        account.MapGet("/", (HttpContext http, AccountService accounts, CancellationToken ct) => accounts.GetAccountAsync(AuthMiddleware.ReadToken(http), ct))
            .WithSummary("The signed-in user, their passkeys and their sessions");

        account.MapPut("/", (UpdateProfileRequest request, AccountService accounts, CancellationToken ct) => accounts.UpdateProfileAsync(request, ct))
            .WithSummary("Change the display name");

        account.MapPost("/password", async (ChangePasswordRequest request, AccountService accounts, CancellationToken ct) =>
            {
                await accounts.ChangePasswordAsync(request, ct);
                return TypedResults.NoContent();
            })
            .RequireRateLimiting(RateLimits.SignIn)
            .WithSummary("Set or change the password");

        account.MapDelete("/sessions/{id:guid}", async (Guid id, AccountService accounts, CancellationToken ct) =>
            {
                await accounts.RevokeSessionAsync(id, ct);
                return TypedResults.NoContent();
            })
            .WithSummary("Sign out another device");

        account.MapPost("/passkeys/options", (AccountService accounts, CancellationToken ct) => accounts.BeginPasskeyRegistrationAsync(ct))
            .WithSummary("Start adding a passkey: WebAuthn creation options");

        account.MapPost("/passkeys", (CompletePasskeyRequest request, AccountService accounts, CancellationToken ct) => accounts.CompletePasskeyRegistrationAsync(request, ct))
            .WithSummary("Finish adding a passkey");

        account.MapDelete("/passkeys/{id:guid}", async (Guid id, AccountService accounts, CancellationToken ct) =>
            {
                await accounts.RemovePasskeyAsync(id, ct);
                return TypedResults.NoContent();
            })
            .WithSummary("Remove a passkey");

        var users = api.MapGroup("/users").WithTags("Users");

        users.MapGet("/", (AccountService accounts, CancellationToken ct) => accounts.ListUsersAsync(ct))
            .WithSummary("List every account (admins only)");

        users.MapPost("/", async (CreateUserRequest request, AccountService accounts, CancellationToken ct) =>
            {
                var user = await accounts.CreateUserAsync(request, ct);
                return TypedResults.Created($"/api/users/{user.Id}", user);
            })
            .WithSummary("Create an account (admins only)");

        users.MapDelete("/{id:guid}", async (Guid id, AccountService accounts, CancellationToken ct) =>
            {
                await accounts.DeleteUserAsync(id, ct);
                return TypedResults.NoContent();
            })
            .WithSummary("Delete an account and everything it owns (admins only)");
    }

    private static SignInResult SignedIn(HttpContext http, SignInResult result)
    {
        var options = CookieOptions(http);
        options.Expires = result.ExpiresAt;
        http.Response.Cookies.Append(AuthMiddleware.SessionCookie, result.Token, options);
        return result;
    }

    private static CookieOptions CookieOptions(HttpContext http) => new()
    {
        HttpOnly = true,
        // Behind a proxy that ends TLS, the request itself may look like plain http.
        Secure = http.Request.IsHttps || http.RequestServices.GetRequiredService<PublicAddress>().IsHttps,
        SameSite = SameSiteMode.Lax,
        Path = "/",
    };

    private static string Device(HttpContext http) => http.Request.Headers.UserAgent.ToString();

    /// <summary>Only same-site paths, so the sign-in flow cannot be used to redirect anywhere else.</summary>
    private static string SafeReturn(string? returnTo) =>
        returnTo is { Length: > 0 } && returnTo.StartsWith('/') && !returnTo.StartsWith("//", StringComparison.Ordinal) && !returnTo.Contains('\\', StringComparison.Ordinal)
            ? returnTo
            : "/";

    private static ExternalIdentity ToIdentity(ClaimsPrincipal principal)
    {
        string? Find(params string[] types) => types.Select(principal.FindFirstValue).FirstOrDefault(v => !string.IsNullOrWhiteSpace(v));
        var subject = Find("sub", ClaimTypes.NameIdentifier) ?? throw new UnauthorizedException("The sign-in provider did not say who you are.");
        var issuer = principal.FindFirst(c => c.Type is "sub" or ClaimTypes.NameIdentifier)?.Issuer ?? "oidc";
        var verified = string.Equals(Find("email_verified"), "true", StringComparison.OrdinalIgnoreCase);
        return new ExternalIdentity(issuer, subject, Find("email", ClaimTypes.Email), verified, Find("name", ClaimTypes.Name, "preferred_username"));
    }
}
