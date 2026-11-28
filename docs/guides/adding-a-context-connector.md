# Adding a context connector

A connector teaches orbs to read a new kind of source: a repository, a calendar, a database, an API. Orbs ships with `text` (notes), `url` (web pages, kept in sync on a schedule) and `file` (uploads: PDF, Word, Markdown, text and pictures).

## 1. Implement `IContextConnector`

Create `backend/src/Orbs.Infrastructure/Context/MyConnector.cs`:

```csharp
using Orbs.Application.Abstractions;
using Orbs.Domain.Context;

namespace Orbs.Infrastructure.Context;

internal sealed class RssConnector(IHttpClientFactory httpClientFactory) : IContextConnector
{
    public string Kind => "rss";
    public string DisplayName => "RSS feed";
    public string Description => "Keep up with a blog or news feed.";
    public bool UsesLocation => true;   // the user gives a URL
    public bool UsesInput => false;     // no pasted text

    public async Task<string> ReadAsync(ContextSource source, CancellationToken cancellationToken = default)
    {
        using var client = httpClientFactory.CreateClient();
        var xml = await client.GetStringAsync(source.Location, cancellationToken);
        return ToReadableText(xml);     // return plain text; orbs see exactly this
    }

    private static string ToReadableText(string xml) => xml; // parse titles and summaries here
}
```

Guidelines:

- Return **plain text** that reads well in a prompt. Strip markup and boilerplate.
- Throw on failure with a message a person understands; it is shown on the source as its last error.
- Content longer than 200,000 characters is cut automatically.
- Read only. Connectors never change the source.

## 2. Register it

```csharp
services.AddSingleton<IContextConnector, RssConnector>();
```

The kind appears in `GET /api/context-sources/kinds` and in the app under **Context > Add a source**. Give it an icon in `frontend/src/features/context/ContextPage.tsx` (`KIND_ICON`), otherwise a generic plug icon is used.

## 3. Test it

Unit-test the parsing in `Orbs.Infrastructure.Tests`. For behaviour across the stack, add a scenario to `Orbs.Specs/Features/ConnectingContext.feature`.

## Secrets

If a connector needs credentials (OAuth tokens, API keys), store them encrypted with `ISecretProtector`, just like provider keys. Extending `ContextSource` with a protected secret field is a good first contribution to discuss in an issue.
