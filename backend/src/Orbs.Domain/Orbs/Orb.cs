using Orbs.Domain.Common;

namespace Orbs.Domain.Orbs;

/// <summary>
/// An orb is a model, its context and a face: an AI companion the user talks to.
/// </summary>
public sealed class Orb : OwnedAggregateRoot
{
    public const int MaxNameLength = 40;
    public const int MaxDescriptionLength = 280;
    public const int MaxInstructionsLength = 8_000;

    // A list (not a set) so EF Core can store it as a primitive collection; uniqueness is kept by the methods below.
    private readonly List<Guid> _contextSourceIds = [];

    private readonly Dictionary<string, ToolAccess> _toolPermissions = new(StringComparer.Ordinal);

    public const int MaxToolNameLength = 64;
    public const int MaxVoiceLength = 64;

    /// <summary>Ask first: an orb never acts in another app without the person saying yes, unless they allowed it.</summary>
    public const ToolAccess DefaultToolPermission = ToolAccess.Ask;

    private Orb(Guid id, string name, DateTimeOffset now) : base(id, now)
    {
        Name = name;
        Appearance = OrbAppearance.Default;
    }

    // For EF Core materialization.
    private Orb()
    {
        Name = string.Empty;
        Appearance = OrbAppearance.Default;
    }

    public string Name { get; private set; }

    /// <summary>A one-liner shown on cards, such as "Work sidekick".</summary>
    public string Description { get; private set; } = string.Empty;

    /// <summary>The brief: how the orb should behave, written like briefing a new teammate.</summary>
    public string Instructions { get; private set; } = string.Empty;

    public OrbAppearance Appearance { get; private set; }

    /// <summary>The model this orb uses, or <c>null</c> for "Auto" (the default model).</summary>
    public ModelRef? Model { get; private set; }

    /// <summary>The context sources this orb may read. It sees nothing else.</summary>
    public IReadOnlyCollection<Guid> ContextSourceIds => _contextSourceIds;

    public static Orb Create(string name, DateTimeOffset now, Guid? id = null) =>
        new(id ?? Guid.NewGuid(), Guard.Text(name, nameof(Name), MaxNameLength), now);

    public void Rename(string name, DateTimeOffset now)
    {
        Name = Guard.Text(name, nameof(Name), MaxNameLength);
        Touch(now);
    }

    public void Describe(string? description, DateTimeOffset now)
    {
        Description = Guard.Text(description, nameof(Description), MaxDescriptionLength, required: false);
        Touch(now);
    }

    public void Brief(string? instructions, DateTimeOffset now)
    {
        Instructions = Guard.Text(instructions, nameof(Instructions), MaxInstructionsLength, required: false);
        Touch(now);
    }

    public void ChangeAppearance(OrbAppearance appearance, DateTimeOffset now)
    {
        ArgumentNullException.ThrowIfNull(appearance);
        Appearance = appearance;
        Touch(now);
    }

    /// <summary>Pins a specific model; pass <c>null</c> to go back to Auto.</summary>
    public void UseModel(ModelRef? model, DateTimeOffset now)
    {
        Model = model;
        Touch(now);
    }

    public void LinkContext(Guid contextSourceId, DateTimeOffset now)
    {
        Guard.NotEmpty(contextSourceId, nameof(contextSourceId));
        if (!_contextSourceIds.Contains(contextSourceId))
        {
            _contextSourceIds.Add(contextSourceId);
            Touch(now);
        }
    }

    public void UnlinkContext(Guid contextSourceId, DateTimeOffset now)
    {
        if (_contextSourceIds.Remove(contextSourceId))
        {
            Touch(now);
        }
    }

    /// <summary>How the orb sounds when it reads replies aloud, such as "nova" or "af_heart". Null for the provider default.</summary>
    public string? Voice { get; private set; }

    public void UseVoice(string? voice, DateTimeOffset now)
    {
        var value = Guard.Text(voice, nameof(Voice), MaxVoiceLength, required: false);
        Voice = value.Length == 0 ? null : value;
        Touch(now);
    }

    /// <summary>Per-tool permissions. Tools that are not listed use <see cref="DefaultToolPermission"/>.</summary>
    public IReadOnlyDictionary<string, ToolAccess> ToolPermissions => _toolPermissions;

    /// <summary>The orb's setting for a tool, or <paramref name="fallback"/> (normally ask first) when it has none.</summary>
    public ToolAccess PermissionFor(string tool, ToolAccess fallback = DefaultToolPermission) =>
        _toolPermissions.TryGetValue(tool, out var permission) ? permission : fallback;

    public void SetToolPermission(string tool, ToolAccess permission, DateTimeOffset now)
    {
        // Explicit rules are kept, even "ask": some tools run without asking unless the orb says otherwise.
        _toolPermissions[Guard.Text(tool, nameof(tool), MaxToolNameLength)] = permission;
        Touch(now);
    }

    /// <summary>Replaces every tool permission in one go.</summary>
    public void SetToolPermissions(IReadOnlyDictionary<string, ToolAccess> permissions, DateTimeOffset now)
    {
        ArgumentNullException.ThrowIfNull(permissions);
        _toolPermissions.Clear();
        foreach (var (tool, permission) in permissions)
        {
            SetToolPermission(tool, permission, now);
        }

        Touch(now);
    }

    /// <summary>Replaces the whole set of linked sources in one go.</summary>
    public void SetContext(IEnumerable<Guid> contextSourceIds, DateTimeOffset now)
    {
        ArgumentNullException.ThrowIfNull(contextSourceIds);
        var ids = contextSourceIds.Distinct().ToList();
        foreach (var id in ids)
        {
            Guard.NotEmpty(id, nameof(contextSourceIds));
        }

        _contextSourceIds.Clear();
        _contextSourceIds.AddRange(ids);
        Touch(now);
    }
}
