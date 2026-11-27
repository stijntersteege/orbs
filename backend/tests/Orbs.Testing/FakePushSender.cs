using Orbs.Application.Notifications;
using Orbs.Domain.Users;

namespace Orbs.Testing;

/// <summary>Collects push messages instead of sending them. Endpoints containing "gone" act unsubscribed.</summary>
public sealed class FakePushSender : IPushSender
{
    public List<(string Endpoint, string Payload)> Sent { get; } = [];

    public string PublicKey => "BTestPublicKey";

    public Task<PushResult> SendAsync(PushSubscription subscription, string payloadJson, CancellationToken cancellationToken = default)
    {
        if (subscription.Endpoint.Contains("gone", StringComparison.Ordinal))
        {
            return Task.FromResult(PushResult.Gone);
        }

        Sent.Add((subscription.Endpoint, payloadJson));
        return Task.FromResult(PushResult.Delivered);
    }
}
