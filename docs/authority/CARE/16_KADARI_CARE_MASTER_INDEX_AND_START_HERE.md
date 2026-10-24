# KADARI Care — Master Index & Start Here

**Status:** Final programme navigation authority  
**Date:** 2026-10-06

## 1. Purpose

This file is the entry point for all KADARI Care work.

KADARI Care is additive to the existing KADARI platform. It does **not** replace or re-platform the current Digital Twin, evidence, insurance/protection or asset-history foundation.

The working principle remains:

**One asset identity. One history. Multiple lifecycle capabilities.**

Care extends that existing asset record with warranty, maintenance, repair, registration, service routing, case handling and after-sales intelligence.

---

## 2. What is locked

The following product principles are locked unless the founder explicitly changes them:

1. **Consumer self-capture first.** KADARI Care must be useful before any retailer or OEM integrates.
2. **Integration is an optimisation layer, not a dependency.**
3. **Care is additive to the existing Digital Twin.** No second asset system.
4. **The customer should not need to understand the warranty ecosystem.** KADARI carries that complexity.
5. **One recommended next action.** Do not expose raw routing complexity to the user.
6. **Warranty is an active object, not a stored PDF.**
7. **KADARI should improve/activate coverage where possible.**
8. **AI interprets. Rules govern. Evidence proves. The customer authorises. KADARI remembers.**
9. **Rules are versioned and auditable.** Historical decisions retain the exact rule/version that produced them.
10. **External failure is not KADARI failure.** User work and evidence must remain safe.
11. **Retries may not create duplicate registrations or duplicate OEM cases.**
12. **Solve once. Learn permanently.** Manual exceptions should improve the registry where reusable.
13. **Proof before partnership. Participation before integration. Operational value before licence expansion.**
14. **Care can launch without OEM contracts.**
15. **No external party receives more asset/customer data than required for the authorised task.**

---

## 3. Read order

### Product and market foundation

1. `CARE/01_KADARI_CARE_PRODUCT_MARKET_AUTHORITY.md`
   - What Care is
   - acquisition principle
   - warranty activation doctrine
   - routing principle
   - Asset Case concept
   - OEM engagement sequence
   - registry architecture

2. `CARE/02_KADARI_CARE_OPERATING_MODEL.md`
   - Care operating model
   - how internal engines cooperate
   - lifecycle flow

3. `CARE/03_KADARI_CARE_V1_BUILD_SPEC.md`
   - V1 modules
   - build sequence
   - P0 brands
   - acceptance scenarios

### Customer experience

4. `CARE/04_KADARI_CARE_CUSTOMER_EXPERIENCE_AUTHORITY.md`
   - customer-facing doctrine
   - language and UX principles
   - journey design

5. `CARE/05_KADARI_CARE_SCREEN_AND_STATE_MAP.md`
   - concrete Care screens/states
   - CTAs
   - proof pack
   - reminders
   - case timeline
   - resolution states

### Intelligence and automation

6. `CARE/06_KADARI_CARE_INTELLIGENCE_AND_AUTOMATION_AUTHORITY.md`
   - extraction
   - rule matching
   - AI boundary
   - message drafting
   - escalation logic

7. `CARE/07_KADARI_CARE_AUTOMATION_STATE_MACHINE.md`
   - registration states
   - coverage states
   - routing states
   - case/communication states
   - retry/idempotency expectations

### Commercial and OEM model

8. `CARE/08_KADARI_CARE_COMMERCIAL_AND_OEM_AUTHORITY.md`
   - commercial ladder
   - consumer/transaction/OEM revenue
   - OEM Free / Pro / Enterprise

9. `CARE/09_KADARI_CARE_OEM_ACTIVATION_PLAYBOOK.md`
   - when to approach an OEM
   - proof thresholds
   - pilot sequence
   - brand-group leverage

### Registry and operations

10. `CARE/10_KADARI_CARE_REGISTRY_FACTORY_AUTHORITY.md`
    - source hierarchy
    - rule freshness
    - verification
    - registry learning loop

11. `CARE/11_KADARI_CARE_OPS_AUTHORITY.md`
    - human-review queues
    - exception handling
    - severity
    - SLA and QA principles

### Architecture and backend contract

12. `CARE/12_KADARI_CARE_DATA_MODEL_AUTHORITY.md`
    - core entities
    - relationships
    - enums
    - rule/version objects
    - audit/authority objects

13. `CARE/13_KADARI_CARE_API_AND_EVENT_CONTRACT.md`
    - API surface
    - domain events
    - idempotency expectations
    - Care Ops / registry interfaces

### Execution and release

14. `CARE/14_KADARI_CARE_IMPLEMENTATION_EXECUTION_PLAN.md`
    - epics
    - dependency order
    - ownership split
    - delivery workstreams

