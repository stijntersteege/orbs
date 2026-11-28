using System.Collections.Concurrent;
using System.Text;
using System.Text.RegularExpressions;
using Orbs.Application.Abstractions;
using Orbs.Application.Accounts;
using Orbs.Application.Common;
using Orbs.Domain.Common;
using Orbs.Domain.Tools;

namespace Orbs.Application.Tools;

public interface IMcpServerRepository : IRepository<McpServer>;

/// <summary>How to reach an MCP server. The authorization value is already decrypted.</summary>
public sealed record McpConnection(Guid ServerId, McpTransport Transport, string Location, string? Authorization, bool RestrictNetwork);

/// <summary>A tool as an MCP server describes it.</summary>
public sealed record McpToolInfo(string Name, string? Title, string Description, string InputSchema, bool ReadOnly);

/// <summary>Talks the Model Context Protocol. One call per operation; connections are not kept open.</summary>
public interface IMcpClient
{
    Task<IReadOnlyList<McpToolInfo>> ListToolsAsync(McpConnection connection, CancellationToken cancellationToken = default);

    Task<string> CallToolAsync(McpConnection connection, string tool, string arguments, CancellationToken cancellationToken = default);
}

/// <summary>Whether people may start MCP servers as local processes. Off by default: it runs commands on the server.</summary>
public sealed record McpSettings(bool AllowStdio = false);

public sealed record McpServerDto(
    Guid Id,
    string Name,
    McpTransport Transport,
    string Location,
    bool HasAuthorization,
    bool IsEnabled,
    string? LastError,
    DateTimeOffset CreatedAt);

/// <summary>Adds or changes an MCP server. On update, a null authorization keeps the stored one and "" removes it.</summary>
public sealed record SaveMcpServerRequest(string Name, McpTransport Transport, string Location, string? Authorization = null, bool IsEnabled = true);

public sealed record McpServerToolsDto(IReadOnlyList<ToolDto> Tools, string? Error);

/// <summary>Use cases for connecting MCP servers.</summary>
public sealed class McpServerService(
    IMcpServerRepository servers,
    IMcpClient client,
    ISecretProtector secrets,
    McpToolCache cache,
    McpSettings settings,
    CurrentUser currentUser,
    IUnitOfWork unitOfWork,
    TimeProvider clock)
{
    public async Task<IReadOnlyList<McpServerDto>> ListAsync(CancellationToken cancellationToken = default) =>
        [.. (await servers.ListAsync(cancellationToken)).OrderBy(s => s.CreatedAt).Select(ToDto)];

    public async Task<McpServerDto> CreateAsync(SaveMcpServerRequest request, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        EnsureAllowed(request.Transport);
        var now = clock.GetUtcNow();
        var server = McpServer.Create(request.Name, request.Transport, request.Location, now);
        Apply(server, request, now);
        servers.Add(server);
        await unitOfWork.SaveChangesAsync(cancellationToken);
        return ToDto(server);
    }

    public async Task<McpServerDto> UpdateAsync(Guid id, SaveMcpServerRequest request, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        EnsureAllowed(request.Transport);
        var server = await LoadAsync(id, cancellationToken);
        var now = clock.GetUtcNow();
        server.Configure(request.Name, request.Transport, request.Location, now);
        Apply(server, request, now);
        await unitOfWork.SaveChangesAsync(cancellationToken);
        cache.Forget(id);
        return ToDto(server);
    }

    public async Task DeleteAsync(Guid id, CancellationToken cancellationToken = default)
    {
        servers.Remove(await LoadAsync(id, cancellationToken));
        await unitOfWork.SaveChangesAsync(cancellationToken);
        cache.Forget(id);
    }

    /// <summary>Connects and lists the server's tools, remembering any error for the app.</summary>
    public async Task<McpServerToolsDto> ToolsAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var server = await LoadAsync(id, cancellationToken);
        cache.Forget(id);
        try
        {
            var tools = await client.ListToolsAsync(ConnectionFor(server), cancellationToken);
            server.RecordError(null, clock.GetUtcNow());
            await unitOfWork.SaveChangesAsync(cancellationToken);
            return new McpServerToolsDto([.. tools.Select(t => new ToolDto(McpTool.NameFor(server, t.Name), t.Title ?? t.Name, t.Description, t.ReadOnly ? ToolRisk.Read : ToolRisk.Write))], null);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            server.RecordError(ex.Message, clock.GetUtcNow());
            await unitOfWork.SaveChangesAsync(cancellationToken);
            return new McpServerToolsDto([], ex.Message);
        }
    }

    public McpConnection ConnectionFor(McpServer server)
    {
        ArgumentNullException.ThrowIfNull(server);
        var auth = server.ProtectedAuthorization is null ? null : secrets.Unprotect(server.ProtectedAuthorization);

        // Admins may reach their own network; other people's servers go through the network policy.
        return new McpConnection(server.Id, server.Transport, server.Location, auth, RestrictNetwork: !currentUser.IsAdmin);
    }

    private void Apply(McpServer server, SaveMcpServerRequest request, DateTimeOffset now)
    {
        if (request.Authorization is not null)
        {
            server.SetProtectedAuthorization(request.Authorization.Length == 0 ? null : secrets.Protect(request.Authorization), now);
        }

        server.Enable(request.IsEnabled, now);
    }

    private void EnsureAllowed(McpTransport transport)
    {
        if (transport == McpTransport.Stdio && !(settings.AllowStdio && currentUser.IsAdmin))
        {
            throw new ForbiddenException("Local (stdio) MCP servers run commands on the Orbs server. Only admins can add them, and only when Orbs:Mcp:AllowStdio is true.");
        }
    }

    private async Task<McpServer> LoadAsync(Guid id, CancellationToken cancellationToken) =>
        await servers.FindAsync(id, cancellationToken) ?? throw new NotFoundException("MCP server", id);

    private static McpServerDto ToDto(McpServer s) =>
        new(s.Id, s.Name, s.Transport, s.Location, s.HasAuthorization, s.IsEnabled, s.LastError, s.CreatedAt);
}

