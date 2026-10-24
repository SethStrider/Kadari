# KADARI Care — Implementation Execution Plan

**Status:** Build execution authority  
**Date:** 2026-10-06  
**Purpose:** Convert the Care product, CX, automation, commercial, registry and API authorities into a buildable implementation programme without architectural drift.

## 1. Non-negotiable implementation boundary

KADARI Care is additive to the existing KADARI Digital Twin and asset foundation.

Do not:
- create a second asset identity;
- duplicate existing evidence/document storage;
- duplicate existing user/tenant/auth models;
- create Care-only copies of asset history;
- make retailer or OEM integration a dependency for V1;
- encode warranty policy in UI code;
- hard-code brand-specific flows where a generic rule/case engine can solve the same problem.

Care must attach to the existing asset and write material outcomes back into the same asset history.

## 2. Delivery doctrine

Build vertically, not layer-by-layer only.

Every major increment should demonstrate a real customer journey against a real test brand/category.

Preferred sequence:

**Asset → Care Profile → Coverage → Registration → Reminder → Problem → Route → Case → Resolution → History**

Each increment should leave the product usable even before all later features exist.

## 3. Core workstreams

### EPIC C1 — Care Foundation

**Outcome:** Existing KADARI assets can expose a Care profile without changing core identity.

Scope:
- CareProfile persistence;
- Care eligibility;
- Care card on Digital Twin;
- Care overview;
- coverage status summary;
- next attention date;
- open-case count;
- basic audit/event wiring.

Dependencies:
- existing Digital Twin ID;
- existing permissions;
- existing evidence/document store.

Definition of done:
- an existing asset can be Care-enabled;
- no duplicate asset is created;
- Care state survives reload/session changes;
- Care card reflects backend state;
- non-Care-relevant assets degrade cleanly.

Suggested lead:
- backend/data: Jono;
- product integration/front end: Pierre.

---

### EPIC C2 — Registry Read Layer

**Outcome:** Production code can resolve brand/category/warranty/service rules from structured registry data.

Scope:
- registry entities from authority 12;
- import initial SA market registry;
- brand groups;
- brands;
- operators/distributors;
- product families;
- retailers;
- service networks;
- coverage rules;
- registration rules;
- routing rules;
- source evidence;
- rule versions;
- freshness metadata.

Rules:
- spreadsheet is research input, not runtime datastore;
- every production rule must have source, confidence, last verified and version;
- exact/model rules override broader brand/category rules;
- expired/superseded rules remain historically addressable.

Definition of done:
- rule lookup returns deterministic result + source version;
- stale/unknown state is distinguishable from valid rule;
- changing a rule does not alter historical decisions already made.

Suggested lead: Jono.

---

### EPIC C3 — Evidence Extraction & Product Resolution

**Outcome:** Customer photographs/receipts/labels can populate Care inputs with minimal manual typing.

Scope:
- receipt extraction;
- model/serial label extraction;
- barcode/GTIN where available;
- retailer extraction;
- purchase date;
- purchase price;
- brand/category suggestion;
- confidence per extracted field;
- user confirmation/correction.

Rules:
- inference never becomes verified fact silently;
- preserve raw evidence;
- retain extracted value + confidence + source;
- user correction must be auditable.

Definition of done:
- user can capture receipt + serial label and reach a usable Care profile;
- failed extraction does not block manual completion;
- extraction errors cannot silently overwrite user-confirmed values.

Suggested split:
- extraction/inference service: Jono;
- capture UX: Pierre.

---

### EPIC C4 — Coverage Engine

**Outcome:** KADARI determines the current coverage position in plain language.

Scope:
- CoverageRecord;
- rule matching hierarchy;
- warranty start/end calculation;
- registration-needed flag;
- retailer-return flag;
- coverage unknown state;
- source/confidence;
- plain-language explanation;
- distinction between OEM warranty, retailer route, extended protection and other cover where known.

