using Orbs.Domain.Common;

namespace Orbs.Domain.Skills;

/// <summary>
/// A routine any of the person's orbs can follow: a name, when to use it, and the steps. Orbs read it with the
/// use_skill tool. It matches the SKILL.md format, so skills move between Orbs and other agents.
/// </summary>
public sealed class Skill : OwnedAggregateRoot
{
    public const int MaxDescriptionLength = 1_024;
    public const int MaxInstructionsLength = 20_000;

    private Skill(Guid id, string name, string description, string instructions, DateTimeOffset now) : base(id, now)
    {
        Name = name;
        Description = description;
        Instructions = instructions;
    }

    // For EF Core materialization.
    private Skill()
    {
        Name = string.Empty;
        Description = string.Empty;
        Instructions = string.Empty;
    }

    /// <summary>A lower-case name such as "weekly-review": how orbs ask for it.</summary>
    public string Name { get; private set; }

    /// <summary>What the skill does and when to use it.</summary>
    public string Description { get; private set; }

    /// <summary>The steps, in Markdown.</summary>
    public string Instructions { get; private set; }

    public static Skill Create(string name, string description, string instructions, DateTimeOffset now)
    {
        var skill = new Skill(Guid.NewGuid(), string.Empty, string.Empty, string.Empty, now);
        skill.Set(name, description, instructions);
        return skill;
    }

    public void Update(string name, string description, string instructions, DateTimeOffset now)
    {
        Set(name, description, instructions);
        Touch(now);
    }

    private void Set(string name, string description, string instructions)
    {
        Name = Slug.Normalize(name, nameof(Name));
        Description = Guard.Text(description, nameof(Description), MaxDescriptionLength);
        Instructions = Guard.Text(instructions, nameof(Instructions), MaxInstructionsLength);
    }
}
