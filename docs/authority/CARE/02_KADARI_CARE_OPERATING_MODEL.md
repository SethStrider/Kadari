# KADARI Care — Operating Model

**Status:** Founder direction for implementation planning  
**Date:** 2026-10-06  
**Depends on:** `PRODUCT/01_PRODUCT_AUTHORITY.md`, `PRODUCT/02_CAPTURE_EVIDENCE_STANDARD.md`, `CARE/01_KADARI_CARE_PRODUCT_MARKET_AUTHORITY.md`

## 1. Purpose

KADARI Care is the active lifecycle layer on top of the existing KADARI Digital Twin.

KADARI already records what an asset is, its evidence, value, condition, documents and history. Care adds the ability to understand what protection applies, what action is required, who should act, and what happened afterwards.

Care must make the customer's experience simple:

> **Tell KADARI what you bought and what happened. KADARI works out what to do next.**

Care is not a separate app and does not replace the current insurance / protection proposition.

## 2. Product architecture

KADARI Care adds five connected capabilities to the Digital Twin:

1. **Coverage** — what warranty, guarantee, service plan or statutory route may apply.
2. **Activation** — whether additional cover can be unlocked or registered.
3. **Attention** — reminders, deadlines, maintenance and expiring rights.
4. **Resolution** — what to do when something goes wrong.
5. **Memory** — permanent case, communication, repair and outcome history.

The customer-facing language should remain human. Internal complexity stays behind the product.

## 3. Care state on every asset

Every eligible Digital Twin may have a Care state:

- `NOT_ASSESSED`
- `COVERAGE_UNKNOWN`
- `COVERAGE_FOUND`
- `ACTION_REQUIRED`
- `COVERED_ACTIVE`
- `COVERAGE_EXPIRING`
- `OUT_OF_COVERAGE`
- `OPEN_CASE`
- `MAINTENANCE_DUE`
- `RECALL_ACTION`
- `CARE_COMPLETE`

Multiple states may coexist. Example: an asset may be `COVERED_ACTIVE` and `OPEN_CASE`.

## 4. Capture-to-Care flow

### 4.1 Entry

Care begins from the existing camera-first capture flow.

Recommended evidence:

- product overview;
- model / rating plate;
- serial number;
- receipt / invoice;
- box / barcode where useful;
- installation certificate where relevant;
- warranty / guarantee document if supplied.

Capture must remain progressive and non-blocking.

### 4.2 Extraction

KADARI should attempt to extract or infer:

- brand;
- model;
- serial / IMEI / unique identifier;
- category;
- retailer;
- purchase date;
- purchase price;
- GTIN / barcode where available;
- installer where relevant;
- warranty language on the document;
- confidence for each extracted fact.

AI inference is never treated as verified fact without evidence.

### 4.3 Registry match

The Care Registry attempts to resolve:

- consumer brand;
- parent / group;
- SA administrator / distributor;
- product family;
- warranty rule;
- registration opportunity;
- retailer route;
- service route;
- known channels;
- source freshness.

If confidence is insufficient, Care records `COVERAGE_UNKNOWN` and falls back to verified official support / research-on-demand rather than guessing.

## 5. Coverage Engine

### 5.1 Inputs

- asset identity;
- category / model;
- retailer;
- purchase date;
- purchase price where relevant;
- country;
- existing registrations;
- uploaded terms;
- Care Registry rules;
- third-party protection records if present.

### 5.2 Outputs

A structured list of coverage components, for example:

- manufacturer warranty;
- retailer guarantee / return route;
- registered extension;
- extended service plan;
- insurer / protection product;
- potentially relevant statutory route.

Each component stores:

- provider;
- type;
- start date;
- end date;
- status;
- registration requirement;
- proof required;
- important exclusions / conditions;
- original source / terms;
- plain-language explanation;
- confidence;
- last verified date.

### 5.3 Plain-language coverage

KADARI should present:

**You are covered for**  
**You are probably not covered for**  
**Things that can affect the claim**  
**What you need if something goes wrong**  
**Important dates**

The original terms must remain available.

KADARI should explain, not silently reinterpret or replace, the legal / contractual terms.

## 6. Warranty Activation Engine

### 6.1 Trigger

When registry rules show that registration can activate or improve coverage, Care creates an activation opportunity.

Example customer experience:

> **Extra cover available**  
> This product may qualify for additional warranty protection if registered before 30 November.  
> **Authorise KADARI to activate it**

### 6.2 Authority

The customer provides explicit, transaction-specific authority.

Store:

- authority timestamp;
- requested action;
- provider;
- data authorised for submission;
- terms / consent reference where relevant;
- result.

### 6.3 Execution modes

**Automatic**  
KADARI submits the registration where technically and contractually permitted.

