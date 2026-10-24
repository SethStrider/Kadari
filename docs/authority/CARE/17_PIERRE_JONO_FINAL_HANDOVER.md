# KADARI Care — Final Pierre & Jono Handover

**Date:** 2026-10-06  
**Audience:** Pierre McCarthy, Jonathan McCarthy  
**Status:** Execution handover

## 1. What this handover means

The KADARI Care product thinking is now sufficiently defined for implementation.

This is not permission to redesign KADARI or revisit foundational product choices. It is permission to implement Care inside the existing KADARI architecture using the authorities in `CARE/`.

Start with:

`CARE/16_KADARI_CARE_MASTER_INDEX_AND_START_HERE.md`

Then follow the read order in that file.

---

## 2. What is already decided

Do not reopen these without a material implementation blocker:

- Care is additive to the existing KADARI Digital Twin.
- Consumer self-capture is the initial acquisition mechanism.
- POS/OEM integrations are not required for V1.
- Warranty and service knowledge comes from a versioned Care Registry.
- KADARI should detect warranty-registration/extension opportunities.
- Customer authority is required for external submissions/actions.
- Registration supports Automatic, Assisted and Guided modes.
- `Something's wrong` is the primary fault entry point.
- Routing is based on asset/product/retailer/coverage/issue context.
- Cases are generic Asset Cases, not a hard-coded workflow per brand.
- Customer-facing complexity must remain minimal.
- AI may interpret and draft, but registry evidence/rules govern entitlement and routing.
- Every material decision is auditable and tied to a rule version.
- External failures do not erase work or imply KADARI failure.
- Care Ops handles exceptions and feeds reusable knowledge back into the Registry.
- OEM participation starts with proof/volume; enterprise integration is earned later.

---

## 3. First engineering task

Before writing new Care infrastructure, perform an **existing-KADARI reconciliation**.

Map each Care concept to existing primitives:

- Digital Twin / asset identity;
- tenant/user/role model;
- evidence/document/media storage;
- audit/event trail;
- notification/reminder infrastructure;
- integration/outbox/message bus if present;
- current insurance/protection case objects;
- existing API conventions;
- current front-end component/navigation conventions.

Produce a short reconciliation note showing:

- `REUSE`
- `EXTEND`
- `NEW`

for every major Care entity/capability.

The objective is **minimum architectural duplication**.

---

## 4. Recommended ownership split

This is a recommended working split, not a rigid personnel rule.

### Pierre — product/application implementation lead

Likely emphasis:

- Care UI surfaces;
- Digital Twin Care integration;
- customer journeys;
- capture/evidence handoff;
- registration experience;
- Something's wrong flow;
- proof pack;
- case timeline;
- Care Ops/admin surfaces where appropriate.

### Jono — backend/domain/integration lead

Likely emphasis:

- registry schema/read layer;
- coverage/routing engines;
- rule versioning;
- case domain model;
- API/event contracts;
- idempotent external actions;
- background reminders/commitments;
- integration adapters;
- auditability and operational telemetry.

Both should reconcile this split against the actual existing codebase and skill ownership.

---

## 5. Build order

Do not start with OEM API integrations.

### Build 1 — Foundation

Deliver:

- Care attached to Digital Twin;
- registry seed import;
- rule/source/version support;
- coverage evaluator;
- coverage card/overview;
- reminder base.

### Build 2 — Activation

Deliver:

- registration opportunity detection;
- customer authority;
- Automatic/Assisted/Guided registration flow;
- confirmation evidence;
- deadline handling;
- idempotency.

### Build 3 — Resolution

Deliver:

- Something's wrong intake;
- evidence readiness;
- safety interruption;
- routing engine;
- one recommended next action;
- proof pack;
- customer WhatsApp/email/web handoff;
- Asset Case.

### Build 4 — Case lifecycle

Deliver:

- timeline;
- external references;
- provider responses;
- commitments;
- appointments;
- follow-up;
- outcomes;
- asset-history write-back.

### Build 5 — Operations

Deliver:

- Registry Factory management;
- Care Ops queues;
- stale rules;
- failed registrations;
- unknown routes;
- manual review;
- QA and telemetry.

---

## 6. Test against route archetypes, not only brands

Use P0 brands from the registry, but prove the architecture across different operational models:

1. retailer return/credit;
2. OEM direct support;
3. authorised service centre;
4. onsite large-appliance service;
5. carry-in workshop;
6. collection/courier;
7. registration-dependent extended cover;
8. unknown-route/manual research fallback.

The objective is not to hard-code Samsung, Bosch or Hisense flows.

The objective is to prove a generic engine can correctly support them.

---

## 7. What not to build yet

Unless required by the existing architecture, do not make V1 dependent on:

- retailer POS integrations;
- direct OEM APIs;
- full OEM CRM replacement;
- large repair marketplace;
- advanced reliability scoring;
- predictive failure claims;
- automatic legal conclusions;
- global multi-country registry depth;
- complex consumer subscription redesign.

The V1 proof is simpler:

**Can a real South African customer capture a product, understand/activate cover, report a problem, be routed correctly, resolve it and retain the full history without retailer/OEM integration?**

If yes, the thesis is proven.

---

## 8. When you may make implementation decisions without James

You may independently decide:

- table names;
- indexes;
- internal DTO shapes;
- caching;
- queue/message technology usage;
- component composition;
- code organisation;
- internal service boundaries;
- implementation-level naming;
- test framework/detail;

provided the locked product behaviour and evidence/audit requirements remain intact.

Escalate only if a decision materially changes:

- customer-facing behaviour;
- permissions/data exposure;
- warranty/coverage certainty;
- external authority/consent;
- commercial model;
- asset/evidence integrity;
- Care scope;
- existing KADARI product direction.

---

## 9. Definition of a good implementation

A good Care implementation feels native to KADARI.

It should not feel like a warranty app bolted onto an asset platform.

The same Digital Twin captured for evidence, insurance, valuation or resale should simply become more useful over time because Care now knows:

- what cover exists;
- what action is due;
- what happened when something failed;
- who was contacted;
- what was repaired/replaced;
- what it cost;
- what proof exists.

---

## 10. Immediate deliverable expected from engineering

Return with a **Care Build Reconciliation Pack** containing:

1. existing architecture map relevant to Care;
2. REUSE / EXTEND / NEW table;
3. proposed first implementation PR/workstream;
4. schema/API deltas from the authority files if any;
5. known technical blockers;
6. anything that genuinely requires founder decision.

Do not respond with a new product concept.

Respond with how the locked Care product maps onto the code that already exists.

---

## 11. Final mandate

Build KADARI Care so that the owner does not need to understand the fragmented after-sales world behind their product.

The owner should be able to say:

> **I bought this.**

and later:

> **Something's wrong.**

KADARI should carry as much of everything in between as safely and practically possible.

That is the handover.