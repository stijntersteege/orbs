using System.Text.RegularExpressions;
using Orbs.Domain.Orbs;

namespace Orbs.Application.Conversations;

/// <summary>Finds "@Name" mentions of orbs in a message. Names with spaces work too ("@Lab Helper").</summary>
public static partial class Mentions
{
    /// <summary>The mentioned orbs in the order they appear, each once. Longer names win over shorter ones that start the same.</summary>
    public static IReadOnlyList<Orb> Find(string text, IReadOnlyList<Orb> orbs)
    {
        ArgumentNullException.ThrowIfNull(orbs);
        if (string.IsNullOrEmpty(text) || !text.Contains('@', StringComparison.Ordinal))
        {
            return [];
        }

        var found = new List<(int Index, Orb Orb)>();
        foreach (var orb in orbs.OrderByDescending(o => o.Name.Length))
        {
            var match = Regex.Match(text, $@"(?<![\w@])@{Regex.Escape(orb.Name)}(?![\w])", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant, TimeSpan.FromMilliseconds(100));
            if (match.Success && !found.Any(f => match.Index >= f.Index && match.Index < f.Index + f.Orb.Name.Length + 1))
            {
                found.Add((match.Index, orb));
            }
        }

        return [.. found.OrderBy(f => f.Index).Select(f => f.Orb)];
    }
}
