using System.Xml.Linq;
using Cronos;
using Orbs.Application.Schedules;
using Orbs.Domain.Common;
using Orbs.Infrastructure.Integrations;

namespace Orbs.Infrastructure.Schedules;

/// <summary>Cron expressions (five fields) with time zones and daylight saving time, via Cronos.</summary>
internal sealed class CronCalculator : ICronCalculator
{
    public void Validate(string expression, string timeZone)
    {
        _ = Parse(expression);
        _ = Zone(timeZone);
    }

    public DateTimeOffset? NextAfter(string expression, string timeZone, DateTimeOffset after) =>
        Parse(expression).GetNextOccurrence(after, Zone(timeZone));

    private static CronExpression Parse(string expression)
    {
        try
        {
            return CronExpression.Parse(expression);
        }
        catch (CronFormatException ex)
        {
            throw new DomainException($"\"{expression}\" is not a cron expression Orbs understands: {ex.Message}");
        }
    }

    private static TimeZoneInfo Zone(string timeZone) =>
        TimeZoneInfo.TryFindSystemTimeZoneById(timeZone, out var zone) ? zone : throw new DomainException($"Unknown time zone '{timeZone}'.");
}

/// <summary>Reads RSS 2.0 and Atom feeds with plain XML; no feed library needed for ids, titles and links.</summary>
internal sealed class FeedReader(IHttpClientFactory httpClientFactory) : IFeedReader
{
    private static readonly XNamespace Atom = "http://www.w3.org/2005/Atom";

    public async Task<IReadOnlyList<FeedItem>> ReadAsync(string url, bool restrictNetwork, CancellationToken cancellationToken = default)
    {
        var client = httpClientFactory.CreateClient(restrictNetwork ? IntegrationKit.RestrictedHttpClientName : IntegrationKit.HttpClientName);
        using var request = new HttpRequestMessage(HttpMethod.Get, url);
        var body = await IntegrationKit.SendAsync(client, request, cancellationToken);
        return Parse(body);
    }

    internal static IReadOnlyList<FeedItem> Parse(string xml)
    {
        var doc = XDocument.Parse(xml);
        var rss = doc.Descendants("item").Select(i => new FeedItem(
            (string?)i.Element("guid") ?? (string?)i.Element("link") ?? (string?)i.Element("title") ?? string.Empty,
            ((string?)i.Element("title") ?? "(no title)").Trim(),
            (string?)i.Element("link"),
            Clean((string?)i.Element("description"))));
        var atom = doc.Descendants(Atom + "entry").Select(e => new FeedItem(
            (string?)e.Element(Atom + "id") ?? (string?)e.Element(Atom + "link")?.Attribute("href") ?? string.Empty,
            ((string?)e.Element(Atom + "title") ?? "(no title)").Trim(),
            (string?)e.Elements(Atom + "link").FirstOrDefault(l => (string?)l.Attribute("rel") is null or "alternate")?.Attribute("href"),
            Clean((string?)e.Element(Atom + "summary") ?? (string?)e.Element(Atom + "content"))));
        return [.. rss.Concat(atom).Where(i => i.Id.Length > 0)];
    }

    private static string? Clean(string? html) => html is null ? null : Context.UrlConnector.HtmlToText(html);
}