15. `CARE/15_KADARI_CARE_ACCEPTANCE_AND_RELEASE_GATES.md`
    - functional proof
    - architecture gates
    - safety/privacy/audit gates
    - release blockers
    - pilot proof

---

## 4. Research estate

The market registry and machine-searchable extracts live alongside the authorities in `CARE/`.

Use them as the working seed for the production registry, not as a static source of truth forever.

The production system must preserve:

- source;
- effective date where known;
- last verified date;
- confidence;
- version;
- supersession history.

Do not hard-code policy assumptions merely because they appeared in the initial workbook.

---

## 5. What Pierre/Jono should build first

### Phase 0 — Reconcile with existing KADARI

Before adding tables or endpoints:

- map existing asset/Digital Twin identifiers;
- map existing evidence/document storage;
- map existing audit/event infrastructure;
- map current insurance/protection capability;
- reuse equivalent platform primitives wherever possible.

Do **not** duplicate a platform capability just because a Care document uses a new conceptual name.

### Phase 1 — Care foundation

Build:

- Care profile attached to existing Digital Twin;
- registry read model;
- source/rule/version structures;
- coverage evaluation;
- warranty status card and Care overview;
- basic reminders.

Success proof:

A captured asset plus receipt/model/serial can produce a source-backed coverage position without OEM integration.

### Phase 2 — Warranty activation

Build:

- registration opportunity detection;
- explicit customer authority;
- Automatic / Assisted / Guided registration state machine;
- proof/result capture;
- idempotency protection;
- deadline reminders.

Success proof:

An eligible real product can move from capture to registered/extended cover with confirmation retained against the Digital Twin.

### Phase 3 — Something's wrong

Build:

- issue intake;
- safety gate;
- evidence readiness;
- routing engine;
- recommended next action;
- proof pack;
- customer-channel handoff;
- Asset Case creation.

Success proof:

A real product fault can be routed correctly without the customer knowing the internal OEM/distributor/service structure.

### Phase 4 — Case memory and follow-up

Build:

- case timeline;
- external references;
- replies;
- commitments;
- appointments;
- overdue follow-up;
- repair/replacement/refund/rejection outcomes;
- write-back to Digital Twin history.

Success proof:

The full resolution remains permanently attached to the same asset.

### Phase 5 — Registry Factory + Care Ops

Build:

- unknown-route queue;
- failed-registration queue;
- stale-rule queue;
- source review;
- rule authoring/versioning;
- human escalation;
- registry-learning feedback loop.

Success proof:

An unknown case can be manually resolved once and converted into reusable structured knowledge when appropriate.

---

## 6. Technical non-negotiables

- Reuse existing platform identity, auth, evidence and history primitives.
- Make state transitions explicit and auditable.
- Store exact rule-version references on material decisions.
- Use idempotency keys for external registration/case actions.
- Separate `external_failed` from `internal_failed`.
- Avoid silent AI-generated warranty facts.
- Preserve raw evidence underneath AI summaries.
- Never overwrite historical rule outcomes when policy changes later.
- Registry changes must be versioned, not silently mutated.
- Care API must be additive to existing KADARI API conventions.

---

## 7. Product non-negotiables

The customer experience should answer only five things:

1. **What cover do I have?**
2. **Can KADARI improve it?**
3. **What should I do now?**
4. **Who are we waiting for?**
5. **What happened in the end?**

The user should not have to understand:

- OEM vs distributor;
- warranty administrator;
- retailer-credit mechanics;
- service-network ownership;
- internal rule confidence;
- internal workflow enums.

That complexity belongs inside KADARI.

---

## 8. Release doctrine

Care is not complete because the UI looks finished.

It is complete only when real test journeys prove:

- correct coverage interpretation;
- correct registration behaviour;
- correct routing;
- safe communication;
- preserved evidence;
- no duplicate external actions;
- working follow-up;
- outcome write-back;
- registry refreshability;
- human escalation for uncertainty;
- no corruption of existing KADARI capabilities.

See `CARE/15_KADARI_CARE_ACCEPTANCE_AND_RELEASE_GATES.md` for release authority.

---

## 9. Governance

If implementation uncovers a genuine conflict between existing KADARI architecture and a Care authority:

1. do not silently choose a new product direction;
2. preserve the locked product outcome;
3. adapt implementation to existing primitives where possible;
4. document the conflict;
5. escalate only decisions that materially affect customer experience, commercial model, evidence integrity, permissions or product scope.

Engineering detail should not require founder re-approval where the product outcome remains unchanged.

---

## 10. Final statement

KADARI Care should make owning a physical product materially easier.

The customer captures the asset once.

From there KADARI should remember the proof, understand the cover, improve it where possible, tell the owner what matters, help when something goes wrong, carry the case to resolution and preserve the history for the rest of the asset's life.

**That is the system Pierre and Jono are now authorised to build.**