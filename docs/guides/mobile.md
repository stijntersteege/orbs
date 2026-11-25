# Mobile apps

The iOS and Android apps are the web app inside a [Capacitor](https://capacitorjs.com) shell. Every screen is responsive, so there is no separate mobile UI to maintain.

## Build

```bash
cd frontend
pnpm install
pnpm cap:sync                 # builds the web app and copies it into the native projects
npx cap add android           # first time only (creates frontend/android)
npx cap add ios               # first time only, macOS with Xcode (creates frontend/ios)
pnpm cap:android              # opens Android Studio
pnpm cap:ios                  # opens Xcode
```

Run `pnpm cap:sync` after every web change you want in the native app.

## Connect to a server

The app talks to your Orbs server over HTTPS. On first launch, open **Settings > Server** and enter its address (for example `https://orbs.example.com`) and the API token if the server has one.

For development against a local API, use your computer's LAN address (`http://192.168.1.20:5080`) and add the app origin to CORS:

```bash
dotnet run --project backend/src/Orbs.Api -- --Orbs:Cors:Origins:0=capacitor://localhost --Orbs:Cors:Origins:1=http://localhost
```

The Development environment already allows these origins.

To ship a build that points at a fixed server, set `VITE_API_URL` when building:

```bash
VITE_API_URL=https://orbs.example.com pnpm cap:sync
```

## App identity

`frontend/capacitor.config.ts` sets the app id (`org.orbs.app`), name and background color. Change the app id before publishing to a store.

## Installable web app

Without the stores, people can install Orbs from the browser: it ships a web app manifest, so **Add to Home Screen** gives a full-screen app with the Orbs icon.
