# KADARI Care: code-level reconciliation (STEP 1)

**Status:** Draft for review. No code has changed.
**Date:** 2026-10-24
**Answers:** `docs/authority/CARE/18_KADARI_CARE_CODEBASE_RECONCILIATION_STATUS.md` §5, and the "Care Build
Reconciliation Pack" asked for in `CARE/17` §10.
**Method:** I read every Care authority (01–19), the Ripple reference
(`docs/architecture/RIPPLE-ARCHITECTURE-FOR-KADARI.md`), `CLAUDE.md`, the four design boards, and the code under
`src/` and `tests/`. Every class, table, endpoint and screen named below exists in this repository at commit
`a162630`. Anything that does not exist is marked **absent**.

Labels: **REUSE** means use it as it is. **EXTEND** means add to an existing primitive. **NEW** means nothing
equivalent exists.

---

## 0. Summary

- **There is exactly one asset identity, and Care can attach to it.** `Asset` (`src/Kadari.Domain/Assets/Asset.cs`)
  is the Digital Twin aggregate: a strongly typed `AssetId` (UUIDv7), tenant-scoped, with primary owner and
  household co-owners. Care records reference `AssetId`. No second asset table is needed.
- **Evidence is reusable, but too thin for Care in two places.** `AssetEvidence` is an append-only, provenance-carrying
  child of `Asset`, and receipts are stored envelope-encrypted through `IBlobStorage`. Care needs two things it lacks:
  - **structured purchase facts**: purchase date, retailer, brand, model, and the per-field confidence and
    confirmation status of each;
  - **document kinds beyond "ownership"**: warranty PDF, job card, confirmation screenshot.

  The receipt upload has no purchase date or retailer field. The vision model's `SerialHint` is parsed and then
  dropped before it is saved.
- **There is no asset history or timeline today.** Domain events (`AssetRegisteredDomainEvent`,
  `AssetTransferredDomainEvent`, …) are dispatched in the transaction but never stored. `AuditEvent` exists but only
  identity and erasure handlers write it. CARE/12 §11 and CARE/15 G0 require material outcomes to "write back to
  asset history", so that history has to be built first (§4.1).
- **Background infrastructure is minimal by design, and the brief conflicts with `CLAUDE.md` here.**
  `CLAUDE.md` §6a says *"KADARI has no RabbitMQ, outbox or worker … If a real cross-process need appears, add the
  outbox then; don't pre-build it."* The STEP 2b brief asks for RabbitMQ, a MassTransit outbox and a Worker. Care's
  registrations and reminders are the "real need" §6a anticipates, but changing the contract is a decision to make,
  not assume (Q1).