**Assisted**  
KADARI pre-fills / prepares the registration; the customer performs OTP, login, final consent or final submit.

**Guided**  
KADARI takes the user through the official flow where automation is unavailable or inappropriate.

### 6.4 Success proof

On completion, attach:

- provider registration reference;
- date/time;
- confirmation email / PDF / screenshot where available;
- terms applicable;
- effective expiry date;
- source;
- method used;
- registration status.

A successful registration becomes part of the permanent Digital Twin evidence history.

## 7. Warranty Clock and Reminder Engine

Care should create reminders from actual events and deadlines, not generic marketing cadence.

Possible triggers:

- product-registration deadline;
- warranty extension deadline;
- 50% of warranty elapsed;
- 90 days to expiry;
- 30 days to expiry;
- pre-expiry health check;
- maintenance interval;
- service interval;
- unresolved case deadline;
- OEM promised-response date;
- part / technician follow-up;
- recall / safety action.

### 7.1 Attention score

Do not spam every low-value asset.

Suggested internal prioritisation:

`Care Attention = Value × Failure/Consequence Risk × Time Sensitivity × Coverage Opportunity`

This score is not a legal or actuarial metric. It determines user-engagement priority.

### 7.2 Pre-expiry health check

For suitable assets, Care should ask category-specific questions before warranty expiry.

Examples:

- unusual noises;
- intermittent failures;
- leaks;
- error codes;
- display / button issues;
- degraded battery / capacity;
- cooling / heating performance;
- connectivity issues.

If a problem is identified, Care can open an Asset Case while coverage is still active.

## 8. Problem Resolution flow

Primary CTA:

> **Something's wrong**

The customer should not need to choose between retailer, manufacturer, distributor or repairer.

### 8.1 Minimum intake

Care asks only what is required:

- what happened;
- when it started;
- whether the asset still works;
- photos / video / audio if useful;
- error code;
- safety risk;
- accidental / physical damage where relevant.

Existing asset information is reused automatically.

Never ask again for data already held unless confirmation is required.

### 8.2 Safety gate

For hazards such as smoke, fire, electrical shock, gas leakage, overheating or dangerous mechanical failure, Care must prioritise safe shutdown / emergency advice and avoid ordinary troubleshooting that could increase risk.

## 9. Routing Engine

### 9.1 Routing equation

`Brand + Product/Model + Retailer + Purchase Date + Coverage + Issue + Geography = Best Next Action`

### 9.2 Supported route types

- retailer return;
- retailer warranty-credit / replacement;
- OEM direct case;
- local distributor / administrator;
- authorised service centre;
- onsite technician;
- workshop / carry-in;
- collection / courier;
- extended-protection provider;
- insurer / protection route;
- out-of-warranty paid repair;
- replacement / economic-repair decision;
- unknown-route research fallback.

### 9.3 Route record

Every recommendation must store:

- rule used;
- registry source;
- confidence;
- last verified date;
- recommended counterparty;
- required evidence;
- required channel;
- alternative route if available.

## 10. Asset Case Engine

Every lifecycle problem is represented by an Asset Case.

### 10.1 Case types

- WARRANTY
- RETURN
- REPAIR
- MAINTENANCE
- RECALL
- INSURANCE
- REPLACEMENT
- TRANSFER
- OTHER

### 10.2 Case statuses

- DRAFT
- READY_TO_SEND
- SUBMITTED
- AWAITING_COUNTERPARTY
- AWAITING_CUSTOMER
- APPOINTMENT_BOOKED
- IN_ASSESSMENT
- REPAIR_IN_PROGRESS
- PARTS_WAITING
- APPROVED
- REJECTED
- CREDIT_OFFERED
- REPLACEMENT_OFFERED
- RESOLVED
- CLOSED
- ESCALATED

### 10.3 Case record

Each case stores:

- case ID;
- asset ID;
- owner / authorised user;
- case type;
- counterparty;
- issue summary;
- structured symptoms;
- evidence attachments;
- coverage basis;
- routing decision;
- messages;
- external reference numbers;
- commitments / promised dates;
- appointments;
- technician / service centre;
- parts;
- cost;
- customer contribution;
- outcome;
- closure evidence;
- satisfaction / result;
- timeline.

## 11. Communication Rail

Care should maintain one coherent case history even when communications happen across external channels.

### 11.1 Initial channel modes

**Customer-send**  
KADARI prepares WhatsApp / email / other correspondence and attachments; customer sends from their own account.

**KADARI-send under authority**  
KADARI sends through an approved channel after explicit authority.

**Reference capture**  
Where the interaction happens by telephone or external portal, the customer or KADARI records reference numbers, promises and notes.

### 11.2 Message pack

A Care message pack can include:

