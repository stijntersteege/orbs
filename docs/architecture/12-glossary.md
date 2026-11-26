# 12. Glossary

| Term | Meaning |
| --- | --- |
| **Orb** | An AI companion: a model, its context and a face. |
| **Appearance** | The face of an orb: color, shape, eyes, mouth, accessory, blush, glow, float. |
| **Brief** | The instructions that tell an orb how to behave (stored as `Instructions`). |
| **Provider** | A connected source of models, such as an OpenAI-compatible endpoint or a local runtime. |
| **Kind** | The key that names a plugin (`openai-compatible`, `echo`, `text`, `url`). Stored with the data. |
| **Adapter** | The class that talks to one kind of provider (`IModelAdapter`). |
| **Connector** | The class that reads one kind of context source (`IContextConnector`). |
| **Context source** | Something orbs may know: a note, a web page. Connected once, linked to chosen orbs. |
| **Sync** | Reading a context source again to refresh what orbs see. |
| **Auto** | An orb without a pinned model uses the default model of the oldest enabled provider. |
| **Conversation** | A chat between a person and one orb, made of messages. |
| **Echo** | The built-in demo provider; it repeats what you say. |
| **SSE** | Server-sent events: the one-way HTTP stream used for replies. |
