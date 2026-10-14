# KADARI — Engineering Guide

KADARI is an insurance asset-intelligence product: a household's Live Asset Ledger™ (Σ value × confidence), built from
photo capture, evidence and valuations, with asset passports and an insurer portal. This is the .NET 10 Aspire backend
plus the React customer app and portal. It was extracted from the KADARI monorepo in October 2026 and shares no
runtime with it.

**This file is the contract both developers and Claude follow.** When in doubt, match the patterns
here and the surrounding code. Keep changes consistent over clever.

---

## 1. Golden rules

1. **Vertical slices, not layers of junk drawers.** A feature owns its command/query + handler +
   validator + response, together in one folder. Never a global `Commands/`, `Handlers/`, `Services/` dump.
2. **Dependencies point inward.** `WebApi → Application → Domain`. `Infrastructure` implements
   Application/Domain abstractions. `Domain` references nothing. Never reference `Infrastructure`
   from `Application` or `Domain`.
3. **CQRS is mandatory.** State changes go through `ICommand`; reads go through `IQuery`. No business
   logic in endpoints, no `DbContext` in endpoints.
4. **The domain is not anemic.** Invariants live on entities/aggregates, not in handlers. Handlers
   orchestrate; they don't enforce business rules that belong to the model.
5. **Every slice ships with tests.** No handler or domain rule merges without an xUnit test.
6. **Nothing is hard-deleted.** Soft-delete + audit everywhere (see the data-model conventions).
7. **Async all the way.** Every I/O method takes and honours a `CancellationToken`.

---

## 2. Solution map

```
src/
  Kadari.AppHost          # Aspire orchestrator (Postgres + MinIO + WebApi + client + portal)
  Kadari.ServiceDefaults  # OpenTelemetry / health / resilience / service discovery
  Kadari.WebApi           # Minimal-API endpoints (thin) + composition root
  Kadari.Application      # CQRS slices (Features/…) + abstractions + pipeline behaviours
  Kadari.Domain           # Entities, value objects, domain events, invariants — NO dependencies
  Kadari.Infrastructure   # EF Core, envelope-encrypted S3 storage, sign-in security, vision enrichment
  Kadari.Client           # Customer SPA (React 19 / Vite / TanStack Router), port 8200
  Kadari.Portal           # Insurer + staff portal SPA, port 8300
tests/
  Kadari.Domain.Tests / Kadari.Application.Tests / Kadari.Infrastructure.Tests / Kadari.ArchitectureTests
```

Stack: .NET 10 · Aspire 13 · Minimal APIs + Scalar docs · **MediatR 12.5** (CQRS) · **MassTransit 8**
on RabbitMQ · **EF Core 10 / PostgreSQL** · **AWS S3 SDK → MinIO** · **xUnit**. Package versions are
centralised in `Directory.Packages.props` (CPM) — add packages **without** a `Version` attribute.

---

## 3. Vertical slice anatomy

One folder per use case under `KADARI.Application/Features/{Aggregate}/{UseCase}/`:

```
KADARI.Application/Features/Journeys/
  StartJourney/
    StartJourneyCommand.cs           # the ICommand<T> + response record + the handler
    StartJourneyValidator.cs         # FluentValidation rules (runs in the pipeline)
    StartJourneyOwnerResolver.cs     # who owns the resource (scoped slices only — §4a)
  GetJourney/
    GetJourneyQuery.cs               # the IQuery<T> + its DTOs + the handler
```

**The request file carries its handler.** A slice is small enough to read in one sitting, and keeping
the command, its response shape and the handler together is what makes it readable. Split the handler
into its own `{UseCase}Handler.cs` only when it outgrows roughly 60 lines — several slices already
have, and that is fine. What is not fine is a slice split for no reason: the file count should track
the work, not a template.

Endpoints live in `KADARI.WebApi/Endpoints/{Aggregate}Endpoints.cs` and do **three things only**:
bind the request → `sender.Send(command)` → map the result to an HTTP response.

### Reference command slice

