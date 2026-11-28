# Adding tools, apps and channels

Orbs grows through small plugin classes. This guide covers the three that let orbs act and talk: tools, connected apps (integrations) and channels. For models and context, see [Adding a model provider](adding-a-model-provider.md) and [Adding a context connector](adding-a-context-connector.md).

Never add vendor-specific branches to Domain or Application: put the vendor code in a plugin and register it.

## A tool: `ITool`

A tool is one thing an orb can do while it answers. The model sees `Kind` (its name), `Description` and `ParametersSchema`; people see `DisplayName` and `Summary`.

```csharp
using System.Globalization;
using System.Text.Json;
using Orbs.Application.Abstractions;

namespace Orbs.Infrastructure.Tools;

internal sealed class DiceTool : ITool
{
    public string Kind => "roll_dice";                 // letters, digits, '_' and '-'
    public string DisplayName => "Roll dice";
    public string Description => "Roll dice. Give the number of sides; defaults to 6.";
    public string Summary => "Rolls dice for games and decisions.";
    public string ParametersSchema => """
        {"type":"object","properties":{"sides":{"type":"integer","minimum":2}}}
        """;
    public ToolRisk Risk => ToolRisk.Read;             // Write tools ask first by default

    public Task<string> InvokeAsync(ToolInvocation invocation, CancellationToken cancellationToken = default)
    {
        using var args = JsonDocument.Parse(string.IsNullOrWhiteSpace(invocation.Arguments) ? "{}" : invocation.Arguments);
        var sides = args.RootElement.TryGetProperty("sides", out var value) && value.TryGetInt32(out var n) && n >= 2 ? n : 6;
        return Task.FromResult(Random.Shared.Next(1, sides + 1).ToString(CultureInfo.InvariantCulture));
    }
}
```

Register it in `Orbs.Infrastructure/DependencyInjection.cs`:

```csharp
services.AddSingleton<ITool, DiceTool>();
```

Guidelines:

- Return text the model can read. Throw with a clear message to report a failure; the model sees it and can try again.
- Use `ToolRisk.Write` for anything that changes something or reaches out (sending, booking, switching). People then approve each call unless they allow the tool for an orb.
- `invocation.OrbId` and `invocation.UserId` say who is asking; repositories are already filtered to that person.
- Reach the network through `IHttpClientFactory` and, for addresses people type, the restricted client that applies the private-network check.

A set of tools that comes from elsewhere (like an MCP server) is an `IToolSource`. Orbs merges all sources into one list.

## A connected app: `IIntegrationKind`

An integration is an app people connect once under **Tools > Connected apps**, which brings tools for every orb. Examples in `Orbs.Infrastructure/Integrations`: web search, CalDAV, email and Home Assistant.

- `Fields` describes the form: label, placeholder, hint, whether it is required, and whether it is the secret (stored encrypted, never shown again).
- `CreateTools(IntegrationConfig config)` returns the tools for one connected account. `config.Settings` holds the non-secret fields, `config.Secret` the decrypted secret, and `config.RestrictNetwork` whether to use the restricted HTTP client.
- `TestAsync` checks the connection for the **Test** button.

Register with `services.AddSingleton<IIntegrationKind, MyIntegration>();`. The form appears in the app automatically.

## A channel: `IChannelAdapter`

A channel lets people talk to an orb from a messaging app. Examples in `Orbs.Infrastructure/Channels`: Telegram, Matrix, Discord, Signal and email.

- `Fields` describes the connect form, like integrations.
- `ConnectAsync` checks the credentials and returns the address to show (a bot name, an account).
- `PollAsync(config, cursor)` returns new messages since the cursor, plus the next cursor. Orbs polls, so servers need no public address.
- `SendAsync` sends the orb's reply to a chat.
- `SetProfileAsync` sets the bot's name, description and picture where the app allows it.

Pairing is handled for you: a message containing the one-time code pairs that chat with the orb.

## Tests

- Unit-test parsing and mapping in `Orbs.Infrastructure.Tests`, with a fake HTTP handler.
- Add an API integration test that uses the plugin through the API, and a Reqnroll scenario for anything people see.
- Mirror API contract changes in the in-browser demo API (`frontend/src/demo/server.ts`).
