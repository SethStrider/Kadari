# KADARI Care — V1 Build Specification

**Status:** Build-ready product specification  
**Date:** 2026-10-06  
**Objective:** Add KADARI Care to the existing KADARI product without rebuilding or replacing the insurance / asset foundation.

## 1. V1 outcome

A user can capture a newly purchased product, understand its warranty position, activate additional warranty where possible, receive useful reminders, report a problem, be routed to the correct counterparty, and retain the entire resolution history against the asset.

V1 must work without retailer or OEM integrations.

## 2. V1 product modules

### Module A — Care profile on Digital Twin

Add a `Care` area to eligible assets.

Minimum fields:

- care eligibility;
- coverage status;
- warranty start date;
- warranty end date;
- provider / administrator;
- registration requirement;
- registration deadline;
- registration status;
- extended-cover status;
- next attention date;
- open-case count.

### Module B — Coverage Engine V1

Inputs:

- brand;
- model/category;
- retailer;
- purchase date;
- purchase evidence;
- registry rule.

Outputs:

- coverage found / unknown;
- likely warranty period;
- expiry date;
- registration opportunity;
- retailer-route flag;
- plain-language summary;
- confidence / source.

V1 rule resolution should favour exact rule matches in this order:

1. model;
2. product family;
3. brand/category;
4. brand generic;
5. retailer/category;
6. fallback unknown.

### Module C — Warranty registration V1

Support three execution modes:

- AUTOMATIC;
- ASSISTED;
- GUIDED.

Minimum workflow:

1. detect opportunity;
2. show value / deadline;
3. request explicit user authorisation;
4. assemble required data;
5. execute selected mode;
6. record result;
7. attach proof;
8. update coverage dates;
9. schedule reminders.

### Module D — Reminder Engine V1

Supported reminders:

- registration deadline;
- 90-day warranty expiry;
- 30-day warranty expiry;
- pre-expiry health check;
- maintenance due where a verified rule exists;
- open-case follow-up;
- promised response overdue.

V1 should avoid blanket quarterly reminders for every asset. Quarterly reminders may be enabled where asset value / risk justifies it.

### Module E — `Something's wrong`

Primary entry point from Care.

Minimum intake:

- short problem description;
- selectable symptoms where known;
- photo;
- video;
- audio;
- error code;
- `Is this unsafe?` signal;
- accidental / physical damage flag where relevant.

The system reuses existing asset, receipt, serial and warranty evidence.

### Module F — Routing Engine V1

Supported routing targets:

- original retailer;
- OEM customer service;
- distributor / warranty administrator;
- authorised service centre;
- onsite service;
- carry-in workshop;
- collection/courier;
- extended-protection provider;
- unknown / research fallback.

Routing output must show:

- recommended action;
- why;
- what to take / attach;
- channel / contact;
- source / confidence internally.

### Module G — Asset Case Engine V1

Create a generic Case object.

Minimum case fields:

- id;
- assetId;
- ownerId;
- caseType;
- title;
- issueSummary;
- counterpartyType;
- counterpartyId / freeform fallback;
- externalReference;
- coverageRecordId;
- routingRuleId;
- status;
- openedAt;
- nextActionAt;
- closedAt;
- outcomeType;
- outcomeNotes.

Related child records:

- CaseMessage;
- CaseAttachment;
- CaseEvent;
- CaseAppointment;
- CaseCommitment;
- RepairOutcome.

### Module H — Communication V1

V1 should support:

1. **Prepare message** — KADARI drafts a structured message / case pack.
2. **Share via customer channel** — WhatsApp/email handoff where available.
3. **Mark sent / capture reference**.
4. **Record reply manually or from supported inbound channel**.
5. **Maintain timeline**.

No direct OEM API is required for V1.

### Module I — Care Registry read layer

V1 should ingest / represent the research registry as structured data rather than depend on the spreadsheet at runtime.

Minimum production entities:

- BrandGroup;
- Brand;
- CountryOperator;
- WarrantyAdministrator;
- ServiceNetwork;
- Retailer;
- ProductFamily;
- ProductModel;
- CoverageRule;
- RegistrationRule;
- RoutingRule;
- ContactChannel;
- ServiceCentre;
- SourceEvidence;
- RuleVersion.

