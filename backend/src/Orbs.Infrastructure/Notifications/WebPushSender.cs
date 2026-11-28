using System.Net;
using System.Security.Cryptography;
using Lib.Net.Http.WebPush;
using Lib.Net.Http.WebPush.Authentication;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Orbs.Application.Abstractions;
using Orbs.Application.Notifications;
using Orbs.Infrastructure.Persistence;
using PushSubscriptionModel = Orbs.Domain.Users.PushSubscription;

namespace Orbs.Infrastructure.Notifications;

/// <summary>A server-wide setting, such as the VAPID keys. Not owned by any user.</summary>
public sealed class ServerSetting
{
    public string Key { get; set; } = string.Empty;

    public string Value { get; set; } = string.Empty;
}

/// <summary>
/// The VAPID key pair that identifies this server to push services. Taken from <c>Orbs:Push:VapidPublicKey</c> and
/// <c>VapidPrivateKey</c> when set; otherwise made once and kept in the database (the private key encrypted).
/// </summary>
internal sealed class VapidKeys(IServiceScopeFactory scopes, IConfiguration configuration, ISecretProtector secrets)
{
    private const string PublicKeySetting = "push.vapid.public";
    private const string PrivateKeySetting = "push.vapid.private";
    private readonly Lazy<(string Public, string Private)> _keys = new(() => Load(scopes, configuration, secrets));

    public string PublicKey => _keys.Value.Public;

    public string PrivateKey => _keys.Value.Private;

    public string Subject => configuration["Orbs:Push:Subject"] is { Length: > 0 } subject
        ? subject
        : configuration["Orbs:PublicUrl"] is { Length: > 0 } url && url.StartsWith("https://", StringComparison.Ordinal) ? url : "https://github.com/stijntersteege/orbs";

    internal static (string Public, string Private) Generate()
    {
        using var key = ECDsa.Create(ECCurve.NamedCurves.nistP256);
        var p = key.ExportParameters(includePrivateParameters: true);
        var publicKey = new byte[65];
        publicKey[0] = 0x04;
        p.Q.X!.CopyTo(publicKey, 1);
        p.Q.Y!.CopyTo(publicKey, 33);
        return (Base64Url(publicKey), Base64Url(p.D!));
    }

    private static (string, string) Load(IServiceScopeFactory scopes, IConfiguration configuration, ISecretProtector secrets)
    {
        if (configuration["Orbs:Push:VapidPublicKey"] is { Length: > 0 } configuredPublic && configuration["Orbs:Push:VapidPrivateKey"] is { Length: > 0 } configuredPrivate)
        {
            return (configuredPublic, configuredPrivate);
        }

        using var scope = scopes.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<OrbsDbContext>();
        var stored = db.ServerSettings.AsNoTracking().Where(s => s.Key == PublicKeySetting || s.Key == PrivateKeySetting).ToDictionary(s => s.Key, s => s.Value);
        if (stored.TryGetValue(PublicKeySetting, out var pub) && stored.TryGetValue(PrivateKeySetting, out var priv))
        {
            return (pub, secrets.Unprotect(priv));
        }

        var (newPublic, newPrivate) = Generate();
        db.ServerSettings.Add(new ServerSetting { Key = PublicKeySetting, Value = newPublic });
        db.ServerSettings.Add(new ServerSetting { Key = PrivateKeySetting, Value = secrets.Protect(newPrivate) });
        db.SaveChanges();
        return (newPublic, newPrivate);
    }

    private static string Base64Url(byte[] bytes) => Convert.ToBase64String(bytes).TrimEnd('=').Replace('+', '-').Replace('/', '_');
}

/// <summary>Web Push (RFC 8030/8291) with VAPID, via Lib.Net.Http.WebPush.</summary>
internal sealed class WebPushSender(IHttpClientFactory httpClientFactory, VapidKeys keys) : IPushSender
{
    public const string HttpClientName = "web-push";

    public string PublicKey => keys.PublicKey;

    public async Task<PushResult> SendAsync(PushSubscriptionModel subscription, string payloadJson, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(subscription);
        var client = new PushServiceClient(httpClientFactory.CreateClient(HttpClientName))
        {
            DefaultAuthentication = new VapidAuthentication(keys.PublicKey, keys.PrivateKey) { Subject = keys.Subject },
        };
        var target = new PushSubscription { Endpoint = subscription.Endpoint };
        target.SetKey(PushEncryptionKeyName.P256DH, subscription.P256dh);
        target.SetKey(PushEncryptionKeyName.Auth, subscription.Auth);
        try
        {
            await client.RequestPushMessageDeliveryAsync(target, new PushMessage(payloadJson) { TimeToLive = 60 * 60 * 24 }, cancellationToken);
            return PushResult.Delivered;
        }
        catch (PushServiceClientException ex) when (ex.StatusCode is HttpStatusCode.Gone or HttpStatusCode.NotFound)
        {
            return PushResult.Gone;
        }
        catch (Exception ex) when (ex is PushServiceClientException or HttpRequestException)
        {
            return PushResult.Failed;
        }
    }
}