```csharp
// StartJourney/StartJourneyCommand.cs — request, response and handler together
using KADARI.Application.Abstractions.Access;
using KADARI.Application.Abstractions.Messaging;

// Every request declares its authorization scope (§4a). This one names a customer, so it is scoped:
// a caller may only start a journey for themselves.
public sealed record StartJourneyCommand(Guid CustomerId, Guid ProductId)
    : ICommand<StartJourneyResponse>, IResourceScopedRequest
{
    public Guid ResourceId => CustomerId;
}

public sealed record StartJourneyResponse(Guid JourneyId);

internal sealed class StartJourneyHandler(
    IKADARIDbContext db,
    TimeProvider clock)
    : ICommandHandler<StartJourneyCommand, StartJourneyResponse>
{
    public async Task<StartJourneyResponse> Handle(StartJourneyCommand cmd, CancellationToken ct)
    {
        var product = await db.Products.FirstOrDefaultAsync(p => p.Id == cmd.ProductId, ct)
            ?? throw new NotFoundException(nameof(Product), cmd.ProductId);

        // Invariant enforced by the aggregate, not here:
        var journey = TrustJourney.Start(cmd.CustomerId, product, clock.GetUtcNow());

        db.Journeys.Add(journey);
        await db.SaveChangesAsync(ct);   // domain events dispatched via SaveChanges / outbox

        return new StartJourneyResponse(journey.Id);
    }
}
```

```csharp
// StartJourney/StartJourneyValidator.cs
using FluentValidation;

internal sealed class StartJourneyValidator : AbstractValidator<StartJourneyCommand>
{
    public StartJourneyValidator()
    {
        RuleFor(x => x.CustomerId).NotEmpty();
        RuleFor(x => x.ProductId).NotEmpty();
    }
}
```

```csharp
// KADARI.WebApi/Endpoints/JourneyEndpoints.cs
public static class JourneyEndpoints
{
    public static IEndpointRouteBuilder MapJourneyEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/journeys").WithTags("Journeys");

        group.MapPost("/", async (StartJourneyCommand cmd, ISender sender, CancellationToken ct) =>
        {
            var result = await sender.Send(cmd, ct);
            return Results.Created($"/api/journeys/{result.JourneyId}", result);
        })
        .WithName("StartJourney")
        .WithSummary("Start a Trust Journey for a customer.");

        return group;
    }
}
// Program.cs: app.MapJourneyEndpoints();   // one line per aggregate — keeps the root thin (OCP)
```

**Handler rules:** handlers are `internal sealed`; one public operation (`Handle`); no cross-slice
handler calls (share via the domain or a small application service). Commands/queries/responses are
`sealed record`s. DTOs never leak EF entities to the API.

**Feature-folder anatomy.** A `Features/{Aggregate}/` folder contains only: `{UseCase}/` slice folders
(request-initiated commands/queries), a **`Reactions/`** folder for domain-event handlers
(`INotificationHandler<DomainEventNotification<T>>`, named **`On{Event}{Action}`**), and a **`Services/`**
folder for the feature's own collaborators (a purpose-named folder like `Rendering/` is fine where it's
clearer). **No loose `.cs` files at a feature root** — everything lives in a slice, a reaction, or a
service. Domain events themselves live in `Domain/{Aggregate}/Events/`.

---

## 3a. Authorization — every request declares its scope

Authorization has **two layers, both mandatory** (`PAS-002 §2`). Getting only the first is what let any
authenticated customer read another customer's report by passing their id.

1. **Role gate — at the edge.** `.RequirePermission(Permission.X)` on the endpoint. Answers *may this
   kind of caller do this kind of thing.*
2. **Resource gate — in the pipeline.** `ResourceAuthorizationBehavior`. Answers *is this resource
   theirs.*

**Every request type under `Features/` implements exactly one of:**

| Marker | Meaning | Also needs |
| --- | --- | --- |
| `IResourceScopedRequest` | Touches a customer-owned resource. Expose `ResourceId`. | An `IResourceOwnerResolver<TRequest>` in the same slice folder |
| `IUnscopedRequest` | Deliberately not customer-scoped — public reads, auth, webhooks, admin. | A one-line comment saying *why* |

This is **enforced by `KADARI.ArchitectureTests`**: a request that declares neither, declares both, or
declares itself scoped without a resolver **fails the build**. If that test fails on a slice you just
added, decide which the request is and say so — do not relax the test.

