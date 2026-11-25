using System.Text.RegularExpressions;

namespace Orbs.Domain.Common;

/// <summary>
/// A lower-case identifier such as <c>openai-compatible</c> or <c>url</c>.
/// Used for extensible "kinds" so new adapters never need a domain change.
/// </summary>
public static partial class Slug
{
    public const int MaxLength = 64;

    public static string Normalize(string? value, string name)
    {
        var slug = Guard.Text(value, name, MaxLength).ToLowerInvariant();
        if (!SlugPattern().IsMatch(slug))
        {
            throw new DomainException($"{name} may only contain a-z, 0-9 and '-'.");
        }

        return slug;
    }

    [GeneratedRegex("^[a-z0-9]+(-[a-z0-9]+)*$")]
    private static partial Regex SlugPattern();
}
