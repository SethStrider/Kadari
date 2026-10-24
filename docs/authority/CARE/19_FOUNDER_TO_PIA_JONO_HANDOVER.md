# KADARI Care — Founder Handover to Pia & Jono

**Status:** Final product handover
**Date:** 2026-10-06

## Purpose

This is the final founder handover for KADARI Care.

KADARI Care is now defined to the point where implementation should move to Pia and Jono.

The product direction is locked. Technical implementation choices belong to the engineering team, provided the locked product authorities and acceptance outcomes are preserved.

## What KADARI Care is

KADARI Care extends the existing KADARI Digital Twin / asset record through the life of the product.

The core journey is:

**CAPTURE → COVERAGE → ACTIVATE → REMIND → SOMETHING'S WRONG → ROUTE → CASE → RESOLVE → HISTORY**

The customer should not need to understand the warranty/service ecosystem.

The simplest product promise is:

> **I bought this.**
>
> Later:
>
> **Something's wrong.**
>
> KADARI carries as much of everything in between as safely and practically possible.

## Locked principles

1. **Do not rebuild KADARI.** Care is additive to the existing asset / Digital Twin system.
2. **One asset identity.** Care, protection, valuation, evidence, resale and history should attach to the same underlying asset.
3. **Consumer self-capture first.** KADARI must work without retailer POS or OEM integration.
4. **Integration is an optimisation layer, not a dependency.**
5. **AI interprets. Rules govern. Evidence proves. The customer authorises. KADARI remembers.**
6. **No guessing.** If warranty coverage, routing or registration cannot be determined confidently, fall back to review/research rather than inventing an answer.
7. **External failure must not erase customer work.**
8. **Retries must not create duplicate registrations or duplicate external cases.**
9. **Every material decision must be auditable and tied to the exact rule/source version that produced it.**
10. **Solve once. Learn permanently.** Manual exceptions should improve the Registry where reusable.

## What is already defined

The `CARE/` folder contains the complete product authority set covering:

- product and market authority;
- operating model;
- V1 scope;
- customer experience;
- screen/state map;
- intelligence and automation;
- automation state machines;
- commercial and OEM model;
- OEM activation playbook;
- Registry Factory;
- Care Ops;
- data model concepts;
- API/event contract concepts;
- implementation execution plan;
- acceptance and release gates;
- master index / start-here document.

The market registry under `CARE/REGISTRY/` is the initial South African research foundation.

## Engineering ownership

Pia and Jono own implementation decisions, including:

- schema design within the existing architecture;
- migrations;
- backend implementation;
- frontend implementation;
- service boundaries;
- queues/background processing;
- integration patterns;
- deployment;
- infrastructure;
- monitoring;
- technical testing;
- performance and security hardening.

Use existing KADARI architecture wherever it can satisfy the product requirements.

**REUSE first. EXTEND second. NEW only where necessary.**

Do not create a parallel asset system for Care.

## Founder decisions vs engineering decisions

### Founder/product decisions are locked

Do not reopen these unless implementation reveals a genuine contradiction or impossible trade-off:

- Care is additive;
- self-capture-first acquisition;
- no POS dependency;
- warranty activation modes: Automatic / Assisted / Guided;
- Coverage, Routing, Case and Reminder engines;
- `Something's wrong` as the primary issue entry point;
- generic Asset Case architecture rather than OEM-specific hard-coded case engines;
- Registry-backed routing and coverage logic;
- explicit customer authority for material external actions;
- Care Ops only where automation cannot safely complete;
- proof-before-partnership OEM strategy;
- free-to-paid OEM commercial ladder;
- historical decision integrity via rule/version retention.

### Engineering decisions belong to Pia/Jono

Choose the technical implementation that best fits the existing codebase and standards.

Do not seek founder approval for routine implementation details unless they materially change product behaviour, privacy, safety, commercial scope or the user journey.

## Start here

Read in this order:

1. `CARE/16_KADARI_CARE_MASTER_INDEX_AND_START_HERE.md`
2. `CARE/03_KADARI_CARE_V1_BUILD_SPEC.md`
3. `CARE/04_KADARI_CARE_CUSTOMER_EXPERIENCE_AUTHORITY.md`
4. `CARE/05_KADARI_CARE_SCREEN_AND_STATE_MAP.md`
5. `CARE/06_KADARI_CARE_INTELLIGENCE_AND_AUTOMATION_AUTHORITY.md`
6. `CARE/10_KADARI_CARE_REGISTRY_FACTORY_AUTHORITY.md`
7. `CARE/11_KADARI_CARE_OPS_AUTHORITY.md`
8. `CARE/12_KADARI_CARE_DATA_MODEL_AUTHORITY.md`
9. `CARE/13_KADARI_CARE_API_AND_EVENT_CONTRACT.md`
10. `CARE/14_KADARI_CARE_IMPLEMENTATION_EXECUTION_PLAN.md`
11. `CARE/15_KADARI_CARE_ACCEPTANCE_AND_RELEASE_GATES.md`

Use `CARE/01_...` and `CARE/02_...` for wider product context and market rationale.

## Recommended first implementation slice

Build one end-to-end vertical slice before broadening coverage:

1. existing asset / Digital Twin;
2. add receipt + model/serial evidence;
3. evaluate Care coverage from a Registry rule;
4. display warranty status;
5. detect a registration opportunity where applicable;
6. support Guided or Assisted activation;
7. store confirmation evidence;
8. trigger a meaningful reminder;
9. allow `Something's wrong`;
10. resolve one verified route;
11. open an Asset Case;
12. prepare customer-sent external communication;
13. capture an external reference;
14. track one commitment/follow-up;
15. record repair/replacement/credit outcome;
16. write the material outcome back to asset history.

Prove this before scaling breadth.

## Do not build yet unless required by the vertical slice

- full OEM API integrations;
- retailer POS integrations;
- broad white-label infrastructure;
- complex OEM enterprise dashboards;
- large repair marketplaces;
- speculative machine-learning reliability models;
- unnecessary new navigation or parallel account structures.

Those come after real usage proves where integration and commercial leverage matter.

## What to send back to James

Do not send back a new interpretation of KADARI Care.

Send back only:

1. implementation status;
2. material product conflicts discovered in the existing system;
3. founder decisions genuinely required;
4. risks that could change customer behaviour, safety, privacy or commercial scope;
5. proof that the agreed vertical slices pass.

Everything else is an engineering decision.

## Definition of handover complete

This handover is complete when Pia and Jono can begin implementation without needing James to restate the product, explain the intended flow, or choose ordinary technical architecture.

The product authorities are the source of truth.

Build toward the release gates, not toward a collection of screens.
