# KADARI Care: checkpoint after STEP 2–4 (C1 Care Foundation, C2 Registry)

**Date:** 2026-10-24
**Branch:** `claude/zen-cray-ihxtla`
**Scope:** STEP 2 (CI + background-work foundation), and STEP 3 + 4 built together as one slice: C1 + C2 + the minimum
of C4 that C1 needs (reconciliation §7, Q11).

Q1–Q12 in `00-RECONCILIATION.md` §9 were approved with their defaults ("start building"). Where this report says
"per Qn" it means that default.

---

## 1. What was built

| Commit | What |
|---|---|
| `739ff7f` | GitHub Actions CI with the same five jobs as `.gitlab-ci.yml`. Removed again afterwards at the owner's request; `.gitlab-ci.yml` is unchanged. |
| `a4d01b5` | MassTransit EF outbox behind the existing `IIntegrationEventPublisher`; `Kadari.Contracts`; `Kadari.Worker` (the only consumer host); RabbitMQ and the migration runner in the AppHost; RabbitMQ and the worker in the production compose; architecture tests for the new projects; `CLAUDE.md` §6a rewritten. |
| `bf8a741` | Asset history; purchase facts as evidence; `CareProfile` (C1); the registry entities, the immutable `CoverageRuleVersion` and the preview-then-apply import (C2); `CoverageEvaluator` + `CoverageDecision` (minimum C4); Care in POPIA erasure/export; `CLAUDE.md` §6b. |
| `1a40708` | Customer screens: the Care card on the asset, and the Care page with Overview, Coverage and History tabs. |

---

## 2. CARE/14 definitions of done

### C1 — Care Foundation

| Definition of done | Status | Proof |
|---|---|---|
| An existing asset can be Care-enabled | ✅ | `EnableCareCommand`; browser run on the demo Samsung TV |
| No duplicate asset is created | ✅ | Unique `CareProfiles.AssetId`; test `EnableCare_AttachesOneProfileToTheExistingAssetAndCreatesNoOther` (asset count stays 1, enabling twice keeps 1 profile) |
| Care state survives reload/session changes | ✅ | Persisted profile; test `Summary_SurvivesReloadFromTheDatabase`; the browser run reloads across routes |
| Care card reflects backend state | ✅ | The card is rendered from `GET …/care` alone; no Care logic in the UI |
| Non-Care-relevant assets degrade cleanly | ✅ | Clothing, jewellery and art: no card (test + browser run on the diamond ring); with Care switched off the routes 404 and the client shows no card |
| Scope: profile, eligibility, card, overview, coverage summary, next attention date, open-case count, audit/event wiring | ✅ | Open-case count is always 0 until C9 adds cases. Audit rows: `CareEnabled`, `CareCoverageEvaluated`, `RegistryImported` |

### C2 — Registry Read Layer

| Definition of done | Status | Proof |
|---|---|---|
| Rule lookup returns a deterministic result plus source version | ✅ | `CoverageEvaluator` is pure; test `Evaluate_IsDeterministic`; decisions store `MatchedRuleVersionIds` and the coverage detail shows the source |
| Stale/unknown is distinguishable from a valid rule | ✅ | A stale rule (not re-verified in 365 days, or marked stale) can only give `LikelyActive`, flagged `RuleStale`; no rule gives `Unknown`; disagreeing rules give `NeedsReview` |
| Changing a rule does not alter historical decisions | ✅ | Test `APublishedRuleChange_LeavesEarlierDecisionsOnTheVersionTheyUsed`: v1 is superseded, v2 published, the earlier decision still cites v1 |
| Every production rule carries source, confidence, last verified and version | ✅ | Enforced by `CoverageRuleVersion.Draft` and the importer. A rule cannot be published without a duration |
| Imported from `CARE/REGISTRY/*.csv` by the migration runner, preview then apply | ✅ | 64 brands, 30 retailers, 10 routing models and 83 sources, each brand and retailer with its source URL. A re-run is "up to date". Apply refuses a plan that differs from the preview |
| Exact/model rules override broader rules; expired/superseded rules stay addressable | ✅ | Specificity tests; superseded versions are kept and never selected |

**Not imported, and why:** `04_WARRANTY_AUTOMATION.csv` feeds registration (C5). `05_RESEARCH_BACKLOG.csv` is a
planning document.

**The registry has no coverage rules yet.** The workbook gives no warranty durations, so
`docs/care/registry/coverage-rules.csv` ships with only its header. Until it holds researched rows, every real asset
reads "KADARI hasn't confirmed this yet". This is correct behaviour, but it means Care shows nothing useful to
customers yet (Q4, §6).

---

## 3. CARE/15 gates — status for what exists so far

### G0 — Architecture integrity

| Criterion | Status |
|---|---|
| Care attaches to the existing Digital Twin identity | ✅ `CareProfile.AssetId`; ownership checked through the existing owner/co-owner and tenant model (`OwnedAssets`) |
| No duplicate asset system | ✅ No Care asset table, user model or file store. Purchase facts are `AssetEvidence` |
| Registry rules versioned and source-backed | ✅ |
| Material decisions keep their rule-version reference | ✅ `CoverageDecision` (append-only: input snapshot, rule version ids, engine version) |
| External actions support idempotency/retry safety | ⏳ The outbox (deterministic message id + inbox) is in place. No external action exists yet; the first is registration (C5) |
| Material outcomes write back to asset history | ✅ For what exists: Care enabled, warranty confirmed/changed. Case outcomes come with C11 |
| Care uses the existing backend/event conventions | ✅ Domain events dispatched in the transaction, CQRS slices, owner resolvers, Problem Details. Architecture tests pass |
| Fail examples (dates in the UI, contacts in front-end constants, a new asset id, routes changing after a registry update) | ✅ None present. Dates come only from the engine |