Two rules the resolvers follow:

- A resource that does not exist resolves to `null` → the behaviour throws **404, not 403**. A 403 on a
  non-existent id tells the caller which ids exist.
- **Staff roles bypass ownership** (`AccessPolicies.RequiresOwnershipScope`). A Verifier must open the
  journey they are verifying; their narrow permission set is the constraint, not ownership.

---

## 4. CQRS conventions

| Concern | Rule |
| --- | --- |
| Write | `ICommand` / `ICommand<T>` + `ICommandHandler<…>` (in `Abstractions/Messaging`). |
| Read | `IQuery<T>` + `IQueryHandler<…>`. Queries **never** mutate; prefer `AsNoTracking()` / projections. |
| Dispatch | Inject `ISender` (MediatR) into endpoints; never resolve handlers directly. |
| Sync vs async | **Default is synchronous** request/response: endpoint → `sender.Send` → handler → `SaveChanges` → HTTP result. **Offload only** work that must ack fast or cross a process boundary (webhooks, integration): the handler publishes an integration event through the outbox and a worker does the work (§6a). Reads and result-returning writes stay synchronous — don't push them through the bus. |
| Validation | `FluentValidation` validator per command, run by a `ValidationBehavior` pipeline (fail fast, before the handler). |
| Cross-cutting | Add behaviours (logging, validation, transaction/outbox) in `AddApplication`; don't scatter them into handlers. |
| Naming | `VerbNounCommand` / `GetNounQuery`; handler = `VerbNounHandler`; response = `VerbNounResponse`. |

Handlers and behaviours are auto-registered — `AddApplication` calls
`RegisterServicesFromAssembly(...)`. Just add the slice; don't touch DI wiring.

---

## 5. Domain layer

- **Rich entities & aggregates.** Business rules, state transitions and guards live here (e.g.
  `TrustJourney.Start(...)`, `journey.RecordPayment(...)`), returning failures via exceptions or a
  `Result` type — never leave an aggregate in an invalid state.
- **Value objects** for typed concepts (money, email, identifiers). Prefer strong IDs over raw `Guid`
  where it removes mix-ups.
- **Domain events** raised inside aggregates, dispatched on `SaveChanges` (then to the bus via the
  outbox). Domain events stay in `Domain`; **integration events** (cross-service) go in `Contracts`.
- Domain has **zero** framework dependencies — no EF, no MediatR attributes, no ASP.NET.

---

## 6. Infrastructure & persistence

### Database standards

PostgreSQL 17 + EF Core 10 (Npgsql), single `kadari` schema. Full detail: `docs/planning/db-standards.md`.

- **Naming:** PascalCase tables + columns (EF default), **plural** table names, no prefixes.
- **Primary keys:** application-generated **UUIDv7** (`Guid.CreateVersion7()`) for **transactional/data
  tables** (they grow into large row counts). **Reference / lookup tables use a `smallint` PK** —
  small, bounded, seeded sets, referenced widely as compact FKs.
- **Entity keys are strongly-typed** (`readonly record struct CustomerId(Guid Value)`) mapped via EF
  value converters; entities derive from `Entity<TId>` / `AggregateRoot<TId>`.
- **Timestamps:** `timestamptz`, UTC only (`DateTimeOffset` in the domain).
- **Concurrency:** `xmin` (`UseXminAsConcurrencyToken`) — no rowversion column.
- **Enums:** stored as `text` (`HasConversion<string>()`). **JSON:** `jsonb`. **Money:** `bigint`
  minor units + `char(3)` ISO-4217 currency, as an owned value object.
- **Soft-delete:** `IsDeleted`/`DeletedAt` + global query filter; `Remove()` → soft-delete.
  **No hard deletes.** Settlement/audit rows are append-only.

- EF Core config per entity via `IEntityTypeConfiguration<T>` — auto-discovered by `KADARIDbContext`
  (`ApplyConfigurationsFromAssembly`). Schema is `kadari`. Never configure entities inline in
  `OnModelCreating`.
