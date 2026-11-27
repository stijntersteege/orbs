using Orbs.Application.Schedules;
using Orbs.Domain.Common;
using Orbs.Domain.Orbs;
using Orbs.Domain.Schedules;

namespace Orbs.Application.Orbs;

/// <summary>
/// An orb as one shareable file: its face, brief, voice, tool permissions and schedules. It leaves out what only
/// makes sense on one account (the pinned model, context sources, chats and memories), so anyone can import it.
/// </summary>
public sealed record OrbTemplate(
    string Format,
    int Version,
    OrbTemplateOrb Orb,
    IReadOnlyList<OrbTemplateSchedule>? Schedules = null)
{
    public const string FormatName = "orbs-template";
    public const int CurrentVersion = 1;
}

public sealed record OrbTemplateOrb(
    string Name,
    string? Description = null,
    string? Instructions = null,
    OrbAppearance? Appearance = null,
    IReadOnlyDictionary<string, ToolAccess>? ToolPermissions = null,
    string? Voice = null);

/// <summary>A schedule in a template. Webhook and email schedules are not exported: they depend on one account.</summary>
public sealed record OrbTemplateSchedule(
    string Name,
    string Prompt,
    TriggerKind Trigger,
    string? Cron = null,
    string? TimeZone = null,
    string? FeedUrl = null,
    bool IsEnabled = true);

/// <summary>The new orb, and how many of the template's schedules came with it.</summary>
public sealed record OrbImportResult(OrbDto Orb, int Schedules);

/// <summary>Exports an orb as a template and creates new orbs from templates.</summary>
public sealed class OrbTemplateService(OrbService orbs, ScheduleService schedules)
{
    public async Task<OrbTemplate> ExportAsync(Guid orbId, CancellationToken cancellationToken = default)
    {
        var orb = await orbs.GetAsync(orbId, cancellationToken);
        var own = await schedules.ListAsync(orbId, cancellationToken);
        return new OrbTemplate(
            OrbTemplate.FormatName,
            OrbTemplate.CurrentVersion,
            new OrbTemplateOrb(orb.Name, orb.Description, orb.Instructions, orb.Appearance, orb.ToolPermissions, orb.Voice),
            [.. own.Where(s => s.Trigger is TriggerKind.Time or TriggerKind.Rss)
                .Select(s => new OrbTemplateSchedule(s.Name, s.Prompt, s.Trigger, s.Cron, s.TimeZone, s.FeedUrl, s.IsEnabled))]);
    }

    /// <summary>Creates a new orb (and its schedules) from a template. The orb's name gets a number when it is taken.</summary>
    public async Task<OrbImportResult> ImportAsync(OrbTemplate template, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(template);
        if (template.Format != OrbTemplate.FormatName || template.Orb is null)
        {
            throw new DomainException("This is not an orb template.");
        }

        if (template.Version > OrbTemplate.CurrentVersion)
        {
            throw new DomainException("This template was made by a newer version of Orbs. Update Orbs to import it.");
        }

        var wanted = (template.Schedules ?? []).Where(s => s.Trigger is TriggerKind.Time or TriggerKind.Rss).ToList();
        var t = template.Orb;
        var names = (await orbs.ListAsync(cancellationToken)).Select(o => o.Name).ToHashSet(StringComparer.OrdinalIgnoreCase);
        var name = (t.Name ?? string.Empty).Trim();
        for (var n = 2; names.Contains(name); n++)
        {
            name = $"{t.Name!.Trim()} {n}";
        }

        var orb = await orbs.CreateAsync(new SaveOrbRequest(name, t.Description, t.Instructions, t.Appearance, ToolPermissions: t.ToolPermissions, Voice: t.Voice), cancellationToken);
        try
        {
            foreach (var s in wanted)
            {
                await schedules.CreateAsync(new SaveScheduleRequest(orb.Id, s.Name, s.Prompt, s.Trigger, s.Cron, s.TimeZone, s.FeedUrl, IsEnabled: s.IsEnabled), cancellationToken);
            }
        }
        catch
        {
            // All or nothing: a schedule the template got wrong takes the new orb with it.
            await orbs.DeleteAsync(orb.Id, cancellationToken);
            throw;
        }

        return new OrbImportResult(orb, wanted.Count);
    }
}
