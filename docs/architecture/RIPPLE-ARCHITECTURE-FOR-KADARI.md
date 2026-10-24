# The Ripple architecture — the reference for building KADARI

KADARI and Ripple are built the same way: same stack, same layering, same rules. Ripple is the further-along of
the two, so where KADARI is missing a piece (a Worker, the outbox, an API test host), build it the way Ripple
does. This file is the reference for that. It travels with the KADARI repo so a cloud session can read it without
access to Ripple.

---

## 1. The shape in one picture

```
             ┌──────────── Aspire AppHost (local dev: starts everything below) ───────────┐
             │                                                                            │
  Browser ─▶ Client SPA (React 19 + Vite)  ─/api─▶  WebApi (Minimal APIs, thin)            │
  Insurer ─▶ Portal SPA                     ─/api─▶      │  sender.Send(command|query)     │
                                                         ▼                                 │
                                           Application (CQRS slices + pipeline)            │
                                                         │ uses                            │
                                                         ▼                                 │
                                           Domain (entities, invariants, events)           │
                                                         ▲ implements abstractions         │
                                           Infrastructure (EF Core/Postgres, S3/MinIO,     │
                                             MassTransit outbox, provider adapters)        │
                                                         │ integration events (outbox)     │
                                                         ▼                                 │
                                           RabbitMQ ──▶ Worker (the ONLY consumer:         │
                                                         consumers + scheduled sweeps)     │
             │  Migration runner: applies EF migrations + seeds, exits; API/Worker wait.   │
             └────────────────────────────────────────────────────────────────────────────┘
```

Dependencies point inward only: `WebApi / Worker → Application → Domain`. `Infrastructure` implements
`Application` abstractions. `Domain` references nothing. An architecture test fails the build if this is broken.

## 2. Projects

| Project | What lives there | KADARI today |
|---|---|---|
| `AppHost` | Aspire: Postgres 17, RabbitMQ, Mailpit, MinIO, migration runner, WebApi, Worker, SPAs | Has Postgres, MinIO, Mailpit, WebApi, Client, Portal. **No RabbitMQ, no Worker.** |
| `ServiceDefaults` | OpenTelemetry, health checks, HTTP resilience, service discovery | ✅ |
| `WebApi` | `Endpoints/{Aggregate}Endpoints.cs` (bind → send → map) + composition root + Scalar docs | ✅ |
| `Worker` | Integration-event consumers and `BackgroundService` sweeps that dispatch Application commands | **Missing** — needed for Care reminders, retries, registry freshness |
| `Migration` | Applies migrations and seeds reference data, then exits | ✅ (project exists) |
| `Application` | `Features/{Aggregate}/{UseCase}/` slices, abstractions, pipeline behaviours | ✅, but some features sit flat (`Features/GetAsset`) instead of under an aggregate |
| `Domain` | Entities, value objects, domain events, invariants. No dependencies | ✅ |
| `Infrastructure` | EF Core config per entity, migrations, outbox, storage, provider adapters | ✅ (outbox publisher is a no-op today) |
| `Contracts` | Integration events, dependency-free | **Missing** |
| `tests/*` | Architecture, Domain, Application, Infrastructure, Integration (real API + Testcontainers) | No API-level integration test host yet |

## 3. A vertical slice

One use case = one folder: `Application/Features/{Aggregate}/{UseCase}/`.

```
Features/Care/
  EnableCare/
    EnableCareCommand.cs    # ICommand<T> + response + validator + handler in one file
  GetCareProfile/
    GetCareProfileQuery.cs  # IQuery<T> + DTOs + handler (AsNoTracking / projection)
  Reactions/                # INotificationHandler<DomainEventNotification<T>>, named On{Event}{Action}
  Services/                 # the feature's own collaborators
```

- Handlers are `internal sealed`; requests and responses are `sealed record`s; DTOs never expose entities.
- Split the handler or validator into its own file only when the request file outgrows ~100 lines.
- Handlers and validators are auto-registered: adding a slice never touches DI.
- Endpoints do three things: bind the request, `sender.Send`, map the result. No logic, no `DbContext`.
- Every request declares its access scope: `IResourceScopedRequest` (the caller must own the resource, checked
  by an `IResourceOwnerResolver<T>` in the same slice → 403 or 404) or `IUnscopedRequest` (+ a one-line comment
  saying why). The architecture test fails otherwise.

## 4. The request pipeline

`endpoint → MediatR → behaviours → handler`:

1. **Validation** (FluentValidation) → 400 with field errors.
2. **Resource authorisation** → 403 for someone else's resource, 404 if it doesn't exist.
3. **Transactional dispatch** — the handler, its domain events and the outbox write commit in **one**
   transaction (`IUnitOfWork`). Domain events are handled *inside* that transaction.
4. **Cache eviction** — commands that change cached reads drop their tag *after* commit.

## 5. Domain rules

- **Not anemic.** Invariants live on aggregates (factory methods, state transitions, guard clauses). Handlers
  orchestrate; they don't decide.
- **Strongly-typed ids:** `record struct AssetId(Guid)`, UUIDv7, generated in the app.
- **Money:** `long` minor units + ISO-4217 currency in a `Money` value object; round half away from zero.
- **Time:** inject `TimeProvider`; never `DateTime.Now`. Everything stored is UTC (`timestamptz`).
- **State machines are explicit:** a status enum + methods that refuse illegal moves (e.g. a Care case can't go
  from `Opened` to `Resolved` without a route). Care's doc 07 maps directly onto this.
