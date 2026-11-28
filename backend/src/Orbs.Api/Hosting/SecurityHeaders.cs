namespace Orbs.Api.Hosting;

/// <summary>
/// Browser protections on every response: a strict Content Security Policy for the web client, no framing,
/// no MIME sniffing, no referrer leaks, and only the device features Orbs uses (the microphone, for voice).
/// </summary>
internal static class SecurityHeaders
{
    /// <summary>
    /// Scripts, styles and connections only from Orbs itself. Inline styles stay allowed because the orb faces are
    /// drawn with them; inline scripts are not. Images and audio may also be data: or blob: URLs the app makes.
    /// </summary>
    public const string ContentSecurityPolicy =
        "default-src 'self'; " +
        "script-src 'self'; " +
        "style-src 'self' 'unsafe-inline'; " +
        "img-src 'self' data: blob:; " +
        "media-src 'self' blob:; " +
        "font-src 'self' data:; " +
        "connect-src 'self'; " +
        "worker-src 'self'; " +
        "manifest-src 'self'; " +
        "object-src 'none'; " +
        "base-uri 'self'; " +
        "form-action 'self'; " +
        "frame-ancestors 'none'";

    public static IApplicationBuilder UseSecurityHeaders(this IApplicationBuilder app) =>
        app.Use(async (context, next) =>
        {
            context.Response.OnStarting(() =>
            {
                var headers = context.Response.Headers;
                headers.XContentTypeOptions = "nosniff";
                headers.XFrameOptions = "DENY";
                headers["Referrer-Policy"] = "no-referrer";
                headers["Permissions-Policy"] = "microphone=(self), camera=(), geolocation=(), payment=(), usb=()";
                headers["Cross-Origin-Opener-Policy"] = "same-origin";

                // The API reference page loads its viewer from a CDN, so it keeps the browser defaults.
                if (!context.Request.Path.StartsWithSegments("/scalar"))
                {
                    headers.ContentSecurityPolicy = ContentSecurityPolicy;
                }

                if (context.Request.IsHttps)
                {
                    headers.StrictTransportSecurity = "max-age=31536000";
                }

                return Task.CompletedTask;
            });
            await next(context);
        });
}
