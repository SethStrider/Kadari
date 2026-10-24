# KADARI Care — Acceptance & Release Gates

**Status:** Release authority  
**Date:** 2026-10-06  
**Purpose:** Define the proof required before KADARI Care can move from build to pilot and from pilot to general release.

## 1. Release principle

Care is not complete because screens render or APIs respond.

Care is complete when real-world customer journeys work correctly against evidence-backed rules and failure states remain safe, honest and recoverable.

## 2. Gate structure

Release is controlled through five gates:

- **G0 — Architecture integrity**
- **G1 — Functional vertical slices**
- **G2 — Safety, privacy and auditability**
- **G3 — Operational readiness**
- **G4 — Pilot performance**

No later gate waives an earlier one.

## 3. G0 — Architecture integrity

Pass only if:
- Care attaches to existing Digital Twin identity;
- no duplicate asset system exists;
- registry rules are versioned and source-backed;
- material decisions retain rule-version reference;
- all external actions support idempotency/retry safety;
- material outcomes write back to asset history;
- Care events integrate with existing backend/event conventions rather than creating parallel infrastructure unnecessarily.

Fail examples:
- warranty dates hard-coded in UI;
- service-centre contact copied into front-end constants;
- new Care asset ID replacing existing KADARI asset ID;
- historical case route changes after registry update because old rule version was not retained.

## 4. G1 — Functional vertical slices

All five P0 slices from the execution plan must pass.

### A — Low-value retailer-return journey

Pass criteria:
- product identity captured;
- receipt stored;
- coverage determined or clearly qualified;
- route returns customer to retailer where rule requires it;
- no service-centre route incorrectly displayed;
- proof pack available;
- case can be closed with refund/credit/replacement outcome.

### B — Major appliance OEM journey

Pass criteria:
- warranty state shown;
- issue captured;
- evidence reused from existing Digital Twin;
- correct OEM route selected;
- case pack generated;
- external reference recorded;
- promised response date can be tracked;
- overdue follow-up generated;
- repair outcome writes to asset history.

### C — Warranty-extension registration journey

Pass criteria:
- opportunity detected from registry rule;
- deadline shown;
- user authority captured;
- Assisted or Guided flow works;
- failed external attempt does not produce false success;
- confirmation/reference stored;
- updated expiry shown;
- reminder schedule recalculated.

### D — Authorised service / collection journey

Pass criteria:
- correct service network selected;
- route reflects carry-in vs collection correctly;
- provider/channel displayed from registry data;
- drop-off/collection event can be recorded;
- resolution retained.

### E — Unknown-marketplace-brand journey

Pass criteria:
- system does not guess;
- uncertainty shown plainly;
- case is preserved;
- Ops queue receives work item;
- operator can resolve route;
- reusable knowledge can create new registry rule/version;
- customer resumes flow without recreating case.

## 5. G2 — Safety, privacy and auditability

Pass only if:
- dangerous issue classes can interrupt normal troubleshooting;
- KADARI does not encourage unsafe manipulation of electrical/gas/battery products;
- outbound proof pack shows user exactly what will be shared;
- external party receives minimum necessary data;
- all user authorisations are timestamped and auditable;
- registrations cannot be submitted without authority;
- OEM credentials are not stored insecurely;
- raw warranty docs/messages are not copied into normal analytics payloads;
- Ops access is role-controlled;
- audit trail identifies actor, action, rule/source and result for material decisions.

## 6. G3 — Operational readiness

Before pilot:
- Registry Factory can publish new rule versions without code deployment;
- stale-rule queue exists;
- failed-registration queue exists;
- unknown-route queue exists;
- case escalation queue exists;
- manual intervention can return case to normal workflow;
- Care Ops runbook exists;
- source-verification process exists;
- no critical unresolved P0 brand rule gaps remain for pilot categories;
- customer support can identify whether issue is KADARI failure or external-provider failure.

## 7. Required automated test classes

### Unit tests
- date calculations;
- rule specificity ordering;
- coverage expiry;
- registration deadline;
- reminder scheduling;
- route precedence;
- state transitions;
- idempotency handling;
- stale-rule detection.

### Integration tests
- asset → Care profile;
- registry import → rule resolution;
- extraction → confirmed identity;
- coverage → registration opportunity;
- problem → route;
- route → case;
- case → message handoff;
- case → resolution → asset history;
- Ops resolution → new rule version.

### Regression tests
Every production registry update affecting P0 brands should rerun representative rule scenarios.

## 8. Failure-mode tests

