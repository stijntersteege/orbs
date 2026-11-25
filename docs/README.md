# Orbs documentation

| | |
| --- | --- |
| **[Architecture (arc42)](architecture/README.md)** | Goals, constraints, building blocks, runtime flows, deployment, quality and risks |
| **[Decisions (ADRs)](adr/README.md)** | Why the important choices were made, one record per decision |
| **Guides** | |
| [Self-hosting](guides/self-hosting.md) | Run Orbs with Docker, back it up, put it behind HTTPS |
| [Configuration](guides/configuration.md) | Every setting and its default |
| [Development](guides/development.md) | Local setup, databases, migrations, debugging |
| [Testing](guides/testing.md) | The test pyramid and how to write each kind of test |
| [Adding a model provider](guides/adding-a-model-provider.md) | Plug in a new kind of model API |
| [Adding a context connector](guides/adding-a-context-connector.md) | Teach orbs to read a new kind of source |
| [Mobile](guides/mobile.md) | Build the iOS and Android apps |
| [Design system](guides/design-system.md) | Tokens, the orb mascot, accessibility rules |

The API itself is documented by OpenAPI: a running server serves the document at `/openapi/v1.json` and an interactive reference at `/scalar`.