- The Application layer depends on an **`IKADARIDbContext` abstraction** (expose `DbSet`s +
  `SaveChangesAsync`), implemented by `KADARIDbContext` in Infrastructure — keeps EF out of
  Application's face and keeps handlers testable.
- Base-entity conventions (UUID PK, `CreatedAt/UpdatedAt`, soft-delete, row version, audit) applied
  centrally via a base type + global query filter. **No hard deletes.**
- Blob storage only through `IBlobStorage` — never touch `IAmazonS3` outside `Infrastructure/Storage`.
- Bus: publish integration events from `Contracts`; MassTransit consumers live in `Infrastructure`
  and are auto-registered (`AddConsumers`). Kebab-case endpoint names. **See §6a for the full flow.**
- Migrations:
  ```bash
  dotnet ef migrations add <Name> --project src/KADARI.Infrastructure --startup-project src/KADARI.WebApi
  dotnet ef database update      --project src/KADARI.Infrastructure --startup-project src/KADARI.WebApi
  ```

---

## 6a. Async messaging — the outbox → worker flow

Two message mechanisms exist; **do not confuse them**:

- **Domain events** (`KADARI.Domain`, e.g. `PaymentSettledDomainEvent`) are **in-process only**. Raised inside an
  aggregate, dispatched by `TransactionalDispatchBehavior` **inside the command's transaction**, handled by
  `INotificationHandler`s in the **same request, same process**. Use for intra-boundary reactions
  (payment → workflow → tasks → comms). This is not the bus.
- **Integration events** (`KADARI.Contracts`, e.g. `WhatsAppMessageReceivedIntegrationEvent`) **cross the process
  boundary** via the transactional outbox → RabbitMQ → worker. Use for provider webhooks, work that
  must ack fast, or anything that should scale/fail independently of the API.

**The guaranteed async flow (the standard for webhooks / offloaded work):**

1. **Thin endpoint.** The endpoint binds the request and sends a *command* — no business logic
   (e.g. `POST /webhooks/whatsapp` → `IngestWhatsAppMessageCommand`).
