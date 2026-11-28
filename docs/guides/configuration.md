# Configuration

Orbs uses standard ASP.NET Core configuration: `appsettings.json`, then environment variables, then command-line arguments. In environment variables, nested keys use a double underscore (`Orbs:ApiToken` becomes `Orbs__ApiToken`).

## Server

| Key | Default | Description |
| --- | --- | --- |
| `Database:Provider` | `Sqlite` | `Sqlite` or `Postgres`. |
| `ConnectionStrings:Orbs` | `Data Source=orbs.db` | SQLite file or PostgreSQL connection string (`Host=db;Database=orbs;Username=orbs;Password=...`). The container default is `/data/orbs.db`. |
| `Orbs:PublicUrl` | `http://localhost:8080` | The address people open Orbs on. Passkeys only work on this address, and the sign-in cookie is `Secure` when it is https. |
| `Orbs:SeedDemoData` | `true` | On an empty database, create the Echo provider, a welcome note and Pip. |
| `Orbs:Cors:Origins` | `[]` | Origins allowed to call the API from another host, such as a separately hosted web app. Example: `Orbs__Cors__Origins__0=https://app.example.com`. |
| `ASPNETCORE_HTTP_PORTS` | `8080` in the container | Port Kestrel listens on. |
| `Logging:LogLevel:Default` | `Information` | Standard logging levels. |

## Accounts and sign-in

The first account you create becomes the admin. Admins add other accounts under **Accounts**.

| Key | Default | Description |
| --- | --- | --- |
| `Orbs:ApiToken` | empty | Optional token for scripts. Requests that send it as `Authorization: Bearer <token>` act as the first admin. |
| `Orbs:Auth:Oidc:Authority` | empty | Your OpenID Connect provider (Authentik, Authelia, Keycloak, Google, ...). With `ClientId`, it turns on a "Continue with ..." button. |
| `Orbs:Auth:Oidc:ClientId`, `ClientSecret` | empty | The client registered at that provider. Its redirect URI is `<PublicUrl>/api/auth/oidc/callback`. |
| `Orbs:Auth:Oidc:Scopes` | `openid profile email` | Scopes to ask for, separated by spaces. |
| `Orbs:Auth:Oidc:DisplayName` | `Single sign-on` | The provider's name on the button. |
| `Orbs:Auth:Oidc:CreateAccounts` | `false` | `true` creates an account for anyone the provider signs in. Otherwise only existing accounts (matched by verified email) can use it. |

## Security

| Key | Default | Description |
| --- | --- | --- |
| `Orbs:TrustForwardedHeaders` | `false` | Turn on when Orbs is only reachable through a reverse proxy. Orbs then reads each visitor's address and https from `X-Forwarded-*` headers. Leave it off when port 8080 is open to others. |
| `Orbs:RateLimits:SignInPerMinute` | `10` | Sign-in, first-run setup, passkey sign-in and password-change attempts per address per minute. |
| `Orbs:RateLimits:HooksPerMinute` | `60` | Webhook calls per address per minute. |

## What Orbs may fetch

Web pages, feeds and push endpoints, plus model providers, apps, channels and MCP servers added by people who are not admins, may not reach private, loopback or link-local addresses.

| Key | Default | Description |
| --- | --- | --- |
| `Orbs:Network:AllowedHosts` | empty | Comma-separated hosts that may be on a private network, such as `wiki.lan,nas.lan`. Subdomains count too. |
| `Orbs:Network:BlockedHosts` | empty | Hosts Orbs never fetches, even public ones. |
| `Orbs:Network:AllowPrivateNetworks` | `false` | `true` turns the private-network check off. Only for a server nobody else uses. |

## Tools, background work and notifications

| Key | Default | Description |
| --- | --- | --- |
| `Orbs:Mcp:AllowStdio` | `false` | Lets admins add local (stdio) MCP servers, which run a command on the Orbs server. HTTP MCP servers always work. |
| `Orbs:Jobs:Enabled` | `true` | The background worker that runs schedules, syncs web pages, polls channels and sends notifications. |
| `Orbs:Jobs:PollSeconds` | `5` | How often the worker looks for due work. |
| `Orbs:Push:VapidPublicKey`, `VapidPrivateKey` | generated | The key pair that identifies the server to push services. When empty, Orbs makes one on first use and keeps it in the database, with the private key encrypted. Set both to keep the same keys across fresh installs. |
| `Orbs:Push:Subject` | `PublicUrl` when https | Contact for push services, such as `mailto:admin@example.com`. |

## Compose (`.env`)

| Variable | Default | Maps to |
| --- | --- | --- |
| `ORBS_PORT` | `8080` | Host port for Orbs |
| `POSTGRES_PASSWORD` | `orbs` | Database password (change it) |
| `POSTGRES_PORT` | `5432` | Host port for PostgreSQL, bound to localhost |
| `ORBS_PUBLIC_URL` | `http://localhost:8080` | `Orbs__PublicUrl` |
| `ORBS_TRUST_FORWARDED_HEADERS` | `false` | `Orbs__TrustForwardedHeaders` |
| `ORBS_API_TOKEN` | empty | `Orbs__ApiToken` |
| `ORBS_OIDC_AUTHORITY`, `ORBS_OIDC_CLIENT_ID`, `ORBS_OIDC_CLIENT_SECRET`, `ORBS_OIDC_DISPLAY_NAME` | empty, `Single sign-on` | `Orbs__Auth__Oidc__*` |
| `ORBS_SEED_DEMO_DATA` | `true` | `Orbs__SeedDemoData` |

## In the app

Models (including embedding, speech-to-text and text-to-speech models), context, tools, connected apps, MCP servers, skills, schedules and channels are all set up in the app, per account. Secrets entered there are stored encrypted and never shown again.

## Client (web and mobile)

| Setting | Where | Description |
| --- | --- | --- |
| Server address | Settings > Server | Empty means the same origin as the page. The mobile app needs the full URL. |
| API token | Settings > Server | Sent as a bearer token when set. |
| Theme, animations, reading replies aloud | Settings and the chat | Stored on the device only. |
| `VITE_API_URL` | build-time env | Default server address baked into a build, for example for a branded mobile app. |
| `ORBS_API_URL` | dev server env | Where `pnpm dev` proxies `/api` (default `http://localhost:5080`). |