Registry admin editing can initially be internal-only.

## 3. Suggested rule schema

### CoverageRule

- id
- country
- brandId
- productFamilyId nullable
- productModelId nullable
- retailerId nullable
- coverageType
- durationMonths nullable
- startBasis
- registrationRequired
- registrationDeadlineDays nullable
- extensionMonths nullable
- plainLanguageSummary
- effectiveFrom nullable
- effectiveTo nullable
- confidence
- sourceEvidenceId
- lastVerifiedAt

### RoutingRule

- id
- country
- brandId nullable
- productFamilyId nullable
- productModelId nullable
- retailerId nullable
- coverageState nullable
- issueClass nullable
- routeType
- targetEntityId nullable
- requiredEvidence
- instructions
- priority
- effectiveFrom nullable
- effectiveTo nullable
- confidence
- sourceEvidenceId
- lastVerifiedAt

### RegistrationRule

- id
- brandId
- productFamilyId nullable
- productModelId nullable
- mode
- destinationUrl / channel
- requiredFields
- deadlineDays nullable
- benefitDescription
- consentTextRef nullable
- sourceEvidenceId
- lastVerifiedAt

## 4. UX flow — new purchase

1. User scans product.
2. KADARI identifies likely brand/category.
3. User scans serial/model plate.
4. User captures receipt.
5. KADARI creates / enriches Digital Twin.
6. Care Registry runs.
7. Care result appears:

Example:

> **Warranty found**  
> 12 months, ending 14 September 2027.

If registration can improve coverage:

> **Extra cover available**  
> Register before 14 October to unlock additional protection.  
> **Authorise KADARI to activate it**

8. User authorises.
9. Registration flow runs.
10. Confirmation is saved.
11. Care reminders are scheduled.

## 5. UX flow — problem

1. User opens asset.
2. Taps **Something's wrong**.
3. Describes issue.
4. Adds evidence if helpful.
5. KADARI checks safety.
6. KADARI checks coverage.
7. KADARI resolves route.
8. Shows one primary recommendation.
9. User approves / sends / follows route.
10. Case opens.
11. Follow-up dates tracked.
12. Outcome recorded.
13. Material result is written into asset history.

## 6. Example routing outputs

### Retailer credit / return

> **Take this back to Makro**  
> This type of product is handled as a retailer warranty-credit item rather than a repair.  
> Your proof of purchase and product details are ready.

Actions:

- Show proof pack
- Directions / retailer details
- Open case

### OEM repair

> **This should go through LG support**  
> Your product appears to still be under warranty. KADARI has prepared the serial number, receipt and issue details.

Actions:

- Prepare message
- Open official support
- Record reference

### Carry-in service

> **Authorised workshop required**  
> The nearest verified centre for this product category is available in the registry.

Actions:

- View centre
- Show proof pack
- Record drop-off

## 7. Case timeline

A case timeline should support events such as:

- issue reported;
- case prepared;
- message sent;
- external reference received;
- evidence requested;
- evidence supplied;
- service centre appointed;
- appointment booked;
- product collected;
- assessment complete;
- repair authorised;
- parts ordered;
- repair complete;
- credit issued;
- replacement issued;
- case closed.

## 8. Care reminders and automation rules

V1 scheduling logic:

### Registration opportunity

- immediately after identification;
- halfway to registration deadline if incomplete;
- 72 hours before deadline;
- final day only if user has opted into reminders and action is still possible.

### Warranty expiry

Default meaningful assets:

- 90 days;
- 30 days;
- optional 7 days if an open issue exists.

### Case follow-up

If provider promises response by date/time, create a commitment.

If no outcome is recorded by commitment expiry:

- remind user;
- prepare follow-up;
- optionally escalate based on rule / user approval.

## 9. Notification content rules

Notifications must explain why the user should care.

Good:

> Your Bosch dishwasher warranty expires in 30 days. If you've noticed anything unusual, now is the time to check it.

Bad:

> Warranty reminder: 30 days remaining.

Care notifications should be useful, not engagement-for-engagement's-sake.

## 10. Permissions

At minimum distinguish:

- owner;
- household/team member with asset access;
- case operator;
- external counterparty;
- service provider;
- admin/research operator.

External parties must only receive the minimum case / asset data necessary.

## 11. Auditability

Every material automated action should be auditable:

- what rule fired;
- what source supported it;
- what data was submitted;
- under whose authority;
- when;
- what result came back.

This is particularly important for registration and external communication.

## 12. AI boundary

AI may help with:

- extracting receipts / labels;
- category / model suggestions;
- warranty-language summarisation;
- plain-language explanations;
- issue summarisation;
- drafting correspondence;
- symptom classification;
- selecting likely routing rules.

AI must not silently invent:

- warranty duration;
- coverage entitlement;
- OEM policy;
- service-centre authorisation;
- legal entitlement;
- successful registration.

Those require registry evidence / external confirmation.

## 13. V1 admin / operations surface

Internal Care Ops needs:

- registry search;
- add/edit rule;
- source URL/document;
- source date;
- confidence;
- expiry/review date;
- pending unknown routes;
- failed registrations;
- cases needing manual intervention;
- stale-rule queue.

This internal surface is important from day one even if basic.

## 14. Analytics V1

Track:

- assets assessed for Care;
- coverage found rate;
- registration opportunities found;
- registration completion rate;
- extra months/years of cover activated;
- cases opened;
- route types;
- time to first external response;
- time to resolution;
- outcomes;
- cases requiring manual intervention;
- unknown-route rate;
- rule confidence / staleness.

## 15. Build sequence

### Sprint 1 — Foundation

- Care tab / surface;
- coverage records;
- registry schema;
- registry import of initial SA research;
- basic rule matcher;
- warranty status card.

### Sprint 2 — Activation

- registration opportunity detection;
- authority capture;
- Automatic/Assisted/Guided state machine;
- proof storage;
- reminders.

### Sprint 3 — Resolution

- `Something's wrong` intake;
- routing engine;
- Asset Case object;
- evidence pack;
- customer-send communication.

### Sprint 4 — Case memory

- case timeline;
- external references;
- commitments / follow-up;
- outcome write-back to asset history;
- Care analytics.

### Sprint 5 — Operational hardening

- Care Ops surface;
- stale-rule review;
- unknown-route handling;
- failed-registration queue;
- permissions / audit trail;
- end-to-end tests using top P0 brands.

## 16. P0 test brands

Use the market registry's highest-priority brands / groups for V1 test coverage, including at minimum:

- Samsung;
- LG;
- Hisense;
- Defy/Beko;
- Bosch/Siemens;
- Philips;
- Russell Hobbs/Pineware family;
- Bennett Read;
- Apple;
- Huawei;
- Xiaomi;
- Dell/Lenovo/HP computing cases.

The objective is not exhaustive brand support. It is proving all major route archetypes.

## 17. V1 acceptance scenarios

The build is not considered complete until the following scenarios work end-to-end:

1. **New appliance with ordinary warranty** — receipt + serial → warranty clock.
2. **Product eligible for registration extension** — detect → authorise → Assisted/Guided completion → proof stored.
3. **Low-value product routed back to retailer** — no incorrect service-centre recommendation.
4. **Large appliance requiring OEM/onsite route** — case pack prepared and case tracked.
5. **Carry-in small appliance** — correct service-centre route.
6. **Collection/courier model** — case records collection path.
7. **Unknown brand/model** — system states uncertainty and creates research/manual route rather than guessing.
8. **Open case with promised response date** — follow-up triggers automatically.
9. **Resolved warranty repair** — zero/covered cost and repair event written to asset history.
10. **Rejected case** — rejection reason and evidence retained for escalation / future history.

## 18. Locked implementation principle

**Care must work without permission from the retailer or OEM.**

Self-capture + the Care Registry + customer-authorised communication create the initial network.

Direct integrations come later where they reduce cost, improve resolution or unlock commercial value.

## 19. Definition of V1 success

A real customer buys a product, captures it in KADARI, gets the correct coverage position, activates any available extra cover, receives the right reminder, later reports a fault, is routed correctly, resolves the issue, and has the complete history permanently retained against that same Digital Twin.

If that happens without a retailer POS integration or OEM API, KADARI Care has proven its core thesis.