Definition of done:
- P0 test cases resolve correctly;
- unknowns are explicit;
- original terms/source are linkable;
- the exact rule version producing the decision is stored.

Suggested lead: Jono backend + Pierre CX.

---

### EPIC C5 — Warranty Registration / Extension

**Outcome:** KADARI can help activate extra cover without the customer having to rediscover the process.

Scope:
- registration opportunity detection;
- customer authority capture;
- AUTOMATIC / ASSISTED / GUIDED modes;
- external destination metadata;
- idempotency key;
- retry logic;
- OTP/user-step support;
- confirmation capture;
- registration reference;
- proof storage;
- coverage update;
- deadline reminders.

Hard rules:
- no submission without explicit authority;
- never claim registration succeeded without evidence/confirmation;
- retry must not create duplicate registrations;
- external site failure is not KADARI failure.

Definition of done:
- one guided, one assisted and one automatic-capable test flow can be represented end-to-end even if true external automation is not yet enabled for all brands;
- confirmation becomes permanent asset evidence.

Suggested split:
- orchestration/state machine: Jono;
- authorisation/CX: Pierre.

---

### EPIC C6 — Reminder & Attention Engine

**Outcome:** Care generates useful lifecycle attention without notification spam.

Scope:
- registration deadline;
- 90-day expiry;
- 30-day expiry;
- health-check trigger;
- maintenance rule where verified;
- case follow-up;
- provider commitment overdue;
- recall placeholder/event model.

Rules:
- no blanket reminders for every low-value asset;
- reminder must explain why it matters;
- deep-link to direct action;
- allow snooze/dismiss where appropriate;
- open issue can increase expiry urgency.

Definition of done:
- reminder scheduling is deterministic and testable;
- duplicate reminders prevented;
- reminder delivery state auditable.

Suggested lead: Jono service + Pierre notification UX.

---

### EPIC C7 — Problem Intake & Safety Gate

**Outcome:** User can simply say `Something's wrong` and KADARI converts that into structured problem context.

Scope:
- natural-language description;
- voice input;
- photos/video/audio;
- error code;
- physical/accidental damage flag;
- issue classification;
- safety signals;
- evidence readiness.

Hard rules:
- do not force user to choose warranty vs repair case;
- dangerous conditions interrupt the normal flow;
- do not encourage unsafe troubleshooting;
- AI classification remains an interpretation unless evidenced.

Definition of done:
- safe normal case proceeds;
- safety case interrupts appropriately;
- incomplete evidence can still save as a draft.

Suggested split:
- classifier/orchestration: Jono;
- intake UX: Pierre.

---

### EPIC C8 — Routing Engine

**Outcome:** KADARI gives one best next action based on current verified knowledge.

Scope:
- retailer return;
- retailer credit;
- OEM direct;
- warranty administrator;
- authorised service;
- onsite;
- carry-in;
- collection/courier;
- protection provider;
- research/manual fallback.

Inputs:
- asset identity;
- category/model;
- retailer;
- purchase date;
- current coverage;
- issue class;
- registry version.

Output:
- primary route;
- why;
- target;
- required evidence;
- channel;
- confidence;
- source rule version.

Definition of done:
- route is reproducible from stored inputs;
- unknown/conflicting route does not guess;
- user sees one primary recommendation.

Suggested lead: Jono.

---

### EPIC C9 — Asset Case Engine

**Outcome:** Every material after-sales issue becomes a reusable generic case attached to the same asset.

Scope:
- AssetCase;
- CaseMessage;
- CaseAttachment;
- CaseEvent;
- CaseReference;
- CaseCommitment;
- CaseAppointment;
- RepairOutcome;
- ResolutionOutcome;
- status transitions;
- case timeline;
- audit trail.

Definition of done:
- one generic case model handles retailer, OEM and service-centre routes;
- external reference can be added later;
- all material transitions emit events;
- closed case writes outcome to asset history.

