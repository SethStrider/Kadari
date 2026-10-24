# KADARI Care: checkpoint after C3–C14

**Date:** 2026-10-24
**Branch:** `claude/zen-cray-ihxtla` (PR SethStrider/Kadari#1)
**Scope:** everything after the first checkpoint (`01-CHECKPOINT-C1-C2.md`): C3 extraction, C4 coverage completion,
C5 registration, C6 reminders, C7–C11 "Something's wrong" and cases, C12 Care Ops and C14 metrics. C13 (Registry
Factory) was not built; §6 says why.

The founder asked for "as much as you can that doesn't need my input", so the defaults from the reconciliation (§9,
Q1–Q12) were used throughout. Where a choice needs the founder, it is listed in §6 instead of being guessed.

---

## 1. What was built

| Commit | Epic | What |
|---|---|---|
| `7872548` | C3 | Receipt and label reading. The model proposes and the owner confirms; nothing is saved before that. Every call goes through one gateway, `MeteredModel` (consent gate, quota, once-per-request key, usage and cost recorded in `ModelUsages`). An architecture test enforces that. |
| `adc46a0` | C4 + C5 | Coverage engine "coverage-2" (retailer guarantees, registration-required rules, extension months). Warranty registration: offer, per-action authorisation listing exactly what is shared, an idempotency key unique in the database, a Guided or Assisted step, "complete" only with the provider's reference, extended cover and history. |
| `cc154b9` | C6 | Reminders: a deterministic plan, a dedupe key unique in the database, a Worker sweep that queues due reminders through the outbox, a Worker consumer that delivers them only with the owner's opt-in. Proven end to end through a real RabbitMQ. |
| `039566d` | C8 | Routing registry: immutable versioned `RoutingRuleVersion`, a pure `RoutingEvaluator` that never guesses, append-only `RoutingDecision`, `routing-rules.csv` imported preview-then-apply. |
| `0349b2e` | C7, C9–C11 | "Something's wrong": safety gate first; a case on the existing asset; route from the registry or a person; the case pack (product, purchase, problem; never contact details); "I've sent it" with a handoff authorisation, audit and idempotency key; references, provider replies, promises (chased the day after), appointments, outcome written to history; the open-case count; POPIA erasure and export. |
| `7da2a0f` | C7, C9–C11 UI | Customer screens: a Problems tab, the report form, the case screen. |
| `9bb27f8` | C12 | Care Ops: `OpsWorkItem` queue (unknown route P2, safety P0, failed registration P2, rule conflict P2, stalled case P1), take, resolve with the kind of knowledge produced, set a route that the owner then confirms. Portal "Care Ops" page. Permission `CareOpsReview` (Customer Success). |
| `0535972` | C14 | Care metrics from the operational tables (aggregate counts only); Portal "Care metrics" page; `CareMetricsView` permission. CLAUDE.md §6b rules for routing, safety, cases and Ops. |

---

## 2. CARE/14 definitions of done

### C3 — Evidence extraction

| Definition of done | Status | Proof |
|---|---|---|
| Owner can capture receipt + serial label and reach a usable Care profile | ✅ | `ReadReceipt`, `ReadProductLabel`; the form pre-fills only after the owner confirms |
| Failed extraction does not block manual completion | ✅ | Model unavailable, quota or consent missing → the reason is shown and the form stays editable (tests) |
| Extraction cannot silently overwrite confirmed values | ✅ | Extraction returns a proposal; only `RecordPurchaseDetails` writes, as new evidence |

### C4 — Coverage engine (complete)

| Definition of done | Status | Proof |
|---|---|---|
| P0 test cases resolve correctly | ◑ | Engine tests pass. **There are no real P0 rules yet** (§6.1) |
| Unknowns are explicit | ✅ | `Unknown` / `LikelyActive` / `NeedsReview`; a conflict goes to Care Ops |
| Original terms/source linkable; exact rule version stored | ✅ | `CoverageDecision` holds rule version ids; the coverage tab shows the source |

### C5 — Registration

| Definition of done | Status | Proof |
|---|---|---|
| Guided, assisted and automatic-capable flows represented end to end | ✅ | Automatic runs as Assisted until an OEM integration exists (by design) |
| Confirmation becomes permanent asset evidence | ✅ | `AssetEvidenceKind.Registration`, plus a history entry and re-evaluated cover |

### C6 — Reminders

| Definition of done | Status | Proof |
|---|---|---|
| Deterministic and testable | ✅ | `ReminderPlan` is pure (tests) |
| Duplicates prevented | ✅ | Unique `DedupeKey`; deterministic outbox message id plus inbox |
| Delivery state auditable | ✅ | Queued, Sent (channel and time), or NotSent without opt-in |

### C7 — "Something's wrong" intake

| Definition of done | Status | Proof |
|---|---|---|
| A safe, normal case proceeds | ✅ | Green route → pack → sent (Postgres test and browser) |
| A safety case interrupts appropriately | ✅ | Stop-use guidance leads the screen; Care Ops P0 (test and browser). **Wording is provisional** (§6.2) |
| Incomplete evidence can still save | ✅ | A case opens with no purchase facts; the route is then "a person will find it" |

### C8 — Routing

| Definition of done | Status | Proof |
|---|---|---|
| Route reproducible from stored inputs | ✅ | `RoutingDecision.InputSnapshotJson` + engine version; the evaluator is pure and deterministic (test) |
| An unknown or conflicting route does not guess | ✅ | `ResearchRequired`/red on no match or a tie between different routes (tests) |
| The owner sees one primary recommendation | ✅ | One route per case; amber asks the owner to confirm |

### C9 — Case engine

| Definition of done | Status | Proof |
|---|---|---|
| One generic case for retailer, OEM and service-centre routes | ✅ | `AssetCase` carries any `RouteType` |
| External reference can be added later | ✅ | `AddCaseReference`, de-duplicated |
| All material transitions emit events | ✅ | Every transition appends a sequenced `CaseEvent`; open/close/promise/appointment raise domain events |
| A closed case writes its outcome to asset history | ✅ | `OnAssetCaseClosedAppendHistory` (outcome only, no owner text) |

### C10 — Communication and proof pack

| Definition of done | Status | Proof |
|---|---|---|
| Owner can create, inspect and send a pack without an OEM API | ✅ | Pack preview, copy, the provider's own link (web, mailto, wa.me, tel), "I've sent it" |
| Timeline records the handoff | ✅ | `Sent` event; `CaseMessage`; `ExternalActionAuthorisation` (CaseHandoff); `ExternalActionAudit` |
| No duplicate submissions from retries | ✅ | Idempotency key unique per case (test) |

### C11 — Commitments, follow-up and resolution

| Definition of done | Status | Proof |
|---|---|---|
| A provider promise creates a due item | ✅ | `CommitmentOverdue` reminder for the day after (test) |
| An overdue case triggers attention | ✅ | Reminder to the owner and a Care Ops `CaseStalled` P1 item from the sweep (test) |
| Resolution updates coverage/history appropriately | ◑ | History: yes. Coverage is not re-evaluated on resolution (a repair does not change the warranty in V1) |
| A replacement can create a related new Digital Twin, keeping the old asset's history | ❌ | Not built. The outcome `Replaced` is recorded; creating the new asset needs a product decision (§6.5) |

### C12 — Care Ops

| Definition of done | Status | Proof |
|---|---|---|
| An operator can resolve an exception | ✅ | Take, resolve with the kind of knowledge (tests, browser) |
| A resolution can create or update a registry rule with provenance | ◑ | It is marked "reusable registry knowledge", and the operator's notes carry the source. The rule itself is still added through the CSV import (that is C13) |
| A customer case can resume from manual intervention | ✅ | The operator's route → the owner confirms → sends (test, browser) |

### C13 — Registry Factory: ❌ not built (§6.4)

### C14 — Analytics and commercial readiness

| Metric | Status |
|---|---|
| Care-active assets, coverage-found rate, offers, registration completion, cases, routes by type, outcome mix, resolution time, rejection rate, manual interventions per 1,000 | ✅ `GetCareMetrics` |
| Extra cover activated | ✅ counted as completed registrations |
| Cases by brand/group, time to first external response, retailer returns vs warranty repairs | ❌ need an analytics pipeline (brand is free text on purchase evidence) |

---

## 3. CARE/15 gates — what changed

- **G0 architecture:** external actions now exist (registration, case handoff). Each has an idempotency key unique in
  the database and an audit row; the bus path is proven through RabbitMQ. Case outcomes write back to asset history.
  Routes do not change after a registry update: a case keeps the decision it was given.
- **G1 vertical slices A–E:** they are now *reachable* as flows. Slice A (TV fault in warranty → OEM) ran in the
  browser on a **local demo rule**. B and D (retailer / service centre) are covered by tests with test rules. C
  (registration) is covered by tests. E (unknown route → Ops → customer) ran in the browser. None of them can pass
  for real until researched rules exist (§6.1).
- **G2 safety, privacy, audit:** safety interruption ✅ (wording provisional). Proof-pack disclosure before sending ✅.
  Authorisation before submission ✅. Ops role-controlled ✅. Operators see the owner's words only for a safety review ✅.
  Metrics are aggregate counts only ✅. Erasure redacts cases ✅. The export includes cases ✅.
- **G3 operational readiness:** stale-rule, failed-registration, unknown-route and escalation queues exist ✅ (stale
  rules are flagged on the decision, but there is no separate stale-rule queue yet). KADARI vs provider failure is
  distinguished on registrations ✅. Publishing rules without a deploy ❌ (C13).

---

## 4. Verified, and how

| Check | Result |
|---|---|
| `Kadari.Domain.Tests` | 199 passed (84 more than the first checkpoint) |
| `Kadari.Application.Tests` | 64 passed |
| `Kadari.ArchitectureTests` | 18 passed (every new request declares its scope; AI only through `MeteredModel`) |
| `Kadari.Infrastructure.Tests` (Testcontainers Postgres 17; one test with RabbitMQ) | 190 passed |
| Migration drift | `has-pending-model-changes`: none |
| OpenAPI + types | regenerated; Client and Portal `schema.d.ts` match the document byte for byte |
| Client | `tsc -b` clean, eslint clean on changed files, vite build OK |
| Portal | dependencies installed (pnpm 11.21.0), `tsr generate`, `tsc -b`, eslint, vite build OK |
| Migration runner | ran against a fresh Postgres; imported the registry with demo coverage and routing rules |
| Browser (Playwright/Chromium, real API + Postgres + Mailpit OTP) | Customer: report a TV fault → green route to the manufacturer → pack shows exactly what is shared → "I've sent it" → reference and promise recorded → closed as repaired → history entry. A safety report on a product with no route → stop-use guidance first, "being researched". Portal as the Customer Success demo user: queue shows Safety review (P0) and Unknown route (P2) → operator sets a route → the customer sees "Use this route". **The coverage and routing rules used were local demo rules in a throwaway database, not research.** |

## 5. Not verified

- **Real model calls** (Gemini / OpenAI-compatible) — no keys in this environment. The gateway, consent, quota and
  usage recording are tested against a fake.
- **Real message delivery** (SMS/email of reminders) — delivery goes through the existing notification channel, tested
  with a recording channel. RabbitMQ delivery itself is proven.
- **The full Aspire AppHost run** — each piece was run on its own, as at the first checkpoint.
- **CI** — there is none on GitHub (removed at the owner's request); `.gitlab-ci.yml` is unchanged and its checks were
  run locally.

## 6. Decisions needed from the founder

1. **Researched rules — the blocker for real use.** `coverage-rules.csv`, `registration-rules.csv` and
   `routing-rules.csv` ship with headers only (formats in `docs/care/registry/README.md`). Without rows, cover reads
   "not confirmed yet" and every problem goes to Care Ops. Start with the P0 brands (Samsung, LG, Hisense, Defy/Beko,
   Bosch/Siemens, Apple) and the big retailers' return policies. Care stays off in production
   (`Features__Care: "false"`) until then.
2. **Safety wording sign-off.** `SafetyGate.Guidance` and the keyword list are marked PROVISIONAL. They need a
   reviewed, approved text (CARE/15 G2 "verified safe wording"), including the emergency number to show.
3. **South Africa only for V1** (Q5) — still unconfirmed; the design boards show £ and UK retailers.
4. **C13 Registry Factory: who may publish a rule, and where it lives.** Today the CSV files are the single source of
   truth and an edit needs a deploy. A Portal editor needs decisions: which role approves; whether rules edited in the
   Portal replace the CSV as the source, or the two coexist; whether a second person must review before publishing.
   Building it without those answers would split the source of truth, so it was not built.
5. **Replacement → new Digital Twin (C11).** When a case ends in a replacement, should KADARI create the new asset
   automatically (copying category and household, linking the old one) or ask the owner? This also touches the
   insurer ledger value.
6. **Metrics beyond counts (C14).** Brand/group breakdowns and time to first external response need an analytics
   pipeline or a decision to normalise brand on purchase evidence. Say which.
7. **Ops staffing:** Care Ops is given to the Customer Success role. Confirm, or name a separate "Care specialist" role.

## 7. Next

With §6.1 and §6.2 answered, Care can be switched on for a pilot. C13 follows once §6.4 is decided.
