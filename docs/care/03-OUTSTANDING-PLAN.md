# KADARI Care: plan for what is outstanding

**Date:** 2026-10-24
**Branch / PR:** `claude/zen-cray-ihxtla`, SethStrider/Kadari#1
**Where we are:** C1–C12 and C14 are built and tested (Domain 200, Application 64, Architecture 18, Infrastructure 190
tests passing). The screens follow the design boards. C13 is not built. Care is switched off in production
(`Features__Care: "false"`) until the items in Phase 1 are done.

Each item says **who** does it, **what is needed** to start, and **when it counts as done**. "Claude" means it can be
built in a session once its inputs exist.

---

## Phase 0 — Decisions (founder, about one meeting)

Nothing in Phases 3–5 can start well without these. Each has a suggested default, so a "yes" is enough.

| # | Decision | Suggested default | Unblocks |
|---|---|---|---|
| D1 | Market for V1 | South Africa only (ZAR, SA retailers, POPIA) | Rule research scope, copy, currency |
| D2 | Who publishes registry rules, with or without a second approver | New "Registry editor" role; a second person approves before publishing | C13 |
| D3 | Source of truth for rules | Portal after launch; the CSV files become a one-time starting set | C13 |
| D4 | What counts as a valid rule source | A link to the official page plus a "last checked" date; no automatic page watching in V1 | C13 |
| D5 | A case ending in "Replaced" | Ask the owner; if yes, create the new item in the same room and link it to the old one | C11 replacement |
| D6 | Brand-level metrics | Normalise brand on purchase evidence against the registry (no separate pipeline yet) | C14 extension |
| D7 | Who runs Care Ops | Customer Success, as built | Staffing, permissions |
| D8 | Look and feel | Keep KADARI teal (insurers still re-skin it); no bottom menu bar in V1 | Optional restyle |

**Done when:** each decision is written into `docs/care/00-RECONCILIATION.md` §9.

---

## Phase 1 — Pilot readiness (blocking)

### 1.1 Researched rules — *the main blocker*

- **Who:** a researcher or Care Ops (not engineering).
- **Needed:**
  - Official warranty pages for the P0 brands: Samsung, LG, Hisense, Defy/Beko, Bosch/Siemens, Apple.
  - Return and guarantee policies for the main retailers in the registry: Makro, Game, Takealot, Incredible Connection,
    Hirsch's, HiFi Corp.
- **What to fill in** (formats in `docs/care/registry/README.md`):
  - `coverage-rules.csv`: warranty length per brand and category, start basis, registration needed, extension.
  - `routing-rules.csv`: where a fault goes per brand/category, in cover and out of cover, and the official channel.
  - `registration-rules.csv`: brands where registering adds cover, with the official page and the fields it asks for.
- **Every row needs** a source title, source URL, last-checked date and confidence. Only `High`/`Verified` rows give
  confident answers.
- **Effort:** about 1–2 days for the P0 brands.
- **Done when:** the migration runner's preview shows the rows with no errors, and a test asset for each P0 brand shows a
  real warranty date and a green route.

### 1.2 Safety wording sign-off

- **Who:** founder, with legal or compliance.
- **Needed:** a review of `SafetyGate.Guidance` and the warning words in
  `src/Kadari.Domain/Care/RoutingEvaluator.cs`, and the emergency number to show.
- **Done when:** the approved text replaces the provisional one and the "PROVISIONAL" note is removed (Claude, 15 min).

### 1.3 Production configuration

- **Who:** whoever runs the deployment.
- **Needed**, as secrets in `deploy/docker-compose.prod.yml`, never in chat or code:

  | Setting | Why |
  |---|---|
  | `VISION_PROVIDER=OpenAiCompatible`, `OPENAI_VISION_API_KEY` | Receipt and label reading |
  | `ModelPricing__Models__gpt-4o-mini__InputPerMillionTokens` / `OutputPerMillionTokens` | Without them usage is recorded with no cost. Not set anywhere today |
  | `RABBITMQ_PASSWORD` (already in the compose file) | Reminders and the outbox |
  | `TWILIO_*`, `SMTP_*` (already in the compose file) | Reminder SMS and email |
  | `KADARI_DOMAIN` | Links in reminders |

