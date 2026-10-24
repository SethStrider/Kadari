# KADARI production deployment

One VPS, Docker Compose, the edge nginx in this folder, and Cloudflare in front. Adapted from the Trustli runbook when
KADARI was extracted (October 2026). Since KADARI Care (October 2026) the API publishes integration events through a
transactional outbox to RabbitMQ, and a worker consumes them and runs the scheduled sweeps.

```
Cloudflare (DNS, TLS, WAF, rate rules, Access on portal.)
   │  443, mTLS (Authenticated Origin Pulls), CF-Connecting-IP
   ▼
edge nginx ──► client (app.)      ──► portal (portal.)
   │ /api
   ▼
webapi ──► postgres · minio · rabbitmq   (backend network: internal, nothing published)
       ──► Twilio, the vision model, SMTP (egress network)
worker ◄── rabbitmq; ──► postgres · minio; ──► providers (egress)
backup ──► nightly encrypted dump + blob sync to an off-site bucket (another provider)
```

## What only you can set up

1. **VPS** in a South African region with encrypted volumes. Ubuntu 24.04, SSH keys only, Docker Engine + compose
   plugin. Firewall: `443/tcp` from [Cloudflare's ranges](https://www.cloudflare.com/ips/) only, `22/tcp` from your IP.
2. **Cloudflare**: the zone; proxied `A` records for `app.` and `portal.`; SSL/TLS **Full (strict)**; an **Origin CA
   certificate** for `*.<domain>` saved as `deploy/certs/origin.pem` + `origin.key`; **Authenticated Origin Pulls**
   on, with Cloudflare's origin-pull CA saved as `deploy/certs/cloudflare-origin-pull-ca.pem`
   ([download](https://developers.cloudflare.com/ssl/origin-configuration/authenticated-origin-pull/set-up/zone-level/));
   HSTS; WAF managed rules; rate rules on `/api/v1/auth/*`; **Access** (SSO + MFA) on `portal.`.
3. **Accounts and keys** listed in `deploy/.env.example`: Twilio (an SMS sender, or an approved WhatsApp
   authentication template), an SMTP provider with SPF/DKIM/DMARC, a vision-model key if capture should use AI
   (Gemini paid tier or an OpenAI-compatible endpoint), and the off-site bucket.
4. **Secrets**: `cp deploy/.env.example deploy/.env`, fill it in (`openssl rand -base64 48` per secret), and store the
   file in the password manager with two custodians. `FIELD_ENCRYPTION_KEY` and `BACKUP_ENCRYPTION_PASSWORD`/`_SALT`
   also get a sealed offline copy.

## First deploy

```bash
# Once per server: the MinIO image is our mirror in the private GitLab registry. Use a deploy token with only
# read_registry (mccarthyfam > Settings > Repository > Deploy tokens); never a personal access token.
docker login registry.gitlab.com -u <deploy-token-username>

cd /srv/kadari
export GIT_SHA=$(git rev-parse --short HEAD)
C="docker compose -f deploy/docker-compose.prod.yml --env-file deploy/.env"

$C --profile tools build
$C up -d postgres minio rabbitmq          # roles and the private bucket are created on first start
$C run --rm migrations                    # schema + seeds as kadari_migrator; non-zero exit = stop
$C up -d
$C ps                                     # every service healthy (backup has no health check)
```

Sign in to the portal as `ADMIN_MOBILE` (OTP) or `ADMIN_EMAIL`, then run the first backup and the restore drill below
before real users.

## Every deploy

```bash
git pull && export GIT_SHA=$(git rev-parse --short HEAD)
$C --profile tools build
$C run --rm migrations
$C up -d
```

Rollback: check out the previous commit and repeat. Migrations are forward-only, so review any migration that drops or
renames before deploying it; undoing one needs a restore.

## Backups and the restore drill

The `backup` service dumps the database and syncs the MinIO bucket every night at `BACKUP_HOUR_UTC`, encrypted
client-side (rclone crypt) before it leaves the VPS, and pings `BACKUP_HEARTBEAT_URL` on success — alert on a missed
ping. RPO is 24 hours until point-in-time recovery (WAL-G) is added.

```bash
$C exec backup backup.sh run                          # a backup now
$C exec backup restore.sh list                        # dumps available off-site

# Drill: restore the latest dump next to the live database, compare, drop.
$C exec -e PGUSER=postgres -e PGPASSWORD="$DB_SUPERUSER_PASSWORD" backup restore.sh db latest kadari_restore
$C exec postgres psql -U postgres -d kadari_restore -c 'SELECT count(*) FROM kadari."Assets"'
$C exec postgres psql -U postgres -c 'DROP DATABASE kadari_restore'

# Blobs: the app's MinIO user can reach only the kadari bucket, so a blob restore goes there.
$C exec backup restore.sh blobs kadari
```

Stored photos and receipts are envelope-encrypted by the API, so a blob restore is useless without the
`FIELD_ENCRYPTION_KEY` (and any retired key) that wrapped them.

For a real recovery on a fresh VPS: deploy as above but stop after `up -d postgres minio`, restore the dump into
`kadari` and the blobs into `kadari`, then run `migrations` (a no-op when the dump is current) and `up -d`.

## Monitoring and alerts

Set `OTEL_EXPORTER_OTLP_ENDPOINT` and `OTEL_EXPORTER_OTLP_HEADERS` (a Grafana Cloud OTLP gateway, for example) and the
API ships metrics, traces and logs there. Suggested alert rules:

| Alert | Rule | Means |
| --- | --- | --- |
| Vision model failing | `kadari.snaps{enrichment="enricher_fallback"}` over 20% of `kadari.snaps` for 15 min | Captures still work, but on on-device labels only: check the vision key and provider status |
| Vision quota hit | `kadari.snaps{enrichment="quota_reached"}` rising | Customers are past `Kadari:Vision` daily caps; raise them or look for abuse |
| Server errors | `http.server.request.duration` count with `http.response.status_code >= 500` above a few per minute | The API is failing requests |
| API down | No API metrics for 5 min | The autoheal sidecar restarts it; if it keeps dying, read its logs |

## Routine operations

| Task | Command / action |
| --- | --- |
| Logs (JSON lines) | `$C logs -f webapi` |
| Cloudflare ranges changed | `deploy/scripts/update-cloudflare-ips.sh`, review, commit, `$C exec edge nginx -s reload` |
| Rotate JWT key | Change `JWT_SIGNING_KEY`, `$C up -d webapi` — everyone signs in again |
| Rotate a provider key (Twilio, SMTP, vision) | Rotate in the provider, update `deploy/.env`, `$C up -d webapi` |
| Field-encryption key | Follow the rotation steps in `.env.example`; **never** drop the retired key, because every stored file's own key is wrapped by it |
| Suspected account takeover | Suspend the user in the portal — every session ends at once |
