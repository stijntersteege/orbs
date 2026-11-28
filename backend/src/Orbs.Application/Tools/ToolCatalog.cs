using System.Text.RegularExpressions;
using Orbs.Application.Abstractions;
using Orbs.Domain.Orbs;

namespace Orbs.Application.Tools;

/// <summary>A tool as listed in the app. <see cref="Description"/> is the summary written for people.</summary>
public sealed record ToolDto(string Name, string DisplayName, string Description, ToolRisk Risk, bool AllowedByDefault = false);

/// <summary>
/// Every tool the current user's orbs could use, gathered from all tool sources
/// (built-in tools, connected MCP servers, integrations).
/// </summary>
public sealed partial class ToolCatalog(IEnumerable<IToolSource> sources)
{
    public async Task<IReadOnlyList<ITool>> ListAsync(CancellationToken cancellationToken = default)
    {
        var tools = new Dictionary<string, ITool>(StringComparer.Ordinal);
        foreach (var source in sources)
        {
            foreach (var tool in await source.ListAsync(cancellationToken))
            {
                // Model APIs only accept simple names; the first source to claim a name keeps it.
                if (ValidName().IsMatch(tool.Kind))
                {
                    tools.TryAdd(tool.Kind, tool);
                }
            }
        }

        return [.. tools.Values.OrderBy(t => t.DisplayName, StringComparer.OrdinalIgnoreCase)];
    }

    /// <summary>The tools an orb may use. Per-orb permissions narrow this down.</summary>
    public Task<IReadOnlyList<ITool>> ForOrbAsync(Orb orb, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(orb);
        return ListAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<ToolDto>> DescribeAsync(CancellationToken cancellationToken = default) =>
        [.. (await ListAsync(cancellationToken)).Select(t => new ToolDto(t.Kind, t.DisplayName, t.Summary, t.Risk, t.AllowedByDefault))];

    public static ToolDefinition Define(ITool tool)
    {
        ArgumentNullException.ThrowIfNull(tool);
        return new ToolDefinition(tool.Kind, tool.Description, tool.ParametersSchema);
    }

    [GeneratedRegex("^[a-zA-Z0-9_-]{1,64}$")]
    private static partial Regex ValidName();
}

/// <summary>Tools compiled into Orbs and registered as <see cref="ITool"/> services.</summary>
public sealed class BuiltInToolSource(IEnumerable<ITool> tools) : IToolSource
{
    public Task<IReadOnlyList<ITool>> ListAsync(CancellationToken cancellationToken = default) =>
        Task.FromResult<IReadOnlyList<ITool>>([.. tools]);
}