/// <summary>Remembers each server's tool list for a few minutes, so a chat does not reconnect on every message.</summary>
public sealed class McpToolCache(TimeProvider clock)
{
    private static readonly TimeSpan Lifetime = TimeSpan.FromMinutes(5);
    private readonly ConcurrentDictionary<Guid, (IReadOnlyList<McpToolInfo> Tools, DateTimeOffset Expires)> _entries = new();

    public async Task<IReadOnlyList<McpToolInfo>> GetAsync(Guid serverId, Func<Task<IReadOnlyList<McpToolInfo>>> load)
    {
        ArgumentNullException.ThrowIfNull(load);
        if (_entries.TryGetValue(serverId, out var entry) && entry.Expires > clock.GetUtcNow())
        {
            return entry.Tools;
        }

        var tools = await load();
        _entries[serverId] = (tools, clock.GetUtcNow() + Lifetime);
        return tools;
    }

    public void Forget(Guid serverId) => _entries.TryRemove(serverId, out _);
}

/// <summary>Offers the tools of the current user's enabled MCP servers. A server that cannot be reached is skipped.</summary>
public sealed class McpToolSource(IMcpServerRepository servers, McpServerService service, IMcpClient client, McpToolCache cache) : IToolSource
{
    public async Task<IReadOnlyList<ITool>> ListAsync(CancellationToken cancellationToken = default)
    {
        var tools = new List<ITool>();
        foreach (var server in (await servers.ListAsync(cancellationToken)).Where(s => s.IsEnabled))
        {
            var connection = service.ConnectionFor(server);
            try
            {
                var offered = await cache.GetAsync(server.Id, () => client.ListToolsAsync(connection, cancellationToken));
                tools.AddRange(offered.Select(t => new McpTool(server, t, connection, client)));
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                // Unreachable servers simply offer nothing; the MCP page shows the error.
            }
        }

        return tools;
    }
}

/// <summary>One MCP tool, wrapped as an Orbs tool. Its name carries the server, so two servers never clash.</summary>
public sealed partial class McpTool(McpServer server, McpToolInfo info, McpConnection connection, IMcpClient client) : ITool
{
    public string Kind { get; } = NameFor(server, info.Name);

    public string DisplayName => $"{info.Title ?? info.Name} ({server.Name})";

    public string Description => string.IsNullOrWhiteSpace(info.Description) ? $"{info.Name} from {server.Name}" : info.Description;

    public string ParametersSchema => info.InputSchema;

    /// <summary>Unless the server says a tool only reads, Orbs assumes it acts.</summary>
    public ToolRisk Risk => info.ReadOnly ? ToolRisk.Read : ToolRisk.Write;

    public Task<string> InvokeAsync(ToolInvocation invocation, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(invocation);
        return client.CallToolAsync(connection, info.Name, invocation.Arguments, cancellationToken);
    }

    /// <summary><c>server-slug__tool</c>, limited to what model APIs accept (64 characters of a-z, 0-9, _ and -).</summary>
    public static string NameFor(McpServer server, string tool)
    {
        ArgumentNullException.ThrowIfNull(server);
        var prefix = Unsafe().Replace(server.Name.ToLowerInvariant(), "-").Trim('-');
        prefix = prefix.Length == 0 ? "mcp" : prefix.Length <= 20 ? prefix : prefix[..20];
        var name = $"{prefix}__{Unsafe().Replace(tool, "_")}";
        return name.Length <= 64 ? name : name[..64];
    }

    [GeneratedRegex("[^a-zA-Z0-9_-]")]
    private static partial Regex Unsafe();
}
