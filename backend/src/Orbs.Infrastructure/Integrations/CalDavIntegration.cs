using System.Globalization;
using System.Net.Http.Headers;
using System.Text;
using System.Xml.Linq;
using Orbs.Application.Abstractions;
using Orbs.Application.Tools;

namespace Orbs.Infrastructure.Integrations;

/// <summary>A CalDAV calendar (Nextcloud, Radicale, iCloud, Fastmail, ...): read events and add new ones.</summary>
internal sealed class CalDavIntegration(IHttpClientFactory httpClientFactory, TimeProvider clock) : IIntegrationKind
{
    private static readonly XNamespace Dav = "DAV:";
    private static readonly XNamespace Cal = "urn:ietf:params:xml:ns:caldav";

    public string Kind => "caldav";

    public string DisplayName => "Calendar (CalDAV)";

    public string Description => "Read and add events in a CalDAV calendar such as Nextcloud, Radicale, iCloud or Fastmail.";

    public IReadOnlyList<IntegrationField> Fields { get; } =
    [
        new("url", "Calendar address", Required: true, Placeholder: "https://cloud.example.com/remote.php/dav/calendars/me/personal/", Hint: "The address of one calendar, not the account."),
        new("username", "User name", Required: true),
        new("password", "Password", Secret: true, Hint: "Use an app password where your provider offers one."),
    ];

    public IReadOnlyList<ITool> CreateTools(IntegrationConfig config) =>
    [
        new DelegateTool(
            "calendar_list_events",
            "Read calendar",
            "List calendar events between two moments (ISO 8601). Defaults to the next 7 days.",
            """{"type":"object","properties":{"from":{"type":"string","description":"Start, ISO 8601."},"to":{"type":"string","description":"End, ISO 8601."}}}""",
            ToolRisk.Read,
            (args, ct) => ListAsync(config, args.Text("from"), args.Text("to"), ct)),
        new DelegateTool(
            "calendar_create_event",
            "Add calendar event",
            "Add an event to the calendar. Times are ISO 8601 with an offset, such as 2026-12-01T14:00:00+01:00.",
            """{"type":"object","properties":{"title":{"type":"string"},"start":{"type":"string"},"end":{"type":"string"},"location":{"type":"string"},"description":{"type":"string"}},"required":["title","start","end"]}""",
            ToolRisk.Write,
            (args, ct) => CreateAsync(config, args.Required("title"), args.Required("start"), args.Required("end"), args.Text("location"), args.Text("description"), ct)),
    ];

    public async Task TestAsync(IntegrationConfig config, CancellationToken cancellationToken = default)
    {
        using var request = Request(config, new HttpMethod("PROPFIND"), new Uri(IntegrationKit.BaseUri(config, "url"), "."));
        request.Headers.Add("Depth", "0");
        _ = await IntegrationKit.SendAsync(IntegrationKit.Http(httpClientFactory, config), request, cancellationToken);
    }

    internal async Task<string> ListAsync(IntegrationConfig config, string? from, string? to, CancellationToken cancellationToken)
    {
        var start = ParseMoment(from) ?? clock.GetUtcNow();
        var end = ParseMoment(to) ?? start.AddDays(7);
        var query = new XDocument(new XElement(Cal + "calendar-query",
            new XAttribute(XNamespace.Xmlns + "d", Dav),
            new XAttribute(XNamespace.Xmlns + "c", Cal),
            new XElement(Dav + "prop", new XElement(Cal + "calendar-data")),
            new XElement(Cal + "filter",
                new XElement(Cal + "comp-filter", new XAttribute("name", "VCALENDAR"),
                    new XElement(Cal + "comp-filter", new XAttribute("name", "VEVENT"),
                        new XElement(Cal + "time-range", new XAttribute("start", Stamp(start)), new XAttribute("end", Stamp(end))))))));

        using var request = Request(config, new HttpMethod("REPORT"), IntegrationKit.BaseUri(config, "url"));
        request.Headers.Add("Depth", "1");
        request.Content = new StringContent(query.ToString(), Encoding.UTF8, "application/xml");
        var body = await IntegrationKit.SendAsync(IntegrationKit.Http(httpClientFactory, config), request, cancellationToken);

        var events = XDocument.Parse(body).Descendants(Cal + "calendar-data")
            .SelectMany(d => ICal.ReadEvents(d.Value))
            .OrderBy(e => e.Start)
            .ToList();
        if (events.Count == 0)
        {
            return $"No events between {start:yyyy-MM-dd HH:mm} and {end:yyyy-MM-dd HH:mm} (UTC).";
        }

        var text = new StringBuilder();
        foreach (var e in events)
        {
            text.Append(e.Start.ToString("ddd yyyy-MM-dd HH:mm", CultureInfo.InvariantCulture));
            if (e.End is { } finish)
            {
                text.Append('–').Append(finish.ToString("HH:mm", CultureInfo.InvariantCulture));
            }

            text.Append(" UTC  ").Append(e.Summary);
            if (e.Location is { Length: > 0 })
            {
                text.Append(" @ ").Append(e.Location);
            }

            text.AppendLine();
        }

        return text.ToString().TrimEnd();
    }