- customer identity / contact data permitted for the case;
- product identity;
- serial number;
- purchase date;
- retailer;
- proof of purchase;
- warranty registration proof;
- issue description;
- supporting media;
- requested outcome;
- KADARI case reference.

### 11.3 Timeline

All outbound/inbound events should appear in one ordered timeline.

The Digital Twin history records the final material outcome, while detailed correspondence remains in the linked Case.

## 12. OEM / Counterparty progression

### Level 0 — Public channel
Care uses the provider's existing customer channels.

### Level 1 — Recognised KADARI case pack
Cases carry a consistent KADARI reference / evidence pack.

### Level 2 — KADARI Counterparty Inbox
OEM/distributor/retailer receives or accesses structured cases in one place.

### Level 3 — KADARI Care Connect
Counterparty staff can respond, request evidence, appoint service providers and close cases inside a lightweight dashboard.

### Level 4 — Direct integration
API/webhook/CRM/service-platform integration where volume and economics justify it.

Do not require Level 4 to deliver customer value.

## 13. Care Connect minimum counterparty dashboard

When introduced, the OEM / distributor / retailer dashboard should initially show only what is needed to resolve cases:

- new cases;
- awaiting response;
- awaiting customer;
- booked service;
- overdue cases;
- resolved cases;
- asset / model / serial;
- coverage evidence;
- attachments;
- conversation;
- resolution action.

Later paid capabilities may include:

- workflow automation;
- routing rules;
- teams / permissions;
- SLA analytics;
- recall targeting;
- bulk communications;
- failure intelligence;
- API / webhook;
- service-centre management.

## 14. Registry as production infrastructure

The Care Registry is not a static research spreadsheet.

Production entities should include:

- BrandGroup
- Brand
- CountryOperator
- Distributor
- WarrantyAdministrator
- ServiceNetwork
- Retailer
- ProductFamily
- ProductModel
- CoverageRule
- RegistrationRule
- RoutingRule
- ContactChannel
- ServiceCentre
- SourceEvidence
- RuleVersion

Rules require effective dating and source lineage.

## 15. Rule confidence

Recommended confidence states:

- `OFFICIAL_CURRENT` — current official source.
- `OFFICIAL_STALE` — official source but past refresh threshold.
- `CORROBORATED` — multiple reliable sources agree.
- `OBSERVED` — based on real cases but not yet confirmed by official rule.
- `UNKNOWN` — insufficient confidence.

Routing must prefer current official rules.

## 16. Data and consent boundary

Care must separate:

- KADARI account consent;
- transaction-specific authority;
- OEM data sharing;
- retailer data sharing;
- service-provider data sharing;
- marketing consent.

Do not use a warranty/service interaction as blanket marketing permission.

Only the minimum necessary data should be exposed to a counterparty for the relevant case.

## 17. User surfaces

Care should be visible inside the existing Digital Twin rather than as a separate app.

Recommended asset navigation:

- OVERVIEW
- PROTECT
- CARE
- DOCUMENTS
- HISTORY

### CARE surface

**Coverage**  
Active cover, expiry, registration opportunity.

**Attention**  
Deadlines, maintenance, expiring protection.

**Get help**  
Primary `Something's wrong` action.

**Cases**  
Open and previous cases.

**Product help**  
Manuals, official support, recall / safety information where available.

## 18. Consumer outcome metrics

Care should eventually measure:

- warranty value activated;
- replacement spend avoided;
- repair cost avoided;
- cases successfully resolved;
- time to resolution;
- assets with coverage captured;
- assets with complete evidence;
- expired coverage avoided through early action.

Possible user-facing value statement:

> **KADARI has helped protect / recover R_____ of value across your assets.**

Do not overclaim savings where causation is uncertain.

## 19. Business intelligence generated

At scale, aggregated and appropriately governed Care data can produce:

- failure incidence by model / category;
- time to first failure;
- recurring symptom / component patterns;
- warranty acceptance / rejection rates;
- service response time;
- repair completion time;
- parts delays;
- repair versus replacement outcomes;
- customer effort;
- asset life extension;
- replacement timing.

Identifiable household / customer inventories are not a data product.

## 20. Non-goals for first release

Do not make V1 dependent on:

- retailer POS integrations;
- OEM APIs;
- KADARI underwriting warranties;
- a national repair marketplace;
- automated consumer-law adjudication;
- perfect model-level coverage of every brand;
- complex AI diagnostics;
- replacement commerce.

Those can follow once the core Care loop is proven.

## 21. Core loop

The core KADARI Care loop is:

**CAPTURE → UNDERSTAND COVERAGE → ACTIVATE → REMIND → REPORT PROBLEM → ROUTE → CASE → RESOLVE → WRITE BACK TO HISTORY**

If this loop works reliably, KADARI becomes useful repeatedly throughout the asset's life while preserving the existing KADARI foundation.