- **The research registry is not yet a rules source.** `CARE/REGISTRY/*.csv` contains brands, retailers, ten routing
  archetypes (R1–R10) and eight warranty-automation leads, all as prose. It has **no warranty durations, no start
  basis and no registration deadlines** (for example, Samsung's entry reads "exact extension rules model-specific").
  An import can seed the reference entities, the routing archetypes and draft rules, but on its own data alone every
  coverage evaluation would honestly return `UNKNOWN`. Published coverage rules for the P0 brands need researched
  values with sources (Q4).
- **Recommended first slice:** the CARE/18 default, cut at the first point that proves the architecture. That is:
  existing asset → structured purchase facts → CareProfile → coverage evaluated from one published, versioned rule →
  Care card and coverage detail → history entry. Details in §7.

---

## 1. Existing architecture inventory

| Area | What exists | Where |
|---|---|---|
| Front end | Two React 19 + Vite + TanStack Router SPAs. **Client** (port 8200) routes: `index`, `sign-in`, `$customerId/ledger`, `$customerId/capture`, `$customerId/assets/$assetId`, `verify/$verificationId`. **Portal** (port 8300) routes: `login`, `dashboard/index`, `dashboard/portfolio`, `dashboard/users`. TanStack Query for server state, Zustand for UI state, typed API generated from `openapi/v1.json` (`pnpm api:types`, checked by `pnpm api:check`). | `src/Kadari.Client/src/routes/**`, `src/Kadari.Portal/src/routes/**`, `src/Kadari.Client/src/lib/api/schema.d.ts` |
| Back end | .NET 10 minimal APIs, MediatR CQRS. Pipeline: `ValidationBehavior` → `ResourceAuthorizationBehavior` → `TransactionalDispatchBehavior`. Endpoints grouped under `/api/v1/kadari/customers/{customerId}`. | `src/Kadari.WebApi/Endpoints/KadariEndpoints.cs`, `src/Kadari.Application/Behaviors/*` |
| Slice layout | Most features are **flat** (`Features/GetAsset`, `Features/RecordOwnershipProof`, …) rather than `Features/{Aggregate}/{UseCase}`. Only `Customers/`, `Identity/` and `Development/` follow the aggregate layout. | `src/Kadari.Application/Features/` |
| Persistence | PostgreSQL + EF Core (Npgsql, with the retrying execution strategy). Schema `kadari`. One migration (`20261014071439_Initial`). Soft-delete filter on every `Entity` (`ApplySoftDeleteQueryFilters`), `EntityConventionsInterceptor` turns `Remove()` into soft-delete, `xmin` concurrency. | `src/Kadari.Infrastructure/Persistence/**` |
| Migration runner | `Kadari.Migration` exists (migrate and seed, then exit) but the **AppHost does not run it**: the WebApi migrates on start in Development. | `src/Kadari.Migration/Program.cs`, `src/Kadari.AppHost/AppHost.cs` |
| Auth | Own accounts: OTP (SMS/WhatsApp/email) and password sign-in, JWT plus a refresh cookie, sessions. Roles: `PublicVisitor`, `Customer`, `Valuer`, `CustomerSuccess`, `Insurer`, `SystemAdministrator`. Permissions `{Resource}{Action}`, gated with `.RequirePermission(...)` at the endpoint. Ownership is enforced by `IResourceScopedRequest` + `IResourceOwnerResolver<T>`. Staff bypass ownership through `AccessPolicies.RequiresOwnershipScope`. | `src/Kadari.Domain/Access/*`, `src/Kadari.WebApi/Access/*`, `tests/Kadari.ArchitectureTests/ResourceAuthorizationRuleTests.cs` |
| Tenancy | `Tenant` with `TenantId.Default` for consumer-direct customers. `ITenantScoped` on `Asset`. Explicit `ForTenant`/`TenantOfCustomerAsync` narrowing (not a global filter). Insurer tenants have their own branding. | `src/Kadari.Domain/Assets/Tenant.cs`, `src/Kadari.Application/Features/Tenancy/TenantScopeExtensions.cs` |
| Asset / Digital Twin | `Asset` aggregate: `DisplayName`, `AssetCategory` (Electronics, Furniture, Clothing, Jewellery, ArtAndCollectibles, Other), `RoomLabel`, `AssetLifecycleStatus` (Active, ReportedStolen), owner and co-owners, transfer, inherit, evidence. Assurance and best value are **derived** from evidence, not stored. | `src/Kadari.Domain/Assets/Asset.cs`, `AssetAssurance.cs` |
| Evidence and documents | `AssetEvidence` (kinds: Image, Identification, Ownership, Value; strength: SelfAsserted or Independent; `Source`, `Confidence`, `Reference` = blob key or value; two-tier retention). Receipts are uploaded through `RecordOwnershipProofCommand` to `IBlobStorage` → `EnvelopeEncryptedBlobStorage` → `S3BlobStorage` (MinIO). Signed media links come from `ReportMediaLinkFactory`. | `src/Kadari.Domain/Assets/AssetEvidence.cs`, `src/Kadari.Application/Features/RecordOwnershipProof/*`, `src/Kadari.Infrastructure/Storage/*` |
| Events and audit | Domain events are raised on aggregates and dispatched **inside** the transaction (`DomainEventDispatcher`, `DomainEventNotification<T>`), with reactions such as `OnUserRegisteredCreateCustomer`. `AuditEvent` is append-only, written by `AssignRole`, `RemoveRole`, `SetUserStatus` and `EraseCustomer` only. **No persisted asset history.** | `src/Kadari.Infrastructure/Messaging/DomainEventDispatcher.cs`, `src/Kadari.Domain/Audit/AuditEvent.cs` |
| Integration events | `IIntegrationEventPublisher` is declared (its doc comment already describes a MassTransit outbox), but only `NoOpIntegrationEventPublisher` is registered. There is no `Kadari.Contracts` project. | `src/Kadari.Application/Abstractions/Messaging/IIntegrationEventPublisher.cs`, `src/Kadari.Application/Common/Defaults/NoOpIntegrationEventPublisher.cs` |
| After-commit side effects | `IPostCommitActions` runs work once after commit, in-process and not durable. `SnapCaptureWork` is a once-only guard for the vision call. | `src/Kadari.Application/Abstractions/Persistence/IPostCommitActions.cs` |
| AI | `IVisionEnricher` with Gemini, OpenAI-compatible and pass-through implementations, a cross-border consent gate (`KadariConsentScope.CrossBorderAiEnrichment`), and a daily **call count** per customer (`VisionUsage`, `VisionQuota`). **No token or cost recording**, and no general model abstraction beyond vision. | `src/Kadari.Application/Features/AnalyzeSnap/*`, `src/Kadari.Infrastructure/Enrichment/*`, `src/Kadari.Domain/Assets/VisionUsage.cs` |
| Integrations / connectors | Twilio (SMS/WhatsApp) and SMTP behind `INotificationChannel`, plus a Mailpit dev mailbox. No outbound HTTP connector framework and no SSRF guard. | `src/Kadari.Infrastructure/Notifications/*` |
| Notifications / scheduling | `INotificationChannel.SendAsync(channel, recipient, body)` is the single delivery seam. No reminder model, no notification inbox, no user notification preferences. The only scheduled work is `AssetEvidenceRetentionService`, a hosted service inside the API. | `src/Kadari.Application/Abstractions/Notifications/INotificationChannel.cs`, `src/Kadari.Infrastructure/Tasks/AssetEvidenceRetentionService.cs` |
| Background jobs / workers | **Absent.** No RabbitMQ, no Worker project. `CLAUDE.md` §6a says this is deliberate. | `src/Kadari.AppHost/AppHost.cs` |
| Idempotency | `IdempotencyKeyFilter` checks that the `Idempotency-Key` header is present. **It stores nothing**, so replays are not short-circuited (its own comment says so). `DeterministicGuid` exists for repeatable ids. | `src/Kadari.WebApi/Http/IdempotencyKeyFilter.cs`, `src/Kadari.Application/Common/DeterministicGuid.cs` |
| Feature switches | `FeatureOptions` (`AssetOwnershipChanges`, `InsurerConsole`) with `.RequireFeature(...)`. | `src/Kadari.Application/Abstractions/Configuration/FeatureOptions.cs` |
| Deployment | Aspire AppHost (Postgres, MinIO, Mailpit, WebApi, Client, Portal) for local runs. `deploy/docker-compose.prod.yml` with nginx, Postgres and MinIO for production. GitLab CI (`.gitlab-ci.yml`: unit tests, integration tests, API contract, migration drift, front end). No GitHub Actions yet. | `deploy/*`, `.gitlab-ci.yml` |
| Tests | Domain (`tests/Kadari.Domain.Tests/Assets`), Application (`Features`), Architecture (`DependencyRuleTests`, `ResourceAuthorizationRuleTests`), Infrastructure with Testcontainers (persistence, storage, enrichment, identity). No API-level test host. | `tests/**` |

---

## 2. REUSE / EXTEND / NEW matrix

These are the Care capabilities listed in CARE/18 §5.

| # | Care capability | Verdict | Existing code it builds on | What is missing |
|---|---|---|---|---|
| 1 | **Digital Twin Care profile** | **EXTEND** `Asset` by reference, **NEW** `CareProfile` | `AssetId`, `ITenantScoped`, `Asset.IsOwnedBy`, the owner-resolver pattern (`GetAssetOwnerResolver`) | A `CareProfile` aggregate keyed one-to-one by `AssetId` (unique), with status, eligibility, coverage state, next attention date, open-case count and last evaluated time. **Eligibility cannot be derived from `AssetCategory` today:** a washing machine, kettle and laptop would all be `Electronics` or `Other`. It needs a Care category (registry `ProductFamily.categoryCode`) resolved per asset (Q3). |
| 2 | **Receipt / label extraction** | **EXTEND** | `IVisionEnricher` (`GeminiVisionEnricher`, `OpenAiCompatibleVisionEnricher`), `AssetEnrichmentReplyMapper` (which already treats model output as untrusted), `IdentifiedItem.SerialHint`, the cross-border consent gate, `VisionQuota` | A receipt/label extraction contract (retailer, purchase date, price, brand, model, serial, each with a confidence) that returns a **proposal**. A confirm step that saves confirmed facts as new evidence, never overwriting. Storage of `SerialHint`, which today is parsed and dropped (`CapturedAssetFactory.Create` ignores it). A model-call abstraction that records tokens and cost per call: `VisionUsage` only counts calls (Q6). |
| 3 | **Coverage Engine** | **NEW** | `TimeProvider`, `Money`, the derive-don't-store pattern (`AssetAssurance.For`) | Pure domain evaluator: specificity order model → family → brand/category → brand → retailer/category → unknown (CARE/03 §2B), start/end date maths, a `CoverageRecord` (several per asset), and a `CoverageDecision` that stores an input snapshot, the matched rule version ids and the engine version. |
| 4 | **Registration Engine** | **NEW** (plus EXTEND of consent) | `KadariConsent` (per-customer scope, grant/withdraw), `ConsentRequiredException` | `CoverageRegistration` state machine (CARE/07 §6), a per-action `ExternalActionAuthorisation` (CARE/12 §8; `KadariConsent` is per customer and has no terms version, so it cannot serve here as it is), an idempotency key unique in the database, and confirmation evidence. **Automatic** mode needs outbound execution away from the request transaction (Q1). Assisted and Guided modes can run synchronously as user-driven steps. |
| 5 | **Reminder Engine** | **NEW** (REUSE of delivery) | `INotificationChannel` (Twilio, SMTP, Mailpit), the hosted-service sweep pattern (`AssetEvidenceRetentionService`) | `CareReminder` with an idempotent creation key, a scheduler sweep, a delivery state, notification preferences/opt-in (there are none today), and deep links. |
| 6 | **"Something's wrong" intake** | **NEW** (REUSE of capture) | `IBlobStorage` + `FileSignature`/`ImageMetadata` (metadata stripping), the camera component `capture-camera.tsx`, the evidence model | Problem intake (draft, safety, evidence-check states, CARE/07 §7), a safety classifier with verified wording, and attaching issue media as `AssetEvidence`. This needs a new evidence kind or a case attachment that references evidence (§3.2). |
| 7 | **Routing Engine** | **NEW** | none | `RoutingRuleVersion`, a `RoutingDecision` with an input snapshot, GREEN/AMBER/RED composition (CARE/07 §9, §18), and a `RESEARCH_REQUIRED` fallback. The R1–R10 archetypes in `03_ROUTING_MODELS.csv` map onto CARE/12's `RouteType`. |
| 8 | **Asset Case Engine** | **NEW** | the aggregate and domain-event pattern, soft-delete, `xmin` | `AssetCase` aggregate with an explicit status machine (CARE/07 §10), plus child `CaseEvent` (append-only), `CaseMessage`, `CaseAttachment`, `CaseCommitment`, `CaseAppointment`, `CaseReference`, `RepairOutcome` and `ResolutionOutcome`. Nothing case-like exists today. Insurance claims are not modelled. |
| 9 | **Communications / proof packs** | **EXTEND** | `ReportMediaLinkFactory` (signed, expiring media links), `HmacReportIntegritySigner`, the passport/export pattern (`ExportAssetForSaleQuery`, `AssetForSalePackage`, which assembles evidence for an external party), the WhatsApp icon component | A proof-pack read model with least-data selection and a disclosure summary, "mark sent" with idempotency, and an `ExternalActionAudit` row per handoff. |
| 10 | **Commitments and follow-up** | **NEW** | hosted-service sweep pattern | `CaseCommitment` state (OPEN → DUE_SOON → OVERDUE → FULFILLED/SUPERSEDED), an overdue sweep, a prepared follow-up, and a guard against follow-up loops. |
| 11 | **Resolution write-back** | **NEW** (asset history) plus **EXTEND** `Asset` | `Asset` domain events, `Asset.Register` (for a replacement asset) | An **asset history** read model (§4.1) that Care outcomes, and today's transfer, inherit, stolen and recovered events, write into. Replacement needs an asset-to-asset relationship (old → replacement), which is absent. |
| 12 | **Registry Factory** | **NEW** | `Kadari.Migration` (seeding), the `DefaultTenantSeeder` pattern, smallint lookup-table convention (CLAUDE.md §6) | Registry reference entities, immutable rule versions with draft → publish → supersede, `SourceEvidence`, a preview-then-apply CSV import, and freshness scheduling. |
| 13 | **Care Ops** | **NEW** (REUSE of Portal shell and roles) | Portal SPA (`dashboard/route.tsx`, `dashboard-sidebar.tsx`), `Role.CustomerSuccess`, `RequirePermission`, `AdminUserEndpoints` as the staff-endpoint pattern | `OpsWorkItem`, `RegistryResearchJob`, Care permissions (`CareView`, `CareOpsReview`, `RegistryPublish`, …), and queue screens. Which role holds them is Q7. |
| 14 | **Audit / authorisation** | **EXTEND** | `AuditEvent` (append-only, actor and state), `Permission`/`RolePermissions`, `IResourceScopedRequest` | Care permissions; an `ExternalActionAudit` (destination, request hash, result, external reference); `AuditEvent` written for every material Care transition. The SQL hardening noted in `AuditEvent`'s comment (REVOKE UPDATE/DELETE) is still a later slice. |
| 15 | **Analytics / telemetry** | **EXTEND** | OpenTelemetry through `Kadari.ServiceDefaults`; client `src/lib/analytics.ts`; `SnapMetrics` (a `Meter` example) | Care meters and funnel events (CARE/05 §7), kept free of document or message content. Commercial metrics derive from Care events and tables. |

### 2.1 Platform primitives Care must reuse, never duplicate

| Primitive | Use as is | Never create |
|---|---|---|
| `AssetId`, `Asset` | Every Care row references `AssetId`. Ownership checks go through `Asset.IsOwnedBy`. | A Care asset id or a Care product table holding customer items |
| `Owner`/`Customer`/`UserAccount`, `Tenant` | Case `ownerId` is the asset's `OwnerId`; actors are `UserAccount` ids; tenant comes from `Asset.TenantId` | A Care user, household or tenant model |
| `AssetEvidence` + `IBlobStorage` | Receipts, warranty documents, issue photos, job cards and confirmations as evidence on the asset | A Care file store or a second encryption scheme |
| `INotificationChannel` | All reminder and handoff sends | A direct Twilio or SMTP call from a Care handler |
| `IVisionEnricher` / model seam | Extraction | A second, unmetered model client |
| `AuditEvent` | Material transitions | A parallel generic audit table (`ExternalActionAudit` is specific to external actions and complements it) |

---

## 3. Schema delta

### 3.1 Existing tables reused unchanged

`Assets`, `AssetCoOwners`, `Owners`, `Customers`, `UserAccounts`, `Sessions`, `Tenants`, `TenantMemberships`,
`KadariConsents`, `VisionUsages`, `AssetPassports`, `InsurerUsers`, `AuditEvents`.

### 3.2 Existing tables extended

| Table | Change | Why |
|---|---|---|
| `AssetEvidences` | Add kinds (e.g. `Purchase`, `Warranty`, `Document`) **or** add a `DocumentType` column on `Ownership`/`Image` evidence; add optional structured fact columns or a `jsonb` `Facts` payload (retailer, purchase date, brand, model, serial) plus `ConfirmedAt`/`ConfirmedBy`. | Coverage inputs must be structured, sourced and confirmed (CARE/14 C3). User corrections become **new** evidence rows, never edits, which keeps the append-only rule. |
| `AssetEvidences` (assurance) | `AssetAssurance.For` must ignore the new kinds, or deliberately count `Purchase` as Ownership. | So Care evidence cannot silently change Σ(V×C). Needs a test. |
| `Assets` | Optional `ReplacedByAssetId`/`ReplacesAssetId`, or a separate `AssetRelations` table. | Replacement outcome (CARE/05 C25). |

### 3.3 New tables (Care, transactional, UUIDv7 keys)

`CareProfiles` (unique `AssetId`) · `CoverageRecords` · `CoverageDecisions` · `RegistrationOpportunities` ·
`CoverageRegistrations` (**unique `IdempotencyKey`**) · `ExternalActionAuthorisations` · `ExternalActionAudits`
(append-only) · `CareReminders` (**unique `(AssetId, ReminderType, SourceId, DueAt)` or a deterministic key**) ·
`AssetCases` · `CaseEvents` (append-only) · `CaseMessages` (**unique `IdempotencyKey`** for mark-sent) ·
`CaseAttachments` (FK to `AssetEvidences`) · `CaseReferences` (append-only, **unique `(CaseId, IdempotencyKey)`**) ·
`CaseCommitments` · `CaseAppointments` · `RepairOutcomes` · `ResolutionOutcomes` · `RoutingDecisions` ·
`OpsWorkItems` · `RegistryResearchJobs` · **`AssetHistoryEntries`** (append-only, see §4.1) · `IdempotencyRecords`
(`Key`, `Scope`, `RequestHash`, `ResponseJson`; to make `IdempotencyKeyFilter` real).

### 3.4 New tables (registry, reference data)

`BrandGroups`, `Brands`, `CountryOperators`, `Retailers`, `WarrantyAdministrators`, `ServiceNetworks`,
`ServiceCentres`, `ProductFamilies`, `ProductModels`, `ContactChannels`, `SourceEvidence`,
`CoverageRuleVersions`, `RegistrationRuleVersions`, `RoutingRuleVersions`, `RegistryImports`
(the stored preview plan and its applied state).

- **Keys:** CLAUDE.md §6 says lookup tables use `smallint` keys. Brands and retailers number in the hundreds now but
  are meant to grow (multi-country), and rule versions are append-only. I recommend UUIDv7 for rule versions and
  `SourceEvidence`, and either type for the reference entities (Q8).
- **Rule rows are immutable once `PUBLISHED`:** enforced in the domain (no setters after publish) and by
  `(LogicalRuleId, Version)` unique. Status: DRAFT/PUBLISHED/STALE/SUPERSEDED/RETIRED (CARE/12 §10). CARE/07 §17 uses
  a slightly different list (ACTIVE, REVIEW_DUE); I propose CARE/12's list plus a computed `ReviewDue` flag from
  `LastVerifiedAt`.
- **Registry rows are not tenant-scoped.** They are platform knowledge with no customer data (CARE/12 §14).

### 3.5 Migrations, indexes and constraints

- One migration per epic (`AddCareFoundation`, `AddCareRegistry`, …), each with drift checked in CI. The GitLab
  `migration drift` job already does this.
- Indexes: `CareProfiles(AssetId)` unique; `CoverageRecords(AssetId)`; `AssetCases(AssetId, Status)`;
  `CareReminders(Status, DueAt)` for the sweep; `CaseCommitments(Status, DueAt)`; and rule lookups on
  `(CountryCode, BrandId, ProductFamilyId, ProductModelId, RetailerId, Status)`.
- All Care foreign keys are `Restrict`. Soft-delete applies to the mutable Care aggregates. Event, audit, history and
  reference rows are append-only and never soft-deleted.

### 3.6 Versioning and audit impact

- Every `CoverageDecision`, `RoutingDecision` and `CoverageRegistration` stores the exact rule-version id. A later
  publish creates a new version row, so earlier decisions keep pointing at the old one (CARE/15 G0).
- `AuditEvent` gains Care actions. `ExternalActionAudit` records destination, request hash, result and external
  reference.
- POPIA erasure (`EraseCustomerCommand`, `KadariErasure`) and data export (`KadariDataExport`,
  `ExportCustomerDataQuery`) **must be extended** to cover Care tables. A customer's cases, messages and references
  are personal data. This is easy to miss and is a G2 privacy item.

---

## 4. API delta

Existing conventions win over CARE/13's illustrative paths (CARE/13 §24). Customer routes nest under the existing
group `/api/v1/kadari/customers/{customerId}` so `ResourceAuthorizationBehavior` and an owner resolver per slice
keep working unchanged.

### 4.1 Asset history (prerequisite, NEW)

| Endpoint | Purpose |
|---|---|
| `GET /api/v1/kadari/customers/{customerId}/assets/{assetId}/history` | Material events on the asset: registered, transferred, inherited, reported stolen, recovered, plus Care events (coverage activated, case resolved, repair completed, replaced). |

Writing it: reactions (`Features/Assets/Reactions/On{Event}AppendHistory`) on the existing asset domain events and the
new Care events append `AssetHistoryEntries` in the same transaction. That keeps "write back to asset history" atomic
with the change that caused it.

### 4.2 Existing endpoints reused

| Endpoint | Use in Care |
|---|---|
| `GET …/assets/{assetId}` (`GetAsset`) | Asset header on Care screens. Unchanged. |
| `POST …/assets/{assetId}/proof` (`RecordOwnershipProof`) | Receipt and serial capture. **Extended** with optional `purchaseDate`, `retailer`, `brand`, `model`, or superseded by a confirm-facts endpoint (below). |
| `POST …/snaps` (`AnalyzeSnap`) | Product capture. **Extended** to keep `SerialHint` as self-asserted evidence. |
| `GET …/ledger` (`GetLedger`) | Ledger rows could carry a Care badge or "needs attention" count later. Optional. |
| `GET/POST …/consents` | Unchanged. Care's per-action authorisation is separate (row 4 of §2). |

### 4.3 New Care endpoints, by epic

Customer-scoped endpoints sit under `/api/v1/kadari/customers/{customerId}`:

| Epic | Method and path | Request type (scope) |
|---|---|---|
| C1 | `GET /assets/{assetId}/care` | `GetCareSummaryQuery` (scoped) |
| C1 | `POST /assets/{assetId}/care` (enable) | `EnableCareCommand` (scoped) |
| C3 | `POST /assets/{assetId}/care/extractions` (proposal), `POST /assets/{assetId}/care/facts` (confirm) | scoped |
| C4 | `POST /assets/{assetId}/care/evaluate`, `GET /assets/{assetId}/care/coverage`, `GET /assets/{assetId}/care/coverage/{coverageRecordId}` | scoped |
| C5 | `GET /assets/{assetId}/care/registration-opportunities`, `POST …/registrations/{opportunityId}/authorise`, `POST /care/registrations/{id}/execute`, `POST /care/registrations/{id}/user-step`, `GET /care/registrations/{id}` | scoped, with `Idempotency-Key` |
| C6 | `GET /care/reminders`, `POST /care/reminders/{id}/snooze` · `/dismiss` · `/complete` | scoped |
| C7–C8 | `POST /assets/{assetId}/care/issues`, `POST /care/cases/{caseId}/route`, `POST …/routing/{decisionId}/accept`, `POST …/routing/{decisionId}/request-review` | scoped, with `Idempotency-Key` |
| C9–C11 | `GET /care/cases`, `GET /care/cases/{caseId}`, `POST /care/cases/{caseId}/messages/prepare`, `POST …/messages/{id}/mark-sent`, `POST …/responses`, `POST …/external-reference`, commitments, appointments, `POST …/outcomes/{repair\|replacement\|credit\|refund\|rejection}`, `GET …/proof-pack` | scoped, with `Idempotency-Key` on writes |

Staff and registry endpoints sit under a new `/api/v1/care/ops/...` group: work items, research jobs, draft, publish,
mark-stale, retire, and registry reads. They are `IUnscopedRequest` (staff, permission-gated) per CLAUDE.md §3a.

**Errors.** CARE/13 §22 asks for domain error codes (`CARE_ROUTE_UNKNOWN`, `CARE_EXTERNAL_UNAVAILABLE`, …). Today
`GlobalExceptionHandler` maps typed exceptions to Problem Details. I propose a `CareDomainException(code)` mapped to
`422` (or `409` for a duplicate), with `code` in the Problem Details extensions. `ProblemResponsesTransformer`
documents them in OpenAPI.

### 4.4 Domain events

- **Existing, reused:** `AssetRegisteredDomainEvent`, `AssetEvidenceAttachedDomainEvent` (trigger to re-evaluate Care
  when a receipt or serial arrives), `AssetTransferredDomainEvent` (Care profile and open cases follow the asset; the
  new owner sees history but **not** the previous owner's case messages; Q9), `AssetInheritedDomainEvent`,
  `CustomerErasedDomainEvent` (erase Care personal data).
- **New:** the CARE/12 §11 list. These are in-process domain events in `Domain/Care/Events/`. Integration events (in
  a `Contracts` project) are needed only for work leaving the transaction: registration execution, reminder delivery
  and commitment-overdue sweeps (Q1).

---

## 5. Frontend delta: the 29 screens and states in CARE/05

Existing customer surfaces: **Ledger** (`routes/$customerId/ledger.tsx`); **Asset detail**
(`routes/$customerId/assets/$assetId.tsx`, with sections *Confidence*, *Add proof*, *Asset Passport* and
*Evidence (n)*); **Capture** (`routes/$customerId/capture.tsx` with `capture-camera.tsx` and
`capture-consent.tsx`); the **ownership proof form** (`components/kadari/ownership-proof-form.tsx`: receipt file,
serial, amount). Navigation (`kadari-shell.tsx`) has two items: *Asset Position* and *Capture*. **There is no history
surface, no notifications inbox and no activity or needs-attention surface.**

| # | Screen / state | Maps to | Verdict |
|---|---|---|---|
| C01 | Digital Twin Care card (9 states) | New card in `assets/$assetId.tsx`, above *Confidence* (board 2 places it under the asset header) | NEW component, EXTEND screen |
| C02 | Care overview | New route `assets/$assetId/care` (board 2 shows tabs Overview · Coverage · History · Docs) | NEW |
| C03 | Coverage detail ("How KADARI knows this") | Coverage tab | NEW |
| C04 | Registration opportunity (8 states) | Sheet from the Care card ("Activate cover") | NEW |
| C05 | Registration assisted step | Same flow | NEW |
| C06 | Registration success | Same flow | NEW |
| C07 | Care reminder detail (9 types) | Deep-link target route. **No in-app notification surface exists** | NEW |
| C08 | Pre-expiry health check | Route under Care | NEW |
| C09 | Something's wrong intake | Route under Care. **REUSE** `capture-camera.tsx` for photos; voice and audio are new | NEW, REUSE camera |
| C10 | Safety interruption | Step in intake | NEW |
| C11 | Evidence readiness | Step in intake. **REUSE** evidence list data from `GetAsset` | NEW, REUSE data |
| C12 | Resolving route (transient) | Step in intake | NEW |
| C13 | Recommended next action | Step in intake | NEW |
| C14 | Case pack preview | Case route. **REUSE** the passport card visual language (`asset-passport-card.tsx`) | NEW |
| C15 | Post-handoff confirmation | Case route | NEW |
| C16 | External reference capture (paste, enter, scan) | Case route; scan reuses camera and extraction | NEW |
| C17 | Open Care Case | New route `care/cases/$caseId` | NEW |
| C18 | Case timeline | Case route (board 4) | NEW |
| C19 | Record provider response | Case route | NEW |
| C20 | Appointment | Case route | NEW |
| C21 | Commitment | Case route | NEW |
| C22 | Follow-up | Case route | NEW |
| C23 | Resolution outcome select | Case route | NEW |
| C24 | Repaired outcome | Case route | NEW |
| C25 | Replacement / credit / refund (create replacement twin) | Case route; replacement reuses Capture | NEW, REUSE capture |
| C26 | Rejection outcome | Case route | NEW |
| C27 | Case closed | Case route | NEW |
| C28 | Care history | History tab. **Also the asset's first history surface** (§4.1) | NEW |
| C29 | Care value summary | Later V1/V1.1 | NEW (deferred) |
| — | Empty / partial / unknown / external-failure states (CARE/05 §3) | Inside C01–C03 | NEW |

**Navigation.** CARE/05 §4 says do not add a bottom-nav item unless the wider product's navigation needs it. The
boards show a bottom bar (*Assets · Support · Scan · Settings*) that does not exist in the app, whose nav is
*Asset Position · Capture*. I will not add nav items. Open cases surface on the asset and, later, in a global
needs-attention list (Q10).

**Portal (Care Ops).** New routes under `dashboard/` (`care-queue`, `registry`) reusing `dashboard-sidebar.tsx`. The
sidebar already has "Coming soon" entries.

**Design tokens.** `src/Kadari.Client/src/styles/kadari/kadari.tokens.css` already carries the KADARI palette. The
boards' green and the cards fit it. The boards show **£ prices and UK retailers** (Currys, Argos, John Lewis), but
the registry, `Money` default (`ZAR` in `RecordOwnershipProofHandler`) and POPIA posture are South African (Q5).

---

## 6. Gaps in the platform that Care depends on

These belong to STEP 2 or are prerequisites. They are not Care features.

1. **Persisted idempotency.** `IdempotencyKeyFilter` only checks the header is there. Care needs unique keys stored in
   the database for registrations, mark-sent, external references and reminders (CARE/12 §12). Plan: per-table unique
   keys (domain-level) plus a generic `IdempotencyRecords` replay store for HTTP retries.
2. **Asset history** (§4.1).
3. **Durable background work.** Reminders and commitment sweeps can run as hosted services today (the
   `AssetEvidenceRetentionService` pattern). **Automatic** registration (an outbound HTTP call to an OEM) must not run
   inside the request transaction, and `IPostCommitActions` is explicitly not durable. That is the trigger for the
   outbox and Worker (Q1).
4. **Model usage and cost recording** (Q6).
5. **Migration runner wiring.** The AppHost does not run `Kadari.Migration`. The registry import (STEP 4: "imported …
   by the migration runner") needs the runner in the AppHost and in the deploy pipeline.
6. **Care permissions** in `Permission`/`RolePermissions`.
7. **Feature switch** `FeatureOptions.Care` so production can ship dark.
8. **Slice layout.** New Care slices go under `Features/Care/{UseCase}/`, `Features/CareRegistry/…` and
   `Features/CareCases/…`. Existing flat features are left as they are; moving them is churn with no Care benefit.

---

## 7. Recommended first vertical slice

The smallest production-real slice that proves attachment to the asset, registry matching, rule versioning,
authorisation, evidence and history before cases exist:

1. **Asset history** (prerequisite): `AssetHistoryEntries` plus reactions on existing asset events, and
   `GET …/history`. This proves write-back works before Care needs it.
2. **Structured purchase facts:** extend proof capture with purchase date, retailer, brand and model (manual entry
   first, extraction proposal later in C3). Keep `SerialHint` as self-asserted evidence.
3. **C1:** `CareProfile` attached by `AssetId` (enable, read summary). Card states UNKNOWN, ACTIVE, EXPIRING,
   EXPIRED and NOT_CARE_RELEVANT. Owner-resolver and tenant isolation tests.
4. **C2 minimum:** `Brand`, `ProductFamily`, `Retailer`, `SourceEvidence` and `CoverageRuleVersion`, with a
   preview-then-apply import of `02_BRAND_REGISTRY.csv` and `01_RETAILER_UNIVERSE.csv`, plus **one** hand-researched,
   sourced, published coverage rule per test brand (Bosch, Samsung, Hisense).
5. **C4 minimum:** pure evaluator → `CoverageRecord` + `CoverageDecision` (input snapshot and rule-version id) →
   "Under warranty · expires 14 Mar 2027" on the card, "How KADARI knows this" on coverage detail, and a
   `CoverageActivated` history entry.

**Proof:** Bosch washing machine + receipt (date and retailer) + serial → published rule v1 → ACTIVE until a computed
date, with decision provenance. Publish rule v2 → re-evaluate → new decision on v2 while the old decision still
reads v1. Unknown marketplace brand → UNKNOWN, no guessed dates.

Registration (C5) is the next slice. It is the first that needs `ExternalActionAuthorisation`, the idempotency key
and, for Automatic mode, the outbox.

This splits the brief's STEP 3 (C1) and STEP 4 (C2) differently: a Care card with no coverage behind it would show
only "UNKNOWN". I recommend building C1 + minimum C2 + minimum C4 as one demonstrable slice, then broadening C2 to
the full registry import. Either order works. This one reaches CARE/16 Phase 1's "success proof" earliest (Q11).

---

## 8. Conflicts found between the brief, the authorities and the code

| # | Conflict | Recommendation |
|---|---|---|
| K1 | `CLAUDE.md` §6a ("no message bus … don't pre-build it") vs the STEP 2b brief (RabbitMQ, outbox, Worker) and the Ripple reference §7 | Q1. If approved, update `CLAUDE.md` §6a in the same commit as the Worker. |
| K2 | `CLAUDE.md` names projects `KADARI.*`, `IKADARIDbContext`; the code is `Kadari.*`, `IKadariDbContext`. `CLAUDE.md` §6 describes MassTransit consumers, `Contracts` and kebab-case endpoints that do not exist. | Fix the contract to match the code when STEP 2 lands. |
| K3 | Slice split threshold: `CLAUDE.md` §3 says about 60 lines, Ripple §3 says about 100 | Keep `CLAUDE.md` (it is the binding contract). |
| K4 | CARE/12 `RuleStatus` vs CARE/07 §17 rule lifecycle names | Use CARE/12's enum, with `ReviewDue` computed from `LastVerifiedAt`. |
| K5 | Registry CSVs carry no warranty durations, deadlines or start basis | Import as reference data plus DRAFT rules. Publishing needs researched values (Q4). |
| K6 | Boards are UK-styled (£, Currys/Argos/John Lewis); registry and platform are SA | Q5. |
| K7 | `AssetCategory` is too coarse for Care eligibility or routing | Add a Care product category from the registry rather than widening the insurance enum (Q3). |
| K8 | Ripple doc §2 says the Migration project is ✅; it exists but is not run by the AppHost | Wire it up in STEP 2. |

---

## 9. Open questions for the founder or the team

Questions marked **[Founder]** change product behaviour, privacy or scope (CARE/19 escalation rule). The rest are
engineering decisions I can make; I've stated my default for each so you only need to reply where you disagree.

1. **[Team] Message bus.** Approve replacing `CLAUDE.md` §6a's "no message bus" with the outbox, RabbitMQ and Worker
   (STEP 2b)? *Default: yes. Automatic registration and durable reminders are the real need §6a anticipated.* A
   lighter alternative is Postgres-backed jobs swept by a hosted service, with no RabbitMQ.
2. **[Founder] Automatic registration in V1.** CARE/14 C5 accepts that Automatic may be "representable" without being
   switched on for every brand. Ship V1 with **Assisted and Guided only**, and Automatic behind a per-rule flag?
   *Default: yes.* No OEM credentials are stored (CARE/14 §7).
3. **[Team] Care eligibility.** Which assets are Care-relevant? *Default: eligible when a registry `ProductFamily`
   category resolves (appliances, electronics, computing, power tools); clothing, jewellery and art are
   NOT_CARE_RELEVANT unless a rule says otherwise. The customer can also enable Care manually.*
4. **[Founder] Rule research for the P0 brands.** Who researches and signs off the first published coverage and
   registration rules (durations, deadlines, sources)? The CSVs do not contain them. *Until then the engine returns
   UNKNOWN, as designed.* Can the founder's team supply a sourced sheet for the twelve CARE/03 §16 P0 brands?
5. **[Founder] Market for V1.** South Africa only (ZAR, SA retailers, POPIA), matching the registry? The boards show
   UK retailers and £. *Default: SA only; `CountryCode` on every rule keeps multi-country open.*
6. **[Team] Model usage and cost.** Build the single model-call abstraction (usage and cost row per call) in STEP 2
   and move `IVisionEnricher` onto it, or only when C3 extraction lands? *Default: with C3.*
7. **[Founder] Care Ops staffing and role.** Who works the Care Ops queues: `CustomerSuccess`, or a new `CareOperator`
   role? *Default: new `CareOperator` role, plus `RegistryEditor` for publishing.* Should insurer tenants (Portal)
   see their customers' Care cases? *Default: no in V1.* That would be a new data disclosure needing customer consent.
8. **[Team] Registry keys.** `smallint` (CLAUDE.md §6 lookup rule) or UUIDv7 for brands and retailers? *Default:
   UUIDv7 for rule versions and sources; `smallint` for small fixed enums only.*
9. **[Founder] Transfer and Care.** When an asset is transferred or sold, does the new owner see the previous owner's
   cases? *Default: they see coverage, outcomes and repair history, but not messages or contact details.*
10. **[Founder] Navigation.** Is a global "Needs attention" or notifications surface wanted in V1? The boards show a
    bottom bar that the app does not have. *Default: no new nav item; Care lives on the asset, with deep links from
    notifications.*
11. **[Team] Slice order.** Build C1 + minimum C2 + minimum C4 together (§7) instead of C1 alone then C2? *Default:
    yes.*
12. **[Founder] Notification channel and consent.** Care reminders via WhatsApp or SMS (Twilio) or email. Is opt-in
    per channel required before the first reminder? *Default: yes, explicit opt-in on enabling Care. No marketing
    content.*

---

## 10. Verification for this step

- **Done:** read all CARE authorities, the Ripple reference, `CLAUDE.md`, the four boards, the registry CSV headers
  and their contents, and the source files cited above.
- **Not done:** no build or tests were run. This step changes no code, and the sandbox has neither the .NET SDK
  (`dotnet: command not found`) nor Docker. The cloud environment's setup script, which installs .NET 10, did not
  run in this session. STEP 2 onward needs it.