Must explicitly test:
- OEM site unavailable;
- email/WhatsApp handoff abandoned;
- duplicate submit/retry;
- receipt extraction wrong;
- serial extraction wrong;
- purchase date missing;
- conflicting warranty documents;
- stale registry rule;
- service centre closed/invalid;
- product category misclassified;
- customer edits AI-derived data;
- registration deadline already expired;
- provider rejects warranty;
- promised response passes with no reply;
- replacement creates new asset;
- customer withdraws case.

Expected principle:
**No failure may erase the customer's work or create false certainty.**

## 9. Pilot cohort

Initial pilot should favour a controlled group with real mixed assets rather than one brand-only cohort.

Recommended categories:
- major appliance;
- small appliance;
- TV/electronics;
- phone/computer;
- power tool or portable power.

Recommended route diversity:
- retailer return;
- OEM direct;
- authorised service;
- collection/courier;
- registration extension;
- unknown/fallback.

## 10. Pilot metrics

Minimum measures:
- % assets with coverage successfully resolved;
- % registration opportunities correctly identified;
- % registrations completed;
- unknown-route rate;
- incorrect-route rate;
- manual interventions per 1,000 Care-active assets;
- case completion rate;
- median time to first external response;
- median time to resolution;
- reminder action rate;
- duplicate action incidents;
- false-success incidents;
- customer correction rate on extracted fields;
- customer abandonment by step;
- Ops resolution time;
- registry rule defects found in production.

## 11. Hard pilot stop conditions

Pause expansion if any of these occur materially/repeatedly:
- KADARI claims coverage that does not exist;
- KADARI misses active coverage because of rule defects;
- incorrect route materially disadvantages customer;
- registration falsely shown as complete;
- duplicate external case/registration creation;
- safety guidance materially unsafe;
- customer data shared beyond authorised scope;
- rule update corrupts historical decisions.

## 12. G4 — Pilot performance threshold

General release requires evidence that:
- rule resolution is reliable for P0 categories;
- incorrect-route rate is low enough to be operationally acceptable;
- unknown cases are safely escalated;
- manual intervention is declining as registry knowledge grows;
- customers can complete the core journey without support assistance in most cases;
- no unresolved critical privacy/safety/audit issues remain;
- product creates clear user value, including prevented replacement, activated cover, resolved repair, refund/credit, or simpler after-sales handling.

Do not define success only by engagement or number of notifications opened.

## 13. Brand-group readiness gate

Before approaching an OEM/brand group commercially, produce a standard evidence pack containing:
- registered asset count;
- active owners where permissioned/aggregated;
- cases opened;
- route types;
- response time;
- resolution time;
- evidence completeness;
- repeated friction points;
- warranty-registration volume;
- manual workload currently imposed on public support channels;
- potential savings/automation opportunities.

The commercial approach should be triggered by evidence, not by an arbitrary calendar date.

## 14. Retailer readiness gate

Before approaching a retailer, show:
- number of assets bought from retailer;
- after-sales cases tied to retailer purchases;
- return/credit volume;
- proof-of-purchase completeness;
- replacement opportunities;
- warranty activation opportunities;
- potential reduction in customer support friction.

## 15. Production observability

Required dashboards/alerts:
- coverage-resolution errors;
- registration failures;
- routing unknowns;
- route overrides;
- duplicate-prevention triggers;
- external channel failures;
- stale P0 rules;
- Ops queue age;
- overdue customer/provider commitments;
- critical safety cases;
- event-processing failures.

## 16. Handover checklist for Pierre/Jono

Before implementation starts, both should confirm:
- existing KADARI entities that Care will reuse;
- any naming conflicts with proposed Care entities;
- existing event/outbox/CQRS conventions;
- existing document/evidence storage APIs;
- existing notification infrastructure;
- existing auth/tenant/permission model;
- existing AI/extraction capability that can be reused;
- any architectural equivalent that makes a proposed new component unnecessary.

Where an existing KADARI component already satisfies the requirement, reuse it and document the mapping rather than duplicating it.

## 17. Founder validation points

Return for founder/product decision only where implementation requires changing a locked product principle, including:
- changing self-capture-first strategy;
- requiring retailer/OEM integration for core use;
- removing user authorisation before submission;
- collapsing unknown into guessed route;
- replacing persistent asset history with ephemeral case data;
- materially changing Care commercial boundary.

Normal technical implementation decisions do not require founder escalation.

## 18. Final release definition

KADARI Care is release-ready when a customer can use a real purchased product through the full lifecycle:

**capture → understand cover → activate additional cover where available → receive useful reminder → report fault → receive correct next action → communicate → track → follow up → resolve → retain permanent history**

and the product remains safe, honest and recoverable when any external participant fails.