    internal async Task<string> CreateAsync(IntegrationConfig config, string title, string start, string end, string? location, string? description, CancellationToken cancellationToken)
    {
        var from = ParseMoment(start) ?? throw new ArgumentException($"'{start}' is not an ISO 8601 time.");
        var until = ParseMoment(end) ?? throw new ArgumentException($"'{end}' is not an ISO 8601 time.");
        if (until <= from)
        {
            throw new ArgumentException("The end must be after the start.");
        }

        var uid = $"{Guid.NewGuid():N}@orbs";
        var ics = ICal.WriteEvent(uid, title, from, until, location, description, clock.GetUtcNow());
        using var request = Request(config, HttpMethod.Put, new Uri(IntegrationKit.BaseUri(config, "url"), $"{uid}.ics"));
        request.Headers.Add("If-None-Match", "*");
        request.Content = new StringContent(ics, Encoding.UTF8, "text/calendar");
        _ = await IntegrationKit.SendAsync(IntegrationKit.Http(httpClientFactory, config), request, cancellationToken);
        return $"Added \"{title}\" on {from.ToUniversalTime():yyyy-MM-dd HH:mm} UTC.";
    }

    private static HttpRequestMessage Request(IntegrationConfig config, HttpMethod method, Uri uri)
    {
        var request = new HttpRequestMessage(method, uri);
        var credentials = Convert.ToBase64String(Encoding.UTF8.GetBytes($"{config.Get("username")}:{config.Secret}"));
        request.Headers.Authorization = new AuthenticationHeaderValue("Basic", credentials);
        return request;
    }

    private static DateTimeOffset? ParseMoment(string? value) =>
        DateTimeOffset.TryParse(value, CultureInfo.InvariantCulture, DateTimeStyles.AssumeUniversal, out var moment) ? moment : null;

    private static string Stamp(DateTimeOffset moment) => moment.UtcDateTime.ToString("yyyyMMdd'T'HHmmss'Z'", CultureInfo.InvariantCulture);
}

/// <summary>Just enough iCalendar (RFC 5545) to read and write simple events.</summary>
internal static class ICal
{
    public sealed record Event(string Summary, DateTimeOffset Start, DateTimeOffset? End, string? Location);

    public static IEnumerable<Event> ReadEvents(string ics)
    {
        // Unfold lines: a line that starts with a space continues the previous one.
        var lines = ics.Replace("\r\n ", string.Empty, StringComparison.Ordinal).Replace("\n ", string.Empty, StringComparison.Ordinal)
            .Split('\n').Select(l => l.TrimEnd('\r'));
        Dictionary<string, string>? current = null;
        foreach (var line in lines)
        {
            if (line == "BEGIN:VEVENT")
            {
                current = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            }
            else if (line == "END:VEVENT" && current is not null)
            {
                if (current.TryGetValue("DTSTART", out var start) && ParseDate(start) is { } begins)
                {
                    yield return new Event(
                        Unescape(current.GetValueOrDefault("SUMMARY") ?? "(no title)"),
                        begins,
                        current.TryGetValue("DTEND", out var end) ? ParseDate(end) : null,
                        current.TryGetValue("LOCATION", out var location) ? Unescape(location) : null);
                }

                current = null;
            }
            else if (current is not null && line.IndexOf(':', StringComparison.Ordinal) is var colon and > 0)
            {
                var name = line[..colon].Split(';')[0];
                current.TryAdd(name, line[(colon + 1)..]);
            }
        }
    }

    public static string WriteEvent(string uid, string summary, DateTimeOffset start, DateTimeOffset end, string? location, string? description, DateTimeOffset now)
    {
        var text = new StringBuilder();
        text.Append("BEGIN:VCALENDAR\r\nVERSION:2.0\r\nPRODID:-//Orbs//Orbs//EN\r\nBEGIN:VEVENT\r\n");
        text.Append("UID:").Append(uid).Append("\r\n");
        text.Append("DTSTAMP:").Append(Utc(now)).Append("\r\n");
        text.Append("DTSTART:").Append(Utc(start)).Append("\r\n");
        text.Append("DTEND:").Append(Utc(end)).Append("\r\n");
        text.Append("SUMMARY:").Append(Escape(summary)).Append("\r\n");
        if (!string.IsNullOrWhiteSpace(location))
        {
            text.Append("LOCATION:").Append(Escape(location)).Append("\r\n");
        }

        if (!string.IsNullOrWhiteSpace(description))
        {
            text.Append("DESCRIPTION:").Append(Escape(description)).Append("\r\n");
        }

        text.Append("END:VEVENT\r\nEND:VCALENDAR\r\n");
        return text.ToString();
    }

    private static DateTimeOffset? ParseDate(string value)
    {
        string[] formats = ["yyyyMMdd'T'HHmmss'Z'", "yyyyMMdd'T'HHmmss", "yyyyMMdd"];
        var utc = value.EndsWith('Z');
        return DateTime.TryParseExact(value, formats, CultureInfo.InvariantCulture, DateTimeStyles.AssumeUniversal | DateTimeStyles.AdjustToUniversal, out var parsed)
            ? new DateTimeOffset(DateTime.SpecifyKind(parsed, utc ? DateTimeKind.Utc : DateTimeKind.Utc))
            : null;
    }

    private static string Utc(DateTimeOffset moment) => moment.UtcDateTime.ToString("yyyyMMdd'T'HHmmss'Z'", CultureInfo.InvariantCulture);

    private static string Escape(string text) =>
        text.Replace("\\", "\\\\", StringComparison.Ordinal).Replace(";", "\\;", StringComparison.Ordinal)
            .Replace(",", "\\,", StringComparison.Ordinal).Replace("\r\n", "\\n", StringComparison.Ordinal).Replace("\n", "\\n", StringComparison.Ordinal);

    private static string Unescape(string text) =>
        text.Replace("\\n", "\n", StringComparison.OrdinalIgnoreCase).Replace("\\,", ",", StringComparison.Ordinal)
            .Replace("\\;", ";", StringComparison.Ordinal).Replace("\\\\", "\\", StringComparison.Ordinal);
}
