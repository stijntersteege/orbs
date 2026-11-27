using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;
using Orbs.Application.Abstractions;
using Orbs.Application.Common;
using Orbs.Domain.Common;
using Orbs.Domain.Conversations;
using Orbs.Domain.Skills;

namespace Orbs.Application.Skills;

public interface ISkillRepository : IRepository<Skill>;

public sealed record SkillDto(Guid Id, string Name, string Description, string Instructions, DateTimeOffset CreatedAt, DateTimeOffset UpdatedAt);

public sealed record SaveSkillRequest(string Name, string Description, string Instructions);

/// <summary>A SKILL.md file's text.</summary>
public sealed record ImportSkillRequest(string Markdown);

/// <summary>Turns the tool calls of one reply into a skill.</summary>
public sealed record SkillFromReplyRequest(string Name, string? Description = null);

/// <summary>A skill as a SKILL.md file.</summary>
public sealed record SkillFile(string FileName, string Markdown);

/// <summary>Use cases for skills: list, save, import and export SKILL.md files, and save a reply's steps as a skill.</summary>
public sealed class SkillService(ISkillRepository skills, IConversationRepository conversations, IUnitOfWork unitOfWork, TimeProvider clock)
{
    public async Task<IReadOnlyList<SkillDto>> ListAsync(CancellationToken cancellationToken = default) =>
        [.. (await skills.ListAsync(cancellationToken)).OrderBy(s => s.Name, StringComparer.Ordinal).Select(ToDto)];

    public async Task<SkillDto> GetAsync(Guid id, CancellationToken cancellationToken = default) =>
        ToDto(await LoadAsync(id, cancellationToken));

    public async Task<SkillDto> CreateAsync(SaveSkillRequest request, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        var skill = Skill.Create(request.Name, request.Description, request.Instructions, clock.GetUtcNow());
        await EnsureFreeAsync(skill.Name, null, cancellationToken);
        skills.Add(skill);
        await unitOfWork.SaveChangesAsync(cancellationToken);
        return ToDto(skill);
    }

    public async Task<SkillDto> UpdateAsync(Guid id, SaveSkillRequest request, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        var skill = await LoadAsync(id, cancellationToken);
        skill.Update(request.Name, request.Description, request.Instructions, clock.GetUtcNow());
        await EnsureFreeAsync(skill.Name, skill.Id, cancellationToken);
        await unitOfWork.SaveChangesAsync(cancellationToken);
        return ToDto(skill);
    }

    public async Task DeleteAsync(Guid id, CancellationToken cancellationToken = default)
    {
        skills.Remove(await LoadAsync(id, cancellationToken));
        await unitOfWork.SaveChangesAsync(cancellationToken);
    }

    /// <summary>Adds a skill from a SKILL.md file. A skill with the same name is replaced.</summary>
    public async Task<SkillDto> ImportAsync(ImportSkillRequest request, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        var parsed = SkillMarkdown.Parse(request.Markdown);
        var name = SkillMarkdown.ToName(parsed.Name);
        var existing = (await skills.ListAsync(cancellationToken)).FirstOrDefault(s => s.Name == name);
        if (existing is null)
        {
            return await CreateAsync(new SaveSkillRequest(name, parsed.Description, parsed.Instructions), cancellationToken);
        }

        existing.Update(name, parsed.Description, parsed.Instructions, clock.GetUtcNow());
        await unitOfWork.SaveChangesAsync(cancellationToken);
        return ToDto(existing);
    }

