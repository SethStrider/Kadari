# KADARI Care — Codebase Reconciliation Status

**Status:** Blocked on runtime source access  
**Date:** 2026-10-06

## 1. Purpose

This note records the result of the first implementation reconciliation attempt after the KADARI Care product, UX, data, API, commercial, registry, operations and release authorities were completed.

The intended next step was to map the existing application source against Care using:

- **REUSE** — existing KADARI capability can be used as-is;
- **EXTEND** — existing KADARI capability should be expanded for Care;
- **NEW** — genuinely new capability is required.

## 2. Finding

The connected GitHub repository `RealMrGrey/KADARI` is currently a **product/governance repository**, not the runtime application source repository.

The repository contains authority, strategy, market, product, UX, integration and handover material, including the full `CARE/` authority set.

It does **not** currently expose application source artefacts needed for code-level reconciliation, such as:

- solution/project files;
- backend source code;
- frontend source code;
- database migrations;
- ORM/entity mappings;
- API controllers/routes/handlers;
- domain/application services;
- infrastructure adapters;
- runtime configuration;
- tests;
- deployment manifests tied to the KADARI application.

A search across the repositories visible to the connected GitHub account also did not reveal a second runtime KADARI repository.

## 3. Consequence

A truthful code-level `REUSE / EXTEND / NEW` reconciliation cannot yet be completed.

Do **not** infer or invent current classes, schemas, endpoints or architectural components from the governance documents.

The Care build authorities remain valid. What is missing is the runtime implementation context required to map those authorities onto the code that Pierre/Jono already built.

## 4. Required unblock

One of the following is required:

1. connect/provide access to the actual KADARI application source repository; or
2. import/push the existing KADARI application source into `RealMrGrey/KADARI`; or
3. provide a source archive/export if the runtime is not currently in GitHub.

Preferred outcome: keep `RealMrGrey/KADARI` as the canonical repository and place runtime source in an explicit application structure, unless Pierre/Jono have a strong technical reason to retain a separate runtime repository.

## 5. What the engineering team must provide once source is available

The first reconciliation response should contain:

### Existing architecture inventory
- frontend framework/app structure;
- backend/runtime stack;
- persistence/database stack;
- authentication/authorisation;
- asset/Digital Twin aggregate or equivalent;
- evidence/document storage;
- event/audit/history model;
- integrations/connectors;
- notifications/scheduling;
- background jobs/workers;
- deployment environments.

### REUSE / EXTEND / NEW matrix
For every major Care capability:
- Digital Twin Care profile;
- receipt/label extraction;
- Coverage Engine;
- Registration Engine;
- Reminder Engine;
- `Something's wrong` intake;
- Routing Engine;
- Asset Case Engine;
- communications/proof packs;
- commitments and follow-up;
- resolution write-back;
- Registry Factory;
- Care Ops;
- audit/authorisation;
- analytics/telemetry.

### Schema delta
List:
- existing entities/tables reused;
- existing entities extended;
- new entities required;
- migrations required;
- indexes/constraints/idempotency keys;
- versioning/audit impact.

### API delta
List:
- existing endpoints reused;
- endpoints extended;
- new Care endpoints required;
- existing event contracts reused;
- new domain events required.

### Frontend delta
Map the 29 Care states/screens against:
- existing Digital Twin surfaces;
- existing navigation;
- existing document/camera capture;
- existing activity/history surfaces;
- genuinely new Care components.

### First vertical slice
Recommend the smallest production-real slice that proves the architecture end-to-end.

Default preferred first slice unless the existing architecture suggests a better one:

**existing Digital Twin → receipt + serial evidence → Care coverage evaluation → warranty clock → registration opportunity → stored registration proof**

This proves attachment to the existing asset, registry matching, rule versioning, authorisation, evidence storage and reminders before case orchestration is added.

## 6. Founder decisions currently required

**None.**

The product direction is sufficiently defined for engineering reconciliation.

The missing input is implementation access, not another product decision.

If engineering uncovers a genuine conflict between the existing architecture and the Care authorities, it should be surfaced as a specific trade-off with recommended resolution rather than reopening the entire Care design.

## 7. Governance rule

Until runtime source is accessible:

> **Do not begin a parallel Care implementation based only on the authority documents if an existing KADARI application already exists.**

The objective remains to extend the existing KADARI product, preserve one Digital Twin/asset identity, and avoid duplicate backend or frontend architectures.

## 8. Next action

**Pierre/Jono: expose the actual KADARI runtime source to the canonical GitHub workflow, then perform the code-level reconciliation against CARE/12 through CARE/17 before implementation begins.**

Once source access exists, the next sprint becomes repository inspection and a concrete code-level reconciliation pack rather than additional conceptual product work.