Suggested lead: Jono backend + Pierre case surfaces.

---

### EPIC C10 — Communication & Proof Pack

**Outcome:** KADARI prepares the right information and helps the user send it through existing channels.

Scope:
- structured issue summary;
- case pack preview;
- authorised data selection;
- WhatsApp handoff;
- email handoff;
- official support page handoff;
- copy summary;
- mark sent;
- capture external reference;
- record provider reply;
- proof pack view.

Rules:
- user sees what will leave KADARI;
- minimum necessary personal data;
- no duplicate outbound submissions from retries;
- provider reply may be manually captured before integrations exist.

Definition of done:
- user can create, inspect and send a case pack without an OEM API;
- case timeline records the handoff.

Suggested split:
- backend message/pack object: Jono;
- handoff UX: Pierre.

---

### EPIC C11 — Commitments, Follow-up & Resolution

**Outcome:** KADARI remembers promises and closes the loop.

Scope:
- promised-response dates;
- appointments;
- waiting-for-provider;
- waiting-for-user;
- overdue detection;
- prepared follow-up;
- repair outcome;
- replacement;
- refund;
- credit;
- rejected case;
- no-fault-found;
- case closure;
- write-back to Digital Twin history.

Definition of done:
- a provider promise creates a due item;
- overdue case triggers attention;
- resolution updates coverage/history appropriately;
- replacement can create a related new Digital Twin while preserving old asset history.

Suggested split: Jono workflow + Pierre CX.

---

### EPIC C12 — Care Ops

**Outcome:** Human intervention exists only where automation cannot safely finish.

Queues:
- unknown brand/model;
- conflicting rule;
- stale rule;
- registration failure;
- deadline risk;
- unknown route;
- OEM non-response;
- rejection/escalation review;
- safety/manual review;
- source verification.

Every Ops resolution must be marked:
- reusable registry knowledge;
- case-specific/non-reusable;
- requires policy review;
- requires product bug fix.

Definition of done:
- operator can resolve an exception;
- resolution can create/update a registry rule with provenance;
- customer case can resume from manual intervention.

Suggested lead: Jono backend/admin surface; Pierre minimal ops UI if shared front-end stack.

---

### EPIC C13 — Registry Factory

**Outcome:** Registry can be continuously refreshed without code deploys.

Scope:
- source intake;
- source classification;
- change detection;
- pending review;
- rule proposal;
- approval;
- version publication;
- stale/expiry scheduling;
- service-centre status;
- brand/group mapping;
- retailer policy mapping.

Definition of done:
- authorised operator can update a rule without engineering intervention;
- previous version remains available;
- impacted future decisions use new version;
- historical decisions remain linked to old version.

Suggested lead: Jono.

---

### EPIC C14 — Analytics & Commercial Readiness

**Outcome:** Product usage produces operational and commercial evidence.

Minimum metrics:
- Care-active assets;
- coverage-found rate;
- registration opportunities;
- registration completion;
- extra cover activated;
- cases by brand/group;
- routes by type;
- time to first external response;
- resolution time;
- outcome mix;
- retailer returns;
- warranty repairs;
- rejection rate;
- manual interventions per 1,000 Care-active assets;
- unknown-route rate;
- stale-rule rate;
- brand/group volume thresholds for OEM activation.

Definition of done:
- metrics derive from production events rather than manual spreadsheets;
- no sensitive message/document content placed in general analytics payloads.

Suggested lead: Jono data; Pierre dashboards where required.

## 4. Dependency graph

Minimum dependency order:

C1 Foundation
→ C2 Registry Read
→ C4 Coverage
→ C5 Registration
→ C6 Reminders

C1 Foundation
→ C3 Extraction
→ C4 Coverage

C1 + C2 + C4
→ C7 Problem Intake
→ C8 Routing
→ C9 Case
→ C10 Communication
→ C11 Follow-up/Resolution