2. **Handler publishes, doesn't process.** The ingest handler persists whatever state belongs in that
   transaction (often none) and publishes an integration event through
   **`IIntegrationEventPublisher`** (never inject MassTransit's `IPublishEndpoint` into Application).
   Give it a **deterministic message id** (e.g. from a provider id) so retries de-dupe.
3. **Outbox = atomic, no dual write.** The publish is *buffered* into the `OutboxMessage` table **in the
   same transaction as `SaveChanges`**. Nothing is sent to RabbitMQ inside the transaction — the row and
   any state commit together or not at all. After commit, MassTransit's delivery service ships it to the
   broker (retrying until it succeeds). The endpoint acks fast.
4. **Worker drains the queue.** A **separate `KADARI.Worker` process** owns consumption. Its
   `IConsumer<T>` (in `Infrastructure/Messaging`, a thin adapter) dispatches an Application command
   (e.g. `ProcessWhatsAppMessageCommand`) that does the real work. The **inbox** de-dupes on the message
   id, so a provider retry is processed exactly once; the worker's own `SaveChanges` commits the result
   atomically.

**Process topology (enforced by DI):** `AddInfrastructure()` registers the bus **publish-only** (WebApi
— never drains the queue). `AddInfrastructure(withConsumers: true)` registers consumers + inbox + receive
endpoints (the **worker** — the only process that drains queues). Reference: the WhatsApp inbound slice
(`Features/WhatsApp/IngestWhatsAppMessage` → outbox → `Features/WhatsApp/ProcessWhatsAppMessage`).

### Transactional commands & the domain→integration bridge

Every command runs in **one transaction** (`TransactionalDispatchBehavior` + `IUnitOfWork`), and its domain
events are dispatched **inside** that transaction. So the command's writes, the whole synchronous reaction
cascade, and any integration events published to the outbox commit **atomically — both or neither**.
Queries never open a transaction.

- **Raise integration events from a decoupled translator, not the command handler.** A state change raises a
  *domain* event; a dedicated `INotificationHandler<DomainEventNotification<T>>` (the "translator", e.g.
  `OnUserRegisteredPublishIntegrationEvent`) maps it to a `Contracts` integration event via
  `IIntegrationEventPublisher`. Because it runs inside the transaction, the `OutboxMessage` row commits with
  the state. The aggregate and command handler stay ignorant of `Contracts`/the bus.
- **Rollback is all-or-nothing across the cascade.** A downstream reaction that throws rolls back the
  primary command too. To *persist a side-effect while still rejecting* (e.g. a failed OTP attempt bumping
  the brute-force counter), throw an exception marked `IPersistableRejection` — the UoW commits, then
  rethrows.
- **⚠️ Retry re-runs the whole command delegate.** Postgres has the Npgsql **retrying execution strategy**
  on by default, so on a transient DB error the UoW re-executes the command + its cascade in a fresh
  transaction. **DB state is safe** (each failed attempt rolls back), **but external I/O inside a handler
  (payment / notification / WhatsApp sends) can re-fire.** Don't do external side-effects inline in a
  command handler — **move them to post-commit integration events** (publish through the outbox, let a
  consumer do the send). This is the same outbox→worker pattern above and is the intended fix; until a
  given send is migrated, treat it as a known at-least-once hazard.

---

## 7. Testing (xUnit)

- **Every slice = at least one test.** Domain invariants → `KADARI.Domain.Tests`; handler behaviour
  → `KADARI.Application.Tests`. Mirror the `Features/…` folder layout in the test project.
- **Naming:** `Method_Scenario_ExpectedResult` (e.g. `Start_WhenProductInactive_Throws`).
- **Structure:** Arrange / Act / Assert with blank-line separation. One logical assertion per test.
- **Domain tests are pure** — no mocks, no DB; construct aggregates and assert on state/events.
- **Handler tests** use an in-memory/fake `IKADARIDbContext` (or SQLite in-memory for query shape).
  Mock only owned abstractions (`IKADARIDbContext`, `IBlobStorage`, bus, `TimeProvider`) — never
  mock types you don't own.
- Determinism: inject `TimeProvider`; no `DateTime.Now`, no `Guid.NewGuid()` scattered in logic.
- Keep an **architecture test** enforcing the dependency rules (see `DependencyRuleTests`).

```csharp
public class StartJourneyTests
{
    [Fact]
    public void Start_WhenProductActive_RaisesJourneyStartedEvent()
    {
        var product = ProductFactory.Active();

        var journey = TrustJourney.Start(customerId: Guid.NewGuid(), product, DateTimeOffset.UnixEpoch);

        Assert.Contains(journey.DomainEvents, e => e is JourneyStartedEvent);
    }
}
```

---

## 8. SOLID in practice (how we read it here)

- **S** — a slice/handler has one reason to change; a class does one job. Split when a handler grows a "and".
- **O** — extend by adding a slice / behaviour / consumer, not by editing a switch. `Program.cs` gains
  one `MapXEndpoints()` line per aggregate, nothing more.
- **L** — implementations honour their abstraction's contract (e.g. any `IBlobStorage`).
- **I** — small, purpose-specific interfaces (`ICommandHandler<T>`, `IBlobStorage`) over god-services.
- **D** — depend on abstractions defined by the inner layers; Infrastructure supplies the implementations.

---

## 9. Conventions & housekeeping

- C#: file-scoped namespaces, `sealed` by default, `internal` unless a type must cross a project
  boundary, `record` for DTOs/messages, primary constructors for DI, nullable reference types on.
- No magic strings for config — bind to `IOptions<T>` (see `S3Options`).
- Errors: throw typed exceptions (`NotFoundException`, `ValidationException`) mapped to Problem
  Details at the API edge — endpoints don't build error bodies by hand.
- Client (`KADARI.Client`): TanStack Query for server state, Zustand for UI state, components under
  `src/components`, routes under `src/routes`. Run `pnpm lint` + `pnpm format` before pushing (husky
  enforces on commit).
- **Definition of done:** builds clean · slice has tests · `dotnet test` green · migration added if the
  schema changed · endpoint documented (Scalar) · no `Infrastructure` leak into `Application`/`Domain`.

## 10. Common commands

```bash
dotnet run --project src/KADARI.AppHost     # full stack via Aspire (dashboard URL printed)
dotnet build KADARI.sln
dotnet test  KADARI.sln
```
