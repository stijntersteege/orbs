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
| **Conversation** | A chat between a person and one or more orbs, made of messages. Also called a chat. |
| **Group chat** | A chat with several orbs; mention one with `@Name` to bring it in. |
| **Attachment** | A file added to one chat only; the orb can also be asked to remember it for every chat. |
| **Tool** | Something an orb can do while it answers, such as a calculation, a web search or adding a calendar event (`ITool`). |
| **Tool access** | Per orb and tool: allow, ask first or block. |
| **Approval** | A paused tool call waiting for the person's yes or no. |
| **Integration** | A connected app that brings tools: web search, calendar, email, Home Assistant (`IIntegrationKind`). |
| **MCP server** | A Model Context Protocol server whose tools every orb can use. |
| **Memory** | A lasting fact an orb keeps, for one orb or shared with all. |
| **Retrieval** | Picking the passages of large sources that match a question, using an embedding model. |
| **Skill** | A saved routine any orb can follow, in the SKILL.md format. |
| **Coordinator** | An orb that asks a specialist orb for help with the `ask_orb` tool. |
| **Template** | An orb exported as a file (face, brief, voice, tool settings, schedules) that anyone can import. |
| **Schedule** | Work an orb does on its own: at set times, on a webhook, a feed update or new email. |
| **Job** | A unit of background work, owned by a person, run with retries. |
| **Channel** | A messaging app an orb answers in: Telegram, Matrix, Discord, Signal or email (`IChannelAdapter`). |
| **Voice mode** | Talking to an orb: speech-to-text in, optional text-to-speech out. |
| **Echo** | The built-in demo provider; it repeats what you say. |
| **SSE** | Server-sent events: the one-way HTTP stream used for replies. |