C2 + C9
→ C12 Care Ops
→ C13 Registry Factory

All production events
→ C14 Analytics

## 5. Parallelisation

After C1 and the minimum C2 schema exist:

Pierre can progress:
- Care card/overview;
- coverage detail;
- registration UX;
- problem intake;
- case timeline;
- resolution screens.

Jono can progress:
- registry schema/import;
- rule matcher;
- coverage engine;
- registration state machine;
- routing engine;
- generic case model;
- events/audit;
- reminders.

They should agree contracts early and avoid waiting for complete backend implementation before front-end state work begins.

## 6. P0 vertical slices

### Slice A — Hisense low-value retailer return

Goal:
receipt + serial + product → coverage → `Something's wrong` → retailer warranty-credit route → proof pack → case close.

Proves:
- category-specific routing;
- retailer return;
- no inappropriate repair-centre route.

### Slice B — LG/Samsung major appliance OEM route

Goal:
coverage → issue → OEM support route → prepared case pack → reference → follow-up → repair outcome.

Proves:
- OEM route;
- case timeline;
- response/commitment handling.

### Slice C — Bosch registration opportunity

Goal:
asset capture → eligible registration opportunity → customer authority → assisted/guided registration → confirmation stored → new coverage expiry.

Proves:
- extra-cover activation;
- authorisation;
- proof persistence.

### Slice D — Apple/Xiaomi authorised service route

Goal:
asset → issue → authorised service/collection route → external reference → resolution.

Proves:
- service-network routing;
- carry-in/collection variants.

### Slice E — Unknown marketplace brand

Goal:
asset → unresolved coverage/route → Care Ops → research → rule created or case-specific fallback → customer resumes flow.

Proves:
- safe unknown handling;
- knowledge compounding.

## 7. Security and privacy workstream

Must be completed across all epics, not at the end.

Requirements:
- tenant/owner permissions inherited from KADARI;
- explicit external-sharing authority;
- least-data proof packs;
- audit of material submissions;
- no passwords stored for OEM sites unless architecture explicitly supports secure credential vaulting later;
- OTP remains user-controlled unless a compliant delegated flow exists;
- analytics excludes raw sensitive evidence/content;
- Ops access is permissioned and auditable.

## 8. Idempotency and resilience workstream

Required on all external/action endpoints:
- idempotency key;
- retry-safe transitions;
- outbox/event reliability where existing KADARI architecture supports it;
- external provider error captured separately;
- user work persisted before external handoff;
- duplicate case/registration prevention.

## 9. Build sequencing recommendation

### Build Wave 1 — Foundation
C1, C2, minimum C3, C4.

Exit condition:
real asset shows reliable Care coverage result.

### Build Wave 2 — Activation
C5, C6.

Exit condition:
real registration opportunity can be detected, authorised, completed/assisted and evidenced.

### Build Wave 3 — Problem to route
C7, C8.

Exit condition:
real problem receives correct recommended next action.

### Build Wave 4 — Case lifecycle
C9, C10, C11.

Exit condition:
real external case can be opened, tracked, followed up and resolved.

### Build Wave 5 — Operational scaling
C12, C13, C14.

Exit condition:
unknowns can be resolved operationally and feed durable registry knowledge; analytics show brand/group traction.

## 10. Governance

If implementation conflicts with a previously locked authority, the authority wins unless founder decision explicitly changes it.

Developers should not silently simplify away:
- self-capture-first strategy;
- registry provenance/versioning;
- unknown-state honesty;
- explicit user authority;
- persistent case history;
- proof-before-partnership OEM strategy.

## 11. Final definition of execution success

The implementation programme succeeds when a normal customer can buy an asset, add it to existing KADARI, understand and improve its cover, later report a problem, receive the correct route, manage the external interaction, resolve it, and retain the complete evidence-backed history on the same Digital Twin — without KADARI depending on a retailer POS integration or OEM API.