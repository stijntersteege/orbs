# Adding a model provider

Most providers already work through the **OpenAI-compatible** adapter: OpenAI, OpenRouter, Ollama, LM Studio, vLLM, LiteLLM, Groq, Mistral and more. Try that first; you may only need a [preset](#adding-a-preset).

Write a new adapter when a provider has its own API.

## 1. Implement `IModelAdapter`

Create `backend/src/Orbs.Infrastructure/Models/MyProviderModelAdapter.cs`:

```csharp
using System.Runtime.CompilerServices;
using Orbs.Application.Abstractions;

namespace Orbs.Infrastructure.Models;

internal sealed class MyProviderModelAdapter(IHttpClientFactory httpClientFactory) : IModelAdapter
{
    public string Kind => "my-provider";              // stable, lower-case, stored in the database
    public string DisplayName => "My Provider";
    public string Description => "Models from My Provider with your own key.";
    public bool RequiresBaseUrl => false;
    public bool RequiresApiKey => true;
    public Uri? DefaultBaseUrl => new("https://api.my-provider.example/v1");

    public async IAsyncEnumerable<string> StreamChatAsync(
        ProviderConnection connection,
        ChatRequest request,
        [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        // 1. Map request.Messages (System, User, Assistant) to the provider format.
        // 2. Send with connection.ApiKey (already decrypted) to connection.BaseUrl.
        // 3. Yield each text fragment as it arrives.
        yield break;
    }

    public Task<IReadOnlyList<ModelInfo>> ListModelsAsync(ProviderConnection connection, CancellationToken cancellationToken = default)
    {
        // Return the models the provider offers, or a fixed list.
        return Task.FromResult<IReadOnlyList<ModelInfo>>([new ModelInfo("my-model-1")]);
    }
}
```

Rules:

- Keep the class `internal`; an architecture test enforces it.
- Throw `HttpRequestException` for transport problems. Mid-stream failures become an `error` event automatically.
- Never log the API key.

## 2. Register it

In `backend/src/Orbs.Infrastructure/DependencyInjection.cs`:

```csharp
services.AddSingleton<IModelAdapter, MyProviderModelAdapter>();
```

That is all. `GET /api/providers/kinds` now lists it, and the app can connect it.

## 3. Test it

- Unit-test parsing and mapping in `Orbs.Infrastructure.Tests` (see `AdapterTests` for the OpenAI-compatible stream parser).
- Do not call the real provider in automated tests.

## Adding a preset

A preset is a friendly starting point on top of an existing kind. Add an entry to `frontend/src/features/models/presets.ts`:

```ts
{ id: 'groq', name: 'Groq', kind: 'openai-compatible', baseUrl: 'https://api.groq.com/openai/v1', needsKey: true, hint: 'Very fast hosted open models.' }
```