    public async Task<SkillFile> ExportAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var skill = await LoadAsync(id, cancellationToken);
        return new SkillFile($"{skill.Name}.SKILL.md", SkillMarkdown.Render(skill.Name, skill.Description, skill.Instructions));
    }

    /// <summary>Saves the tools one reply used, in order, as a skill orbs can repeat.</summary>
    public async Task<SkillDto> FromReplyAsync(Guid conversationId, Guid messageId, SkillFromReplyRequest request, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        var conversation = await conversations.FindAsync(conversationId, cancellationToken) ?? throw new NotFoundException("Conversation", conversationId);
        var messages = conversation.Messages;
        var index = -1;
        for (var i = 0; i < messages.Count; i++)
        {
            if (messages[i].Id == messageId)
            {
                index = i;
            }
        }

        if (index < 0)
        {
            throw new NotFoundException("Message", messageId);
        }

        var reply = messages[index];
        var calls = reply.ToolCalls.Where(c => !c.IsError && c.Decision != "denied").ToList();
        if (reply.Role != MessageRole.Assistant || calls.Count == 0)
        {
            throw new DomainException("Only a reply that used tools can become a skill.");
        }

        var asked = messages.Take(index).LastOrDefault(m => m.Role == MessageRole.User)?.Content;
        var steps = new StringBuilder();
        if (!string.IsNullOrWhiteSpace(asked))
        {
            steps.Append("This worked when the person asked: \"").Append(Clip(asked, 300)).Append("\"\n\n");
        }

        steps.Append("Steps:\n\n");
        for (var i = 0; i < calls.Count; i++)
        {
            steps.Append(CultureInfo.InvariantCulture, $"{i + 1}. Use the `{calls[i].Tool}` tool");
            if (calls[i].Arguments is { Length: > 2 } args)
            {
                steps.Append(" with arguments like `").Append(Clip(args, 500)).Append('`');
            }

            steps.Append(".\n");
        }

        steps.Append("\nChange the arguments to fit the new request, then answer with what you found.");
        var description = string.IsNullOrWhiteSpace(request.Description)
            ? $"Repeats the steps from the chat \"{conversation.Title}\"."
            : request.Description;
        return await CreateAsync(new SaveSkillRequest(SkillMarkdown.ToName(request.Name), description, steps.ToString()), cancellationToken);
    }

    private async Task EnsureFreeAsync(string name, Guid? self, CancellationToken cancellationToken)
    {
        if ((await skills.ListAsync(cancellationToken)).Any(s => s.Name == name && s.Id != self))
        {
            throw new DomainException($"There is already a skill named \"{name}\".");
        }
    }

    private async Task<Skill> LoadAsync(Guid id, CancellationToken cancellationToken) =>
        await skills.FindAsync(id, cancellationToken) ?? throw new NotFoundException("Skill", id);

    private static string Clip(string text, int max) =>
        text.Length <= max ? text : string.Concat(text.AsSpan(0, max - 3), "...");

    internal static SkillDto ToDto(Skill s) => new(s.Id, s.Name, s.Description, s.Instructions, s.CreatedAt, s.UpdatedAt);
}

/// <summary>
/// Reads and writes the SKILL.md format: YAML front matter with <c>name</c> and <c>description</c>, then the
/// instructions in Markdown. Only those two keys are read; others are ignored.
/// </summary>
public static partial class SkillMarkdown
{
    private const char ByteOrderMark = (char)0xFEFF;

    public sealed record Parsed(string Name, string Description, string Instructions);

    public static Parsed Parse(string? markdown)
    {
        var text = (markdown ?? string.Empty).Replace("\r\n", "\n", StringComparison.Ordinal).TrimStart(ByteOrderMark);
        var match = FrontMatter().Match(text);
        if (!match.Success)
        {
            throw new DomainException("A SKILL.md file starts with front matter between --- lines, with a name and a description.");
        }

        var values = new Dictionary<string, string>(StringComparer.Ordinal);
        string? folding = null;
        var folded = new StringBuilder();
        foreach (var line in match.Groups["yaml"].Value.Split('\n'))
        {
            var pair = KeyValue().Match(line);
            if (pair.Success)
            {
                EndFold();
                var value = pair.Groups["value"].Value.Trim();
                if (value is ">" or "|" or ">-" or "|-" or "")
                {
                    folding = pair.Groups["key"].Value;
                }
                else
                {
                    values[pair.Groups["key"].Value] = Unquote(value);
                }
            }
            else if (folding is not null && line.StartsWith(' '))
            {
                folded.Append(folded.Length > 0 ? " " : string.Empty).Append(line.Trim());
            }
        }

        EndFold();
        var name = values.GetValueOrDefault("name");
        var description = values.GetValueOrDefault("description");
        if (string.IsNullOrWhiteSpace(name) || string.IsNullOrWhiteSpace(description))
        {
            throw new DomainException("A SKILL.md file needs a name and a description in its front matter.");
        }

        var instructions = text[match.Length..].Trim();
        return new Parsed(name, description, instructions.Length == 0 ? description : instructions);

        void EndFold()
        {
            if (folding is not null && folded.Length > 0)
            {
                values[folding] = folded.ToString();
            }

            folding = null;
            folded.Clear();
        }
    }

