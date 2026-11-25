# Self-hosting Orbs

## Requirements

- Docker with Compose v2
- 512 MB RAM is enough for Orbs and PostgreSQL; models run elsewhere (a provider, or Ollama on any machine)

## Install

```bash
git clone https://github.com/your-org/orbs.git && cd orbs
cp .env.example .env
# edit .env: set POSTGRES_PASSWORD, and ORBS_API_TOKEN if the server is reachable from the internet
docker compose up -d
```

Open `http://<server>:8080`. Check health with `curl http://<server>:8080/health`.

Prefer a prebuilt image? Replace `build: .` in `compose.yaml` with the published `image: ghcr.io/your-org/orbs:<version>`.

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
