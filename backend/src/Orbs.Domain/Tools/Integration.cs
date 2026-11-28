using Orbs.Domain.Common;

namespace Orbs.Domain.Tools;

/// <summary>
/// A connected app that brings its own tools: web search, a calendar, an email account, Home Assistant, ...
/// <see cref="Kind"/> names the plugin that knows the app. Plain settings are stored as JSON;
/// the one secret (password, token or key) is stored encrypted.
/// </summary>
public sealed class Integration : OwnedAggregateRoot
{
    public const int MaxNameLength = 60;
    public const int MaxSettingsLength = 10_000;

    private Integration(Guid id, string kind, string name, DateTimeOffset now) : base(id, now)
    {
        Kind = kind;
        Name = name;
    }

    // For EF Core materialization.
    private Integration()
    {
        Kind = string.Empty;
        Name = string.Empty;
    }

    public string Kind { get; private set; }

    public string Name { get; private set; }

    /// <summary>Non-secret settings as a JSON object, such as a server address or a user name.</summary>
    public string Settings { get; private set; } = "{}";

    public string? ProtectedSecret { get; private set; }

    public bool IsEnabled { get; private set; } = true;

    public bool HasSecret => ProtectedSecret is not null;

    public static Integration Create(string kind, string name, DateTimeOffset now) =>
        new(Guid.NewGuid(), Slug.Normalize(kind, nameof(Kind)), Guard.Text(name, nameof(Name), MaxNameLength), now);

    public void Configure(string name, string? settingsJson, DateTimeOffset now)
    {
        Name = Guard.Text(name, nameof(Name), MaxNameLength);
        Settings = Guard.Text(settingsJson ?? "{}", nameof(Settings), MaxSettingsLength);
        Touch(now);
    }

    public void SetProtectedSecret(string? protectedValue, DateTimeOffset now)
    {
        ProtectedSecret = string.IsNullOrEmpty(protectedValue) ? null : protectedValue;
        Touch(now);
    }

    public void Enable(bool enabled, DateTimeOffset now)
    {
        IsEnabled = enabled;
        Touch(now);
    }
}
