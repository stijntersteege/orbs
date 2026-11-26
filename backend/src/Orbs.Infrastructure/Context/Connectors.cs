using System.Net;
using System.Text.RegularExpressions;
using Orbs.Application.Abstractions;
using Orbs.Domain.Context;

namespace Orbs.Infrastructure.Context;

/// <summary>A note the user types or pastes. The simplest possible context.</summary>
internal sealed class TextConnector : IContextConnector
{
    public string Kind => "text";

    public string DisplayName => "Note";

    public string Description => "Type or paste anything your orbs should know.";

    public bool UsesLocation => false;

    public bool UsesInput => true;

    public Task<string> ReadAsync(ContextSource source, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(source);
        return Task.FromResult(source.Input);
    }
}

/// <summary>Reads a public web page or plain-text URL and keeps its readable text.</summary>
internal sealed partial class UrlConnector(IHttpClientFactory httpClientFactory) : IContextConnector
{
    public const string HttpClientName = "context-url";

    public string Kind => "url";

    public string DisplayName => "Web page";

    public string Description => "Paste a link. Orbs reads the page and keeps its text in sync when you ask.";

    public bool UsesLocation => true;

    public bool UsesInput => false;

    public async Task<string> ReadAsync(ContextSource source, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(source);
        if (!Uri.TryCreate(source.Location, UriKind.Absolute, out var uri)
            || (uri.Scheme != Uri.UriSchemeHttp && uri.Scheme != Uri.UriSchemeHttps))
        {
            throw new InvalidOperationException("The location must be an absolute http(s) URL.");
        }

        using var client = httpClientFactory.CreateClient(HttpClientName);
        using var response = await client.GetAsync(uri, cancellationToken);
        response.EnsureSuccessStatusCode();
        var body = await response.Content.ReadAsStringAsync(cancellationToken);
        var mediaType = response.Content.Headers.ContentType?.MediaType ?? string.Empty;
        return mediaType.Contains("html", StringComparison.OrdinalIgnoreCase) ? HtmlToText(body) : body.Trim();
    }

    /// <summary>Good-enough HTML to text: drops scripts, styles and tags, keeps paragraphs.</summary>
    internal static string HtmlToText(string html)
    {
        var text = NonContent().Replace(html, " ");
        text = BlockEnd().Replace(text, "\n");
        text = Tag().Replace(text, " ");
        text = WebUtility.HtmlDecode(text);
        text = Spaces().Replace(text, " ");
        text = BlankLines().Replace(text, "\n\n");
        return text.Trim();
    }

    [GeneratedRegex(@"<(script|style|noscript|svg|head)[^>]*>.*?</\1>", RegexOptions.IgnoreCase | RegexOptions.Singleline)]
    private static partial Regex NonContent();

    [GeneratedRegex(@"</(p|div|h[1-6]|li|tr|section|article)>|<br\s*/?>", RegexOptions.IgnoreCase)]
    private static partial Regex BlockEnd();

    [GeneratedRegex("<[^>]+>")]
    private static partial Regex Tag();

    [GeneratedRegex(@"[ \t\r\f\v]+")]
    private static partial Regex Spaces();

    [GeneratedRegex(@"\s*\n\s*(\n\s*)+")]
    private static partial Regex BlankLines();
}