### G1 — Functional vertical slices A–E

**Not reachable yet.** All five slices need "Something's wrong", routing and cases (C7–C11). What exists covers the
first part of each: asset → evidence → coverage. Slice C (registration) also needs C5 and a researched Bosch rule.

### G2 — Safety, privacy, auditability (applicable items)

| Criterion | Status |
|---|---|
| Audit identifies actor, action, rule/source and result for material decisions | ✅ `AuditEvent` (actor, action, before/after) plus `CoverageDecision` (rule, source, inputs) |
| Raw documents and messages kept out of analytics | ✅ No analytics events added. No document content stored on Care rows |
| Ops access is role-controlled | ✅ Care permissions: Customer has view + manage, CustomerSuccess has view only. There is no Ops surface yet |
| POPIA | ✅ Erasure soft-deletes the Care profile with the asset; the subject-access export includes purchase facts, Care and history (tests) |
| Safety interruption, proof-pack disclosure, authorisation before submission | ⏳ C7, C10, C5 |

### G3 — Operational readiness

| Criterion | Status |
|---|---|
| Registry Factory can publish new rule versions without a code deploy | ◑ Editing the CSV publishes through the import on the next migration run, but that is a file change and a deploy. A UI for operators to publish rules without a deploy is C13 |
| Stale-rule, failed-registration, unknown-route and escalation queues | ⏳ C12 |
| KADARI failure vs external-provider failure distinguishable | ⏳ No external calls yet |

### Required automated test classes (CARE/15 §7) covered so far

Unit tests: date calculations and month-end clamping, rule specificity ordering, coverage expiry, stale-rule
detection, rule state transitions. Integration tests: asset → Care profile, registry import → rule resolution, rule
change → historical decisions unchanged.

---

## 4. Verified, and how

All suites were run in this session; Docker was available.

| Suite | Result |
|---|---|
| `Kadari.Domain.Tests` | 115 passed (39 new) |
| `Kadari.Application.Tests` | 56 passed (6 new) |
| `Kadari.ArchitectureTests` | 16 passed (7 new: messaging rules; every new request declares its scope) |
| `Kadari.Infrastructure.Tests` (Testcontainers Postgres 17) | 147 passed (22 new: outbox atomicity, Care flow, registry import, data rights) |
| OpenAPI contract | regenerated and committed; Client and Portal types regenerated; `api:check` passes |
| Migration drift | no pending model changes |
| Client | `tsc -b` clean, eslint 0 errors, vite build succeeds |
| Smoke | Postgres + RabbitMQ containers: migration runner applies all migrations and imports the registry; Worker and API both log "Bus started: rabbitmq://…" |
| Browser (Playwright, Chromium) | Real API + Postgres, demo customer signed in by OTP through Mailpit: enable Care → "not confirmed yet" → purchase details on an asset with a receipt → "Under warranty — covered until 14 March 2028", with the source on the Coverage tab and three History entries. The Active state used a **local demo rule loaded only into the throwaway database** |

## 5. Not verified

- **No CI runs on GitHub.** The GitHub Actions workflow was removed at the owner's request (its jobs never started on
  this repo). `.gitlab-ci.yml` remains; every check it runs was run locally and passed.
- **No message has been sent end to end** from the outbox through RabbitMQ to a consumer, because no consumer exists
  yet. That test arrives with the first Care consumer (C5/C6).
- **The full `dotnet run --project src/Kadari.AppHost`** was not run (Aspire's orchestrator was not available here).
  Each piece was run on its own.
- **The Portal front-end job**: the Portal has no pinned `packageManager`, and corepack's default pnpm was broken in
  this sandbox, so its types were generated with the same `openapi-typescript` binary instead. The output is
  byte-identical to the check.

## 6. Decisions needed from the founder

1. **Researched coverage rules for the P0 brands (Q4).** This is the blocker before Care is useful. Someone must fill
   `docs/care/registry/coverage-rules.csv` (format in `docs/care/registry/README.md`) with a source and a
   last-verified date per rule. Start with Samsung, LG, Hisense, Defy/Beko, Bosch/Siemens and Apple. Care stays
   switched off in production (`Features__Care: "false"`) until then.
2. **Confirm South Africa only for V1 (Q5).** The design boards show £ and UK retailers.
3. Nothing else blocks C3–C6.

## 7. Next, in CARE/14 order (one PR per epic, stopping after each)

C3 extraction (receipt and label proposals the owner confirms, through one model-call abstraction that records
usage and cost) → C4 complete (retailer guarantees, extended cover, registration flags) → C5 registration (per-action
authorisation, an idempotency key unique in the database, Assisted/Guided first, the first Worker consumer) → C6
reminders (a Worker sweep) → C7 "Something's wrong" → C8 routing → C9–C11 cases and resolution → C12–C13 Care Ops
and the Registry Factory.