- **Also:** a fresh OpenAI key. The one pasted in chat should be rotated.
- **Done when:** the API and the Worker both start with the readiness guard passing.

### 1.4 Verification that could not be done in the build environment

| Check | Needed | Who |
|---|---|---|
| Real receipt reading | `api.openai.com` allowed in the cloud environment's network settings (or run locally) | Founder enables; Claude reruns the live test |
| Real reminder delivery | Twilio and SMTP test credentials | Ops; Claude verifies |
| Full stack via Aspire (`dotnet run --project src/Kadari.AppHost`) | A developer machine with Docker | Developer, about 30 min |
| CI | GitLab pipeline on the PR branch (GitHub Actions was removed on request) | Developer |

### 1.5 Review and merge

- **Who:** engineering reviewer (James).
- **Needed:** time to review PR #1. It is large (~300 files), so review it by area: platform/outbox, Care domain,
  slices, UI.
- **Done when:** merged to `main` with GitLab CI green.

### 1.6 Pilot switch-on

- **Needed:** 1.1–1.5 done. Note that `Features__Care` is global today. If the pilot should cover one insurer only,
  a per-tenant switch is a small addition (Claude, about half a day).
- **Done when:** a pilot group uses Care. Care Ops watches the queue, and the Care metrics page is checked weekly.

---

## Phase 2 — Quick fixes (Claude, no input needed, about half a day in total)

| Item | Why |
|---|---|
| Hide "Users" in the Portal menu for Customer Success | They see it but get "Could not load users" |
| Show an appointment that was never confirmed as "not confirmed", not "cancelled", when a case closes | Reads oddly next to "Repaired" |
| Add default model pricing to `appsettings.json` (overridable) | Cost is null without it (see 1.3) |
| Look at the `System.Security.Cryptography.Pkcs 10.0.0` vulnerability warnings in the build | High-severity advisory on a transitive package; pin a patched version if one is available |
| Remove the now-unused `issueClasses` export in the client | Tidy-up |

---

## Phase 3 — C13 Registry Factory (after D2–D4; Claude about 3–4 days)

- **Scope:**
  - A Portal "Registry" area: list rules, edit a rule as a draft, preview what it changes, publish.
  - A second-person approval if D2 says so.
  - A stale-rule queue: rules not re-checked within a year go to Care Ops.
  - A Care Ops "reusable knowledge" resolution that pre-fills a draft rule.
- **Keeps:** immutable versions (an edit is the next version), old decisions linked to the version they used, an
  audit of who published.
- **Done when** (CARE/14 C13): an authorised operator updates a rule without engineering, the previous version stays
  available, new decisions use the new version, and old decisions stay linked to the old one.

## Phase 4 — C11 replacement → new item (after D5; Claude about 1 day)

- When a case closes as "Replaced", offer to create the new item.
- Link it to the old one, keep the old item's history, and re-run cover for the new item.

## Phase 5 — C14 metrics extension (after D6; Claude about 1–2 days)

- Cases by brand/group.
- Time to first external response (from "Details sent" to the first reference or reply).
- Retailer returns vs warranty repairs.
- Still aggregate counts only.

## Phase 6 — Optional look-and-feel changes (after D8)

- Switch KADARI's primary colour to the boards' green. This changes every screen; insurer branding still overrides it.
- Bottom menu bar and a "Docs" tab, if wanted.

---

## Suggested order and timing

| Week | Work |
|---|---|
| 1 | Phase 0 decisions · Phase 2 quick fixes · start 1.1 research · 1.3 configuration · review PR (1.5) |
| 2 | Finish 1.1 · 1.2 sign-off · 1.4 verification · merge · pilot switch-on (1.6) |
| 3–4 | Phase 3 (C13) · Phase 4 · Phase 5, while the pilot runs |

**The critical path is researched rules (1.1) plus the safety sign-off (1.2).** Everything else can run alongside them.