    public static string Render(string name, string description, string instructions)
    {
        ArgumentNullException.ThrowIfNull(instructions);
        return $"---\nname: {name}\ndescription: {Quote(description)}\n---\n\n{instructions.Trim()}\n";
    }

    /// <summary>Makes any title a skill name: lower case, words joined by '-'.</summary>
    public static string ToName(string? title)
    {
        var name = NotNameChars().Replace((title ?? string.Empty).ToLowerInvariant(), "-").Trim('-');
        return name.Length <= Slug.MaxLength ? name : name[..Slug.MaxLength].TrimEnd('-');
    }

    private static string Quote(string value)
    {
        var line = value.Replace('\n', ' ').Trim();
        return "\"" + line.Replace("\\", "\\\\", StringComparison.Ordinal).Replace("\"", "\\\"", StringComparison.Ordinal) + "\"";
    }

    private static string Unquote(string value)
    {
        if (value.Length >= 2 && value[0] == '"' && value[^1] == '"')
        {
            return value[1..^1].Replace("\\\"", "\"", StringComparison.Ordinal).Replace("\\\\", "\\", StringComparison.Ordinal);
        }

        return value.Length >= 2 && value[0] == '\'' && value[^1] == '\''
            ? value[1..^1].Replace("''", "'", StringComparison.Ordinal)
            : value;
    }

    [GeneratedRegex(@"\A---[ \t]*\n(?<yaml>.*?)\n---[ \t]*(\n|\z)", RegexOptions.Singleline)]
    private static partial Regex FrontMatter();

    [GeneratedRegex(@"^(?<key>[A-Za-z_][\w-]*):(?<value>.*)$")]
    private static partial Regex KeyValue();

    [GeneratedRegex("[^a-z0-9]+")]
    private static partial Regex NotNameChars();
}

/// <summary>Lets any orb read a saved skill and follow its steps.</summary>
public sealed class UseSkillTool(ISkillRepository skills) : ITool
{
    public string Kind => "use_skill";

    public string DisplayName => "Use a skill";

    public string Description =>
        "Read one of the person's saved skills (routines) and follow its steps. Call it without \"skill\" to list the skills and when to use each.";

    public string ParametersSchema => """
        {"type":"object","properties":{"skill":{"type":"string","description":"The name of the skill."}}}
        """;

    public ToolRisk Risk => ToolRisk.Read;

    // It only reads text the person saved; whatever the steps do still goes through each tool's own permission.
    public bool AllowedByDefault => true;

    public async Task<string> InvokeAsync(ToolInvocation invocation, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(invocation);
        var all = (await skills.ListAsync(cancellationToken)).OrderBy(s => s.Name, StringComparer.Ordinal).ToList();
        var wanted = Memories.ToolJson.String(invocation.Arguments, "skill");
        if (string.IsNullOrWhiteSpace(wanted))
        {
            return all.Count == 0
                ? "The person has no saved skills yet."
                : "Skills:\n" + string.Join('\n', all.Select(s => $"- {s.Name}: {s.Description}"));
        }

        var skill = all.FirstOrDefault(s => s.Name == SkillMarkdown.ToName(wanted))
            ?? throw new ArgumentException($"There is no skill named \"{wanted}\". Call use_skill without a name to list them.");
        return $"Skill \"{skill.Name}\": {skill.Description}\n\n{skill.Instructions}";
    }
}
