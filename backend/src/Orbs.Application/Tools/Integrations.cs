using System.Text.Json;
using Orbs.Application.Abstractions;
using Orbs.Application.Accounts;
using Orbs.Application.Common;
using Orbs.Domain.Common;
using Orbs.Domain.Tools;

namespace Orbs.Application.Tools;

public interface IIntegrationRepository : IRepository<Integration>;

/// <summary>One field on an integration's form. At most one field per kind is the secret.</summary>
public sealed record IntegrationField(string Key, string Label, bool Required = false, bool Secret = false, string? Placeholder = null, string? Hint = null, IReadOnlyList<string>? Options = null);

/// <summary>An integration ready to make tools: its settings and its decrypted secret.</summary>
public sealed record IntegrationConfig(Guid Id, string Name, IReadOnlyDictionary<string, string> Settings, string? Secret, bool RestrictNetwork)
{
    public string Get(string key) =>
        Settings.TryGetValue(key, out var value) && !string.IsNullOrWhiteSpace(value)
            ? value
            : throw new InvalidOperationException($"The {Name} integration has no '{key}' set.");

    public string? Find(string key) => Settings.TryGetValue(key, out var value) && !string.IsNullOrWhiteSpace(value) ? value : null;
}

/// <summary>Knows one kind of app: what to ask for when connecting it, and which tools it offers.</summary>
public interface IIntegrationKind : IPlugin
{
    IReadOnlyList<IntegrationField> Fields { get; }

    IReadOnlyList<ITool> CreateTools(IntegrationConfig config);

    /// <summary>Makes one cheap call to check the settings. Throws with a helpful message when they are wrong.</summary>
    Task TestAsync(IntegrationConfig config, CancellationToken cancellationToken = default);
}

public sealed record IntegrationKindDto(string Kind, string DisplayName, string Description, IReadOnlyList<IntegrationField> Fields);

public sealed record IntegrationDto(Guid Id, string Kind, string Name, IReadOnlyDictionary<string, string> Settings, bool HasSecret, bool IsEnabled, IReadOnlyList<string> Tools, DateTimeOffset CreatedAt);

/// <summary>Connects or changes an integration. On update, a null secret keeps the stored one and "" removes it.</summary>
public sealed record SaveIntegrationRequest(string Kind, string Name, IReadOnlyDictionary<string, string>? Settings = null, string? Secret = null, bool IsEnabled = true);

public sealed record IntegrationTestDto(bool Ok, string? Error);

/// <summary>Use cases for connecting apps that bring tools.</summary>
public sealed class IntegrationService(
    IIntegrationRepository integrations,
    PluginRegistry<IIntegrationKind> kinds,
    ISecretProtector secrets,
    CurrentUser currentUser,
    IUnitOfWork unitOfWork,
    TimeProvider clock)
{
    public IReadOnlyList<IntegrationKindDto> Kinds() =>
        [.. kinds.All.Select(k => new IntegrationKindDto(k.Kind, k.DisplayName, k.Description, k.Fields))];

    public async Task<IReadOnlyList<IntegrationDto>> ListAsync(CancellationToken cancellationToken = default) =>
        [.. (await integrations.ListAsync(cancellationToken)).OrderBy(i => i.CreatedAt).Select(ToDto)];

    public async Task<IntegrationDto> CreateAsync(SaveIntegrationRequest request, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        var kind = kinds.Get(request.Kind);
        var now = clock.GetUtcNow();
        var integration = Integration.Create(kind.Kind, request.Name, now);
        Apply(integration, kind, request, now);
        integrations.Add(integration);
        await unitOfWork.SaveChangesAsync(cancellationToken);
        return ToDto(integration);
    }

    public async Task<IntegrationDto> UpdateAsync(Guid id, SaveIntegrationRequest request, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        var integration = await LoadAsync(id, cancellationToken);
        Apply(integration, kinds.Get(integration.Kind), request, clock.GetUtcNow());
        await unitOfWork.SaveChangesAsync(cancellationToken);
        return ToDto(integration);
    }

    public async Task DeleteAsync(Guid id, CancellationToken cancellationToken = default)
    {
        integrations.Remove(await LoadAsync(id, cancellationToken));
        await unitOfWork.SaveChangesAsync(cancellationToken);
    }

    public async Task<IntegrationTestDto> TestAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var integration = await LoadAsync(id, cancellationToken);
        try
        {
            await kinds.Get(integration.Kind).TestAsync(ConfigFor(integration), cancellationToken);
            return new IntegrationTestDto(true, null);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            return new IntegrationTestDto(false, ex.Message);
        }
    }

    public IntegrationConfig ConfigFor(Integration integration)
    {
        ArgumentNullException.ThrowIfNull(integration);
        var settings = JsonSerializer.Deserialize<Dictionary<string, string>>(integration.Settings) ?? [];
        var secret = integration.ProtectedSecret is null ? null : secrets.Unprotect(integration.ProtectedSecret);

        // Like MCP servers: admins may reach their own network, everyone else goes through the network policy.
        return new IntegrationConfig(integration.Id, integration.Name, settings, secret, RestrictNetwork: !currentUser.IsAdmin);
    }

    private void Apply(Integration integration, IIntegrationKind kind, SaveIntegrationRequest request, DateTimeOffset now)
    {
        var allowed = kind.Fields.Where(f => !f.Secret).ToDictionary(f => f.Key, StringComparer.Ordinal);
        var settings = (request.Settings ?? new Dictionary<string, string>())
            .Where(p => allowed.ContainsKey(p.Key))
            .ToDictionary(p => p.Key, p => p.Value.Trim(), StringComparer.Ordinal);
        foreach (var field in allowed.Values.Where(f => f.Required && !settings.ContainsKey(f.Key)))
        {
            throw new DomainException($"{field.Label} is required.");
        }

        integration.Configure(request.Name, JsonSerializer.Serialize(settings), now);
        if (request.Secret is not null)
        {
            integration.SetProtectedSecret(request.Secret.Length == 0 ? null : secrets.Protect(request.Secret), now);
        }

        integration.Enable(request.IsEnabled, now);
    }

    private IntegrationDto ToDto(Integration i)
    {
        var settings = JsonSerializer.Deserialize<Dictionary<string, string>>(i.Settings) ?? [];
        var tools = kinds.Contains(i.Kind)
            ? kinds.Get(i.Kind).CreateTools(new IntegrationConfig(i.Id, i.Name, settings, null, true)).Select(t => t.Kind).ToList()
            : [];
        return new IntegrationDto(i.Id, i.Kind, i.Name, settings, i.HasSecret, i.IsEnabled, tools, i.CreatedAt);
    }

    private async Task<Integration> LoadAsync(Guid id, CancellationToken cancellationToken) =>
        await integrations.FindAsync(id, cancellationToken) ?? throw new NotFoundException("Integration", id);
}

/// <summary>Offers the tools of the current user's enabled integrations.</summary>
public sealed class IntegrationToolSource(IIntegrationRepository integrations, PluginRegistry<IIntegrationKind> kinds, IntegrationService service) : IToolSource
{
    public async Task<IReadOnlyList<ITool>> ListAsync(CancellationToken cancellationToken = default) =>
        [.. (await integrations.ListAsync(cancellationToken))
            .Where(i => i.IsEnabled && kinds.Contains(i.Kind))
            .SelectMany(i => kinds.Get(i.Kind).CreateTools(service.ConfigFor(i)))];
}
