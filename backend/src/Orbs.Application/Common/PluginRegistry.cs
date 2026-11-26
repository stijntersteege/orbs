using Orbs.Application.Abstractions;
using Orbs.Domain.Common;

namespace Orbs.Application.Common;

/// <summary>Finds the plugin registered for a kind. Built from every <typeparamref name="T"/> in the container.</summary>
public sealed class PluginRegistry<T> where T : IPlugin
{
    private readonly Dictionary<string, T> _plugins;

    public PluginRegistry(IEnumerable<T> plugins)
    {
        _plugins = new Dictionary<string, T>(StringComparer.OrdinalIgnoreCase);
        foreach (var plugin in plugins)
        {
            if (!_plugins.TryAdd(plugin.Kind, plugin))
            {
                throw new InvalidOperationException($"Two {typeof(T).Name} plugins share the kind '{plugin.Kind}'.");
            }
        }
    }

    public IReadOnlyList<T> All => [.. _plugins.Values.OrderBy(p => p.DisplayName, StringComparer.OrdinalIgnoreCase)];

    public bool Contains(string kind) => _plugins.ContainsKey(kind);

    /// <summary>Returns the plugin, or throws a <see cref="DomainException"/> naming the unknown kind.</summary>
    public T Get(string kind) =>
        _plugins.TryGetValue(kind, out var plugin)
            ? plugin
            : throw new DomainException($"Unknown kind '{kind}'. Known kinds: {string.Join(", ", _plugins.Keys)}.");
}
