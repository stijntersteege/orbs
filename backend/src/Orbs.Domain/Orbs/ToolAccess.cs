namespace Orbs.Domain.Orbs;

/// <summary>What an orb may do with a tool.</summary>
public enum ToolAccess
{
    /// <summary>Use it without asking.</summary>
    Allow,

    /// <summary>Ask the person in the chat before every call. The default.</summary>
    Ask,

    /// <summary>Never use it; the model does not even see it.</summary>
    Block,
}
