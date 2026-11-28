# Using Orbs

A tour of everything you can do with Orbs. It assumes Orbs is running (see [Self-hosting](self-hosting.md)) and you have signed in.

## Orbs

An orb is a model, the context it may read and a face. Make one with **New orb** in the sidebar, or open **Settings and more > Manage orbs**.

- **Who is it**: a name and a one-line description.
- **What it knows**: the context sources it may read. It sees nothing else.
- **Its brain**: a pinned model, or **Auto** (the default model of your first enabled provider). You can also switch models from the chat header.
- **What it can do**: per tool, **Allow**, **Ask first** or **Block**.
- **How it behaves**: the brief, written like you would brief a new teammate.
- **A face and a voice**: color, shape, eyes, mouth, accessory, plus a voice for reading replies aloud.

The save button stays at the bottom of the screen. Orbs asks before you leave with unsaved changes.

**Share > Export as template** saves an orb as a file with its face, brief, voice, tool settings and schedules. **Import orb** on Your orbs turns such a file into a new orb. The model, context, chats and memories stay on your account.

## Models

Under **Models**, add a provider: OpenRouter, OpenAI, Anthropic, Google Gemini, a local Ollama or LM Studio, or any OpenAI-compatible endpoint. Keys are stored encrypted. A provider can also have:

- an **embedding model**, which turns on retrieval: orbs read the passages of large sources that match the question;
- a **speech-to-text model**, such as `whisper-1`, which turns on talking to orbs;
- a **text-to-speech model**, which lets orbs answer out loud.

## Chatting

- Replies stream in and show Markdown: headings, lists, tables and highlighted code with a **Copy** button.
- The **Activity** panel shows what the orb read, which tools it used and which model answered. **Memory** shows what it remembers.
- **Chats** lists recent chats; **All chats** opens the full list, where you can open, rename or delete them. The **⋯** menu renames or deletes the open chat.
- Hover a message for **Copy** and **Delete**. A deleted message is gone for the orb too.
- **Attach files** with the paperclip, or drop or paste them into the message box. A file is for that chat only, unless you tick **Remember for (orb), in every chat**.
- **Talk** with the microphone button. If it says it is not set up yet, choose a speech-to-text model under Models, and open Orbs on https or localhost (browsers only allow the microphone there). The speaker button reads replies aloud.
- **Group chats**: type `@Name` to bring another orb in. Several mentioned orbs answer in turn and hear each other.

## Context

Under **Context**, add notes, web pages (kept in sync hourly, daily or weekly), or files: PDF, Word, Markdown, text, CSV, JSON and pictures. Click a source to open it:

- see what it holds, as orbs read it: formatted Markdown, a picture preview, or the text from a document;
- **Edit** a note, a web page's address and sync, or the text of a Markdown, text, CSV or JSON file;
- **Download** files, **Read again** web pages, or delete the source.

Then switch sources on per orb in the orb editor.

## Tools and connected apps

Orbs can use tools while they answer. Built in: current time, calculator, remember and forget, asking another orb, and using a skill. Under **Tools** you can add more:

- **Connected apps**: web search (SearXNG or Brave), a CalDAV calendar, email (IMAP and SMTP) and Home Assistant.
- **MCP servers**: any Model Context Protocol server over HTTP. Admins can also allow local (stdio) servers.

Tools that change something ask first: the reply pauses on an approval card with **Allow once**, **Always allow** and **Decline**. Every call is saved with the reply.

## Memory

Orbs remember lasting facts with the `remember` tool and forget them with `forget`. Open **Memory** in the chat panel to add, edit or forget memories, and to choose whether a memory is for one orb or shared with all your orbs.

## Skills

A skill is a routine any orb can follow. Write one under **Skills**, import a SKILL.md file, or click **Save these steps as a skill** under a reply that used tools. Orbs read skills with the `use_skill` tool. Skills export as SKILL.md files.

## Coordinator orbs

The `ask_orb` tool lets an orb hand a question to one of your other orbs and use its answer. Give one orb a coordinating brief and let it ask the specialists. The asked orb only reads and never acts.

## Schedules

Under **Schedules**, let an orb work on its own: at set times (cron), when another app calls a secret webhook, when a feed has something new, or when new email arrives. Each schedule writes in its own chat and notifies you. **Pause**, **Resume**, **Edit** and **Run now** are on every schedule.

## Channels

Under **Channels**, connect an orb to Telegram, Matrix, Discord, Signal or email. **Pair a chat** gives a one-time code to send from the app. Orbs polls the app, so your server needs no public address.

## Notifications

Turn on notifications under **Account** to hear from orbs when a schedule finishes or a channel message arrives, with quiet hours that hold notifications until morning.

## Accounts

Everyone on a server has their own orbs, models, context and chats. Sign in with a password, a passkey or your OpenID Connect provider. Admins add, edit and remove accounts under **Accounts**; a new password signs that person out everywhere. On **Account** you change your name and password, add passkeys and sign out other devices.
