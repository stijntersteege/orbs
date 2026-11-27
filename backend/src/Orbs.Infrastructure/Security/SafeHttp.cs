using System.Net;
using System.Net.Sockets;
using Microsoft.Extensions.Configuration;

namespace Orbs.Infrastructure.Security;

/// <summary>
/// Which addresses Orbs may fetch on behalf of users (web page sources, tools).
/// By default only the public internet: private, loopback, link-local and similar ranges are blocked,
/// so nobody can use Orbs to reach services inside your network (SSRF).
/// </summary>
public sealed record NetworkPolicy(bool AllowPrivateNetworks, IReadOnlyList<string> AllowedHosts, IReadOnlyList<string> BlockedHosts)
{
    public static NetworkPolicy Default { get; } = new(false, [], []);

    /// <summary>Reads <c>Orbs:Network:AllowPrivateNetworks</c>, <c>AllowedHosts</c> and <c>BlockedHosts</c>.</summary>
    public static NetworkPolicy From(IConfiguration configuration)
    {
        ArgumentNullException.ThrowIfNull(configuration);
        var section = configuration.GetSection("Orbs:Network");
        return new NetworkPolicy(
            section.GetValue("AllowPrivateNetworks", false),
            List(section, "AllowedHosts"),
            List(section, "BlockedHosts"));
    }

    /// <summary>A host matches an entry when it is that name or a subdomain of it.</summary>
    public static bool Matches(string host, IReadOnlyList<string> entries) =>
        entries.Any(e => host.Equals(e, StringComparison.OrdinalIgnoreCase)
                         || host.EndsWith("." + e.TrimStart('.'), StringComparison.OrdinalIgnoreCase));

    private static List<string> List(IConfigurationSection section, string key)
    {
        // Accept both a JSON array and a comma-separated value (handy in environment variables).
        var values = section.GetSection(key).Get<string[]>() ?? [];
        if (values.Length == 0 && section[key] is { Length: > 0 } csv)
        {
            values = csv.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        }

        return [.. values.Select(v => v.Trim()).Where(v => v.Length > 0)];
    }
}

/// <summary>An address Orbs refused to fetch.</summary>
public sealed class BlockedAddressException(string message) : HttpRequestException(message);

/// <summary>
/// HTTP handlers that apply a <see cref="NetworkPolicy"/> at connect time. The check runs on the address
/// actually dialled, after DNS, so redirects and DNS rebinding cannot sneak past it.
/// </summary>
public static class SafeHttp
{
    public const long MaxResponseBytes = 10 * 1024 * 1024;

    public static SocketsHttpHandler CreateHandler(NetworkPolicy policy)
    {
        ArgumentNullException.ThrowIfNull(policy);
        return new SocketsHttpHandler
        {
            AllowAutoRedirect = true,
            MaxAutomaticRedirections = 5,
            AutomaticDecompression = DecompressionMethods.All,
            ConnectCallback = (context, cancellationToken) => ConnectAsync(policy, context.DnsEndPoint, cancellationToken),
        };
    }

    /// <summary>Resolves the host, refuses blocked addresses and connects to the first allowed one.</summary>
    public static async ValueTask<Stream> ConnectAsync(NetworkPolicy policy, DnsEndPoint endPoint, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(policy);
        ArgumentNullException.ThrowIfNull(endPoint);
        var host = endPoint.Host.TrimEnd('.');
        if (NetworkPolicy.Matches(host, policy.BlockedHosts))
        {
            throw new BlockedAddressException($"Orbs is not allowed to fetch {host}.");
        }

        var addresses = IPAddress.TryParse(host, out var literal)
            ? [literal]
            : await Dns.GetHostAddressesAsync(host, cancellationToken);
        var trusted = policy.AllowPrivateNetworks || NetworkPolicy.Matches(host, policy.AllowedHosts);
        var allowed = addresses.Where(a => trusted || !IsInternal(a)).ToList();
        if (allowed.Count == 0)
        {
            throw new BlockedAddressException(
                $"Orbs does not fetch addresses on private networks ({host}). An admin can allow this host with Orbs:Network:AllowedHosts.");
        }

        var socket = new Socket(SocketType.Stream, ProtocolType.Tcp) { NoDelay = true };
        try
        {
            await socket.ConnectAsync(allowed[0], endPoint.Port, cancellationToken);
            return new NetworkStream(socket, ownsSocket: true);
        }
        catch
        {
            socket.Dispose();
            throw;
        }
    }

    /// <summary>True for addresses that are not on the public internet.</summary>
    public static bool IsInternal(IPAddress address)
    {
        ArgumentNullException.ThrowIfNull(address);
        if (address.IsIPv4MappedToIPv6)
        {
            address = address.MapToIPv4();
        }

        if (IPAddress.IsLoopback(address) || address.Equals(IPAddress.Any) || address.Equals(IPAddress.IPv6Any) || address.Equals(IPAddress.None))
        {
            return true;
        }

        if (address.AddressFamily == AddressFamily.InterNetwork)
        {
            var b = address.GetAddressBytes();
            return b[0] switch
            {
                0 or 10 or 127 => true,
                100 => b[1] >= 64 && b[1] <= 127,          // carrier-grade NAT
                169 => b[1] == 254,                        // link-local, cloud metadata
                172 => b[1] >= 16 && b[1] <= 31,
                192 => (b[1] == 168) || (b[1] == 0 && (b[2] == 0 || b[2] == 2)) || (b[1] == 88 && b[2] == 99),
                198 => b[1] == 18 || b[1] == 19 || (b[1] == 51 && b[2] == 100),
                203 => b[1] == 0 && b[2] == 113,
                >= 224 => true,                            // multicast and reserved
                _ => false,
            };
        }

        if (address.AddressFamily == AddressFamily.InterNetworkV6)
        {
            var b = address.GetAddressBytes();
            return address.IsIPv6LinkLocal
                   || address.IsIPv6SiteLocal
                   || address.IsIPv6Multicast
                   || (b[0] & 0xFE) == 0xFC                // unique local fc00::/7
                   || (b[0] == 0x20 && b[1] == 0x01 && b[2] == 0x0D && b[3] == 0xB8) // documentation
                   || (b[0] == 0x00 && b[1] == 0x64 && b[2] == 0xFF && b[3] == 0x9B); // NAT64 can reach IPv4 private ranges
        }

        return true;
    }
}
