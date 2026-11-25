# Configuration

Orbs uses standard ASP.NET Core configuration: `appsettings.json`, then environment variables, then command-line arguments. In environment variables, nested keys use a double underscore (`Orbs:ApiToken` becomes `Orbs__ApiToken`).

## Server

| Key | Default | Description |
| --- | --- | --- |
| `Database:Provider` | `Sqlite` | `Sqlite` or `Postgres`. |
| `ConnectionStrings:Orbs` | `Data Source=orbs.db` | SQLite file or PostgreSQL connection string (`Host=db;Database=orbs;Username=orbs;Password=...`). The container default is `/data/orbs.db`. |
| `Orbs:ApiToken` | empty | When set, every `/api` request needs `Authorization: Bearer <token>`. Enter the same token in the app under Settings. |
| `Orbs:SeedDemoData` | `true` | On an empty database, create the Echo provider, a welcome note and Pip. |
| `Orbs:Cors:Origins` | `[]` (Development: localhost dev ports and Capacitor) | Origins allowed to call the API from another host. Example: `Orbs__Cors__Origins__0=https://app.example.com`. |
| `ASPNETCORE_HTTP_PORTS` | `8080` in the container | Port Kestrel listens on. |
| `Logging:LogLevel:Default` | `Information` | Standard logging levels. |

## Compose (`.env`)

| Variable | Default | Maps to |
| --- | --- | --- |
| `ORBS_PORT` | `8080` | Host port for Orbs |
| `POSTGRES_PASSWORD` | `orbs` | Database password (change it) |
| `POSTGRES_PORT` | `5432` | Host port for PostgreSQL, bound to localhost |
| `ORBS_API_TOKEN` | empty | `Orbs__ApiToken` |
| `ORBS_SEED_DEMO_DATA` | `true` | `Orbs__SeedDemoData` |

## Client (web and mobile)

| Setting | Where | Description |
| --- | --- | --- |
| Server address | Settings > Server | Empty means the same origin as the page. The mobile app needs the full URL. |
| API token | Settings > Server | Sent as a bearer token when set. |
| Theme, animations | Settings > Appearance | Stored on the device only. |
| `VITE_API_URL` | build-time env | Default server address baked into a build, for example for a branded mobile app. |
| `ORBS_API_URL` | dev server env | Where `pnpm dev` proxies `/api` (default `http://localhost:5080`). |