- **Versioned rules:** a rule version never changes once live; re-using a code makes the next draft version;
  every decision records the exact rule version that produced it. (Ripple's commission rules work this way — Care's
  coverage/registration/routing rules should too.)

## 6. Persistence

- PostgreSQL, one schema, PascalCase plural tables, `IEntityTypeConfiguration<T>` per entity (auto-discovered).
- Enums stored as text, `jsonb` for open shapes, `xmin` optimistic concurrency.
- **Nothing is hard-deleted:** soft-delete filter + audit. Ledgers and histories are append-only.
- No database defaults (C# initialisers); all foreign keys `Restrict`.
- **Tenancy:** tenant-owned entities are filtered by a named query filter from `ITenantContext`; no tenant ⇒
  nothing visible (fails closed). Only the Worker/migrations use the system tenant.
- **Idempotency:** unique keys on external ids (Ripple: `external_event_id`) so retries can't double-write.
  Care's rule 11 (no duplicate registrations / OEM cases) is exactly this: an idempotency key per external action,
  unique in the database.
- Migrations: `dotnet ef migrations add <Name> --project src/<X>.Infrastructure --startup-project src/<X>.Infrastructure --output-dir Persistence/Migrations`.

## 7. Side effects and background work

- **Default is synchronous**: request → one transaction → response.
- **External calls never run inside the request transaction** (Npgsql's retry strategy can re-run the whole
  thing). Instead publish an integration event through `IIntegrationEventPublisher` → MassTransit **outbox**
  (same transaction, deterministic message id) → RabbitMQ → a **public** consumer in the Worker does the call and
  records the result with another command.
- **Scheduled work** = `BackgroundService` sweeps in the Worker that dispatch Application commands
  (Ripple: automation tick every minute, retention sweeps, report delivery at 07:00 SAST). No Quartz or Hangfire.
  For Care: warranty-expiry reminders, registration retries, follow-ups on OEM commitments, registry freshness.
- **Providers sit behind ports** (`IEmailSender`, `IWhatsAppSender`, …) with a logging or Mailpit adapter in dev.
- **Uploaded files** go through `IObjectStorage`: presigned PUT/GET, bytes never pass through the API, keys are
  ours (`tenants/{tenant}/…/{id}`), the bucket is private. (KADARI already envelope-encrypts objects — keep that.)
- **Outbound HTTP to addresses users or registries supply** goes through an SSRF-safe connector that refuses
  private addresses after DNS resolution.

## 8. AI

One writer: every model call goes through a single abstraction that checks feature switches, caches answers,
routes by tier (cheap vs premium model) and writes one usage row per call (tokens, cost). Nothing AI-written is
saved or sent until a person accepts it, and every draft is checked (no invented facts; gaps left in [brackets]).
Care's principle "AI interprets. Rules govern. Evidence proves. The customer authorises." is the same idea: the
model extracts and drafts; versioned rules decide; the customer confirms.

## 9. Identity and access

Own auth: user account + JWT access token + refresh cookie, email/SMS code sign-in. Role gates at the edge
(`.RequirePermission(...)`), resource gates in the pipeline (§3). Sensitive fields encrypted with a field
protector (AES-256-GCM, versioned key ids, rotation sweep); responses carry only the last four digits.

## 10. Testing

- Test what can break a rule: invariants, state machines, money maths, tenant isolation, idempotency, contracts.
  One or two tests per slice — not one per method.
- Domain tests are pure (no DB, no mocks). Persistence and API tests use one shared Testcontainers Postgres and one
  shared API host; each test creates its own tenant/customer so tests never see each other's rows.
- Deterministic: fixed `TimeProvider`, no sleeps, no test-order dependence. Naming `Method_Scenario_ExpectedResult`.

## 11. Delivery habits

- Each step is its own commit with a detailed message before the next starts.
- Branch per developer, bring `main` in before merging, `main` only fast-forwards.
- When an endpoint changes, regenerate the client's typed API and commit the OpenAPI document with it.
- `CLAUDE.md` at the repo root is the contract — update it when a rule is added.

## 12. What KADARI needs from this for Care

| Care need (doc) | Ripple pattern to copy |
|---|---|
| Reminders, follow-ups, retries (07, 11) | Worker + `BackgroundService` sweeps dispatching commands |
| Registrations / OEM cases without duplicates (07 §retry) | Outbox + idempotency key unique in the DB |
| Versioned, auditable coverage/routing rules (06, 10, 12) | Immutable rule versions + decision evidence rows |
| Asset Case timeline (05) | Append-only history rows, written in the same transaction as the state change |
| Registry import from CSV (10, `REGISTRY/*.csv`) | Migration runner seeds reference data; a plan/apply import (preview, then the same plan applied) |
| Receipt / label extraction (06) | The single AI writer + usage rows; customer confirms before it's saved |
| Proof packs to retailers/OEMs (05, 13) | Presigned storage + least-data rule: send only what the authorised task needs |
| Care Ops review queues (11) | Permission-gated staff endpoints in the Portal |
