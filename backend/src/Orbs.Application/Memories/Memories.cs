using Orbs.Application.Abstractions;
using Orbs.Application.Common;
using Orbs.Domain.Memories;
using Orbs.Domain.Orbs;

namespace Orbs.Application.Memories;

public interface IMemoryRepository : IRepository<Memory>
{
    /// <summary>What one orb remembers, newest first.</summary>
    Task<IReadOnlyList<Memory>> ListForOrbAsync(Guid orbId, CancellationToken cancellationToken = default);
}

public sealed record MemoryDto(Guid Id, Guid OrbId, string Content, MemorySource Source, Guid? ConversationId, DateTimeOffset CreatedAt, DateTimeOffset UpdatedAt);

public sealed record SaveMemoryRequest(string Content);

/// <summary>Use cases for an orb's memory: list, add, edit and forget.</summary>
public sealed class MemoryService(IMemoryRepository memories, IOrbRepository orbs, IUnitOfWork unitOfWork, TimeProvider clock)
{
    /// <summary>At most this many memories go into a prompt, newest first.</summary>
    public const int PromptLimit = 60;

    public async Task<IReadOnlyList<MemoryDto>> ListAsync(Guid orbId, CancellationToken cancellationToken = default)
    {
        await EnsureOrbAsync(orbId, cancellationToken);
        return [.. (await memories.ListForOrbAsync(orbId, cancellationToken)).Select(ToDto)];
    }

    public async Task<MemoryDto> AddAsync(Guid orbId, SaveMemoryRequest request, MemorySource source = MemorySource.Manual, Guid? conversationId = null, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        await EnsureOrbAsync(orbId, cancellationToken);
        var memory = Memory.Create(orbId, request.Content, source, clock.GetUtcNow(), conversationId);
        memories.Add(memory);
        await unitOfWork.SaveChangesAsync(cancellationToken);
        return ToDto(memory);
    }

    public async Task<MemoryDto> EditAsync(Guid id, SaveMemoryRequest request, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        var memory = await LoadAsync(id, cancellationToken);
        memory.Edit(request.Content, clock.GetUtcNow());
        await unitOfWork.SaveChangesAsync(cancellationToken);
        return ToDto(memory);
    }

    /// <summary>Forgets a memory for good.</summary>
    public async Task ForgetAsync(Guid id, CancellationToken cancellationToken = default)
    {
        memories.Remove(await LoadAsync(id, cancellationToken));
        await unitOfWork.SaveChangesAsync(cancellationToken);
    }

    /// <summary>What goes into the orb's prompt.</summary>
    public async Task<IReadOnlyList<Memory>> ForPromptAsync(Orb orb, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(orb);
        return [.. (await memories.ListForOrbAsync(orb.Id, cancellationToken)).Take(PromptLimit)];
    }

    private async Task EnsureOrbAsync(Guid orbId, CancellationToken cancellationToken) =>
        _ = await orbs.FindAsync(orbId, cancellationToken) ?? throw new NotFoundException("Orb", orbId);

    private async Task<Memory> LoadAsync(Guid id, CancellationToken cancellationToken) =>
        await memories.FindAsync(id, cancellationToken) ?? throw new NotFoundException("Memory", id);

    internal static MemoryDto ToDto(Memory m) => new(m.Id, m.OrbId, m.Content, m.Source, m.ConversationId, m.CreatedAt, m.UpdatedAt);
}

/// <summary>Lets an orb save a fact it learned in a chat. Allowed by default: it only touches the orb's own memory, which the person can see and edit.</summary>
public sealed class RememberTool(MemoryService memories) : ITool
{
    public string Kind => "remember";

    public string DisplayName => "Remember";

    public string Description =>
        "Save one short, lasting fact about the person or their world for later chats, such as a preference, a name or a plan. " +
        "Do not save secrets or passing details.";

    public string ParametersSchema => """
        {"type":"object","properties":{"fact":{"type":"string","description":"The fact, as one short sentence."}},"required":["fact"]}
        """;

    public ToolRisk Risk => ToolRisk.Read;

    public bool AllowedByDefault => true;

    public async Task<string> InvokeAsync(ToolInvocation invocation, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(invocation);
        var fact = ToolJson.String(invocation.Arguments, "fact") ?? throw new ArgumentException("Give the fact to remember.");
        await memories.AddAsync(invocation.OrbId, new SaveMemoryRequest(fact), MemorySource.Learned, cancellationToken: cancellationToken);
        return $"Remembered: {fact}";
    }
}

/// <summary>Lets an orb forget a memory when the person asks it to.</summary>
public sealed class ForgetTool(IMemoryRepository repository, MemoryService memories) : ITool
{
    public string Kind => "forget";

    public string DisplayName => "Forget";

    public string Description => "Forget a saved memory when the person asks you to. Give words from the memory to find it.";

    public string ParametersSchema => """
        {"type":"object","properties":{"memory":{"type":"string","description":"Words from the memory to forget."}},"required":["memory"]}
        """;

    public ToolRisk Risk => ToolRisk.Read;

    public bool AllowedByDefault => true;

    public async Task<string> InvokeAsync(ToolInvocation invocation, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(invocation);
        var words = ToolJson.String(invocation.Arguments, "memory") ?? throw new ArgumentException("Say which memory to forget.");
        var all = await repository.ListForOrbAsync(invocation.OrbId, cancellationToken);
        var match = all.FirstOrDefault(m => m.Content.Contains(words, StringComparison.OrdinalIgnoreCase))
            ?? all.OrderByDescending(m => Overlap(m.Content, words)).FirstOrDefault(m => Overlap(m.Content, words) > 0)
            ?? throw new ArgumentException("No memory matches that.");
        await memories.ForgetAsync(match.Id, cancellationToken);
        return $"Forgot: {match.Content}";
    }

    private static int Overlap(string content, string words) =>
        words.Split(' ', StringSplitOptions.RemoveEmptyEntries).Count(w => w.Length > 2 && content.Contains(w, StringComparison.OrdinalIgnoreCase));
}

/// <summary>Reads a string from tool arguments.</summary>
internal static class ToolJson
{
    public static string? String(string json, string name)
    {
        using var doc = System.Text.Json.JsonDocument.Parse(string.IsNullOrWhiteSpace(json) ? "{}" : json);
        return doc.RootElement.ValueKind == System.Text.Json.JsonValueKind.Object
               && doc.RootElement.TryGetProperty(name, out var value)
               && value.ValueKind == System.Text.Json.JsonValueKind.String
            ? value.GetString()
            : null;
    }
}
