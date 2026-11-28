# Self-hosting Orbs

## Requirements

- Docker with Compose v2
- 512 MB RAM is enough for Orbs and PostgreSQL; models run elsewhere (a provider, or Ollama on any machine)

## Install

```bash
git clone https://github.com/stijntersteege/orbs.git && cd orbs
cp .env.example .env
# edit .env: set POSTGRES_PASSWORD and ORBS_PUBLIC_URL
docker compose up -d
```

Open `http://<server>:8080` and create the first account. It becomes the admin and receives the demo orb and anything else already on the server. Check health with `curl http://<server>:8080/health`.

## Accounts and sign-in

Every person on the server has their own account with their own orbs, models, context and chats. Admins add people under **Settings and more > Accounts**; they can then change their password and add passkeys under **Account**.

| Setting | What it does |
| --- | --- |
| `Orbs__PublicUrl` | The address people open Orbs on. Passkeys only work on this address. |
| `Orbs__ApiToken` | Optional. Scripts that send it as a bearer token act as the first admin. |
| `Orbs__Auth__Oidc__Authority` | Your OpenID Connect provider (Authentik, Authelia, Keycloak, Google, ...). Turns on a "Continue with ..." button. |
| `Orbs__Auth__Oidc__ClientId`, `ClientSecret` | The client registered at that provider. Its redirect URI is `<PublicUrl>/api/auth/oidc/callback`. |
| `Orbs__Auth__Oidc__DisplayName` | The provider's name on the button. |
| `Orbs__Auth__Oidc__CreateAccounts` | `true` creates an account for anyone the provider signs in. Default `false`: only existing accounts (matched by verified email) can use it. |

The mobile app and other clients sign in the same way and send the session token as a bearer token.

Prefer a prebuilt image? Replace `build: .` in `compose.yaml` with the published `image: ghcr.io/stijntersteege/orbs:<version>`.

## HTTPS with a reverse proxy

Orbs speaks plain HTTP on port 8080. Put a proxy in front for HTTPS. With [Caddy](https://caddyserver.com):

```
orbs.example.com {
    reverse_proxy localhost:8080 {
        flush_interval -1   # stream replies immediately
    }
}
```

With nginx, add `proxy_buffering off;` for `/api/` so streamed replies are not held back.

## Using local models

Ollama on the Docker host is reachable from the container at `http://host.docker.internal:11434/v1` (Docker Desktop). On Linux, add this to the `orbs` service in `compose.yaml`:

```yaml
extra_hosts:
  - "host.docker.internal:host-gateway"
```

## Backups

Everything lives in PostgreSQL, including the key ring that decrypts provider API keys. Back up both together:

```bash
docker compose exec db pg_dump -U orbs orbs > orbs-$(date +%F).sql
```

Restore into an empty database with `psql`. For the single-container SQLite setup, copy `/data/orbs.db` while Orbs is stopped.

## Upgrading

```bash
git pull
docker compose up -d --build
```

Database migrations run automatically on start. Read the [changelog](../../CHANGELOG.md) before upgrading across minor versions.

## Turning off the demo

Set `ORBS_SEED_DEMO_DATA=false` before the first start, or delete the Echo provider and Pip in the app.
