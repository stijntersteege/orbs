using Orbs.Domain.Common;

namespace Orbs.Domain.Tools;

public enum McpTransport { Http, Stdio }

/// <summary>
/// A connected Model Context Protocol server. Every tool it offers becomes a tool orbs can use,
/// with the same per-orb permissions and approvals as built-in tools.
/// </summary>
public sealed class McpServer : OwnedAggregateRoot
{
    public const int MaxNameLength = 60;
    public const int MaxLocationLength = 2_048;

    private McpServer(Guid id, string name, McpTransport transport, DateTimeOffset now) : base(id, now)
    {
        Name = name;
        Transport = transport;
    }

    // For EF Core materialization.
    private McpServer()
    {
        Name = string.Empty;
        Location = string.Empty;
    }

    public string Name { get; private set; }

    public McpTransport Transport { get; private set; }

    /// <summary>The endpoint URL (HTTP) or the command line to start (stdio).</summary>
    public string Location { get; private set; } = string.Empty;

    /// <summary>An encrypted secret sent as the Authorization header (HTTP), such as "Bearer abc".</summary>
    public string? ProtectedAuthorization { get; private set; }

    public bool IsEnabled { get; private set; } = true;

    public string? LastError { get; private set; }

    public bool HasAuthorization => ProtectedAuthorization is not null;

    public static McpServer Create(string name, McpTransport transport, string location, DateTimeOffset now)
    {
        var server = new McpServer(Guid.NewGuid(), Guard.Text(name, nameof(Name), MaxNameLength), transport, now);
        server.Configure(name, transport, location, now);
        return server;
    }

    public void Configure(string name, McpTransport transport, string location, DateTimeOffset now)
    {
        Name = Guard.Text(name, nameof(Name), MaxNameLength);
        Transport = transport;
        Location = transport == McpTransport.Http
            ? Guard.AbsoluteHttpUri(location, nameof(Location))?.ToString() ?? throw new DomainException("Location is required.")
            : Guard.Text(location, nameof(Location), MaxLocationLength);
        Touch(now);
    }

    public void SetProtectedAuthorization(string? protectedValue, DateTimeOffset now)
    {
        ProtectedAuthorization = string.IsNullOrEmpty(protectedValue) ? null : protectedValue;
        Touch(now);
    }

    public void Enable(bool enabled, DateTimeOffset now)
    {
        IsEnabled = enabled;
        Touch(now);
    }

    public void RecordError(string? error, DateTimeOffset now)
    {
        LastError = error is null ? null : error.Length <= 1_000 ? error : error[..1_000];
        Touch(now);
    }
}
