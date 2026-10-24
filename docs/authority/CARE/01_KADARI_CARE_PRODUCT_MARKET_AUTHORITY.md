# KADARI Care — Product & Market Authority

**Status:** Foundational direction locked for build planning  
**Date:** 2026-10-05  
**Scope:** Adds KADARI Care to the existing KADARI product. This does **not** replace or re-platform the existing insurance / asset-protection work.

## 1. Locked product boundary

KADARI remains the persistent asset record and evidence layer already built.

**KADARI Care** is an additional first-class capability for the life of an owned product:

- warranty and guarantee capture;
- warranty registration / extension;
- plain-language coverage explanation;
- expiry and maintenance reminders;
- fault reporting;
- retailer / OEM / distributor / authorised-service routing;
- case management and communication history;
- repair / replacement outcomes;
- later OEM dashboards and direct channels.

The objective is to make KADARI useful throughout the asset's life, not only at insurance, claim, valuation, sale or transfer events.

## 2. Acquisition and integration principle

**Consumer self-capture is the primary acquisition engine. Enterprise integration is an optimisation layer, not a dependency.**

At or after purchase, the owner should be able to capture:

1. receipt / invoice;
2. product;
3. box / barcode where useful;
4. model / rating plate;
5. serial number.

KADARI extracts and structures the data and creates/updates the existing asset record.

Do **not** make KADARI Care dependent on retailer POS integrations. Retail and OEM integrations may be added later where volume and economics justify them.

## 3. Warranty activation principle

KADARI Care must not merely record a warranty. It must ask:

> **Can KADARI improve or activate this coverage for the owner?**

Where product registration unlocks or extends cover, the preferred customer experience is:

> **Authorise KADARI to register / extend my warranty**

Registration modes:

- **Automatic** — KADARI submits under explicit user authority where technically and contractually permitted.
- **Assisted** — KADARI completes the data and the user performs a required OTP, login, consent or final submission.
- **Guided** — KADARI guides the user through the official process where direct automation is not allowed or reliable.

Successful registration evidence must be retained against the asset:

- registration date;
- OEM / provider reference;
- confirmation email / PDF / screenshot where available;
- applicable terms;
- effective expiry date;
- source and date verified.

## 4. Warranty Clock

Coverage must behave as an active object.

KADARI Care should maintain key dates and generate useful, non-spammy reminders based on asset value, risk and maintenance need.

Potential lifecycle events:

- coverage activated;
- registration deadline approaching;
- quarterly / meaningful remaining-cover reminder where justified;
- 90-day expiry warning;
- 30-day expiry warning;
- pre-expiry product health check;
- maintenance due;
- open case follow-up;
- recall / safety event.

The system should distinguish, where applicable:

- manufacturer warranty;
- retailer guarantee / return route;
- extended protection / service plan;
- applicable statutory consumer rights.

## 5. After-sales routing principle

There is no single South African warranty journey.

The market currently exhibits at least these operational models:

- retailer return / warranty credit;
- OEM direct;
- authorised service centre;
- distributor-managed service;
- onsite service;
- workshop / carry-in;
- collection / courier;
- hybrid by category, retailer or product;
- registration-dependent extended warranty;
- unknown / long-tail fallback.

Therefore KADARI Care must implement a **resolution engine**, not a static contact directory.

### Routing equation

**Brand + product category/model + retailer + purchase date + active coverage + issue type = best next action.**

The output may be:

- return to original retailer;
- request credit / replacement;
- open OEM case;
- route to local warranty administrator;
- select authorised service centre;
- book onsite technician;
- arrange collection;
- carry product to workshop;
- invoke an extended-protection provider;
- research-on-demand if no verified route exists.

## 6. Asset Case Engine

Every lifecycle problem should become a generic **Asset Case** linked to the existing asset.

Possible case types:

- Warranty Case
- Repair Case
- Return Case
- Recall Case
- Insurance Case
- Maintenance Case
- Replacement Case
- Ownership / Transfer Case

Each Case should support:

- asset;
- owner;
- counterparty;
- issue;
- evidence;
- messages;
- references;
- actions;
- deadlines;
- status;
- outcome.

This prevents hard-coding separate workflows for every OEM.

## 7. Communications principle

KADARI Care should become the owner's organised interaction layer with the product ecosystem.

Initial channels may include:

- customer-sent WhatsApp / email prepared by KADARI;
- OEM email;
- OEM WhatsApp;
- official web forms;
- call-centre reference capture;
- authorised repair centres.

All interactions, references, attachments and outcomes should be written back into the Asset Case and asset history.

Over time, high-volume OEMs can receive a KADARI dashboard and eventually direct API / webhook / portal connectivity.

## 8. OEM engagement sequence — proof before partnership

Do not make OEM contracts a prerequisite to value.

Sequence:

1. customers self-capture assets;
2. KADARI manages warranty and service activity through existing public/official channels;
3. KADARI accumulates real brand/product interaction volume;
4. OEM recognises recurring KADARI-originated cases;
5. KADARI offers a free/basic OEM response surface;
6. paid operational automation and intelligence follow;
7. direct integrations are added only where justified.

The commercial conversation changes from:

> “Please integrate with our startup”

to:

> “Your customers already manage thousands of your products through KADARI. Here is a lower-cost direct channel.”

## 9. Registry architecture

The registry must distinguish these entities because they are often not the same organisation:

- consumer-facing brand;
- global parent / brand owner;
- South African legal entity;
- importer / distributor;
- warranty administrator;
- service-network operator;
- retailer;
- service centre;
- product category/model.

Examples identified in the initial sweep show group leverage matters. One commercial/service relationship can cover multiple brands.

## 10. Core Care engines

KADARI Care should ultimately expose four internal engines:

### Coverage Engine
Determines what coverage exists, what can be activated, what expires when, and what evidence/conditions apply.

### Routing Engine
Determines the correct next counterparty and service route for a particular asset and issue.

### Case Engine
Creates and maintains the structured lifecycle case and communication history.

### Reminder Engine
Manages registration windows, coverage expiries, maintenance, unresolved cases and recall events.

## 11. Registry data standard

For every material brand/category combination, capture:

### Commercial identity
- brand;
- global parent;
- SA legal entity;
- importer/distributor;
- warranty administrator;
- service operator.

### Market presence
- retailers;
- product categories;
- direct OEM channels;
- private-label / exclusive relationships.

### Coverage rules
- standard warranty;
- registration requirement;
- extra-cover opportunity;
- activation deadline;
- evidence required;
- meaningful exclusions;
- source terms.

### Resolution rules
- retailer return;
- credit / replacement;
- carry-in;
- onsite;
- workshop;
- collection/courier;
- OEM;
- distributor;
- authorised service centre.

### Interaction channels
- email;
- WhatsApp;
- telephone;
- form;
- portal;
- later KADARI Connect.

### Service infrastructure
- centre;
- geography;
- category;
- onsite/carry-in/collection capability;
- source proving authorisation.

### Automation capability
- Automatic / Assisted / Guided;
- required fields;
- customer consent / OTP / login requirement.

### Evidence governance
- source URL;
- document / terms version where available;
- last verified date;
- confidence level;
- change history.

## 12. Data integrity principle

Warranty and service rules change.

No rule should be treated as timeless.

Production registry records must carry:

- source;
- last verified date;
- confidence;
- effective date where known;
- review / refresh status.

If KADARI cannot determine the correct route with sufficient confidence, it should explicitly fall back to research-on-demand or verified official support rather than inventing an answer.

## 13. Initial South African research estate

The initial market registry covers:

- major national appliance/electronics retailers;
- grocery/general-merchandise chains that sell electrical products;
- computing/device specialists;
- DIY/tool retailers;
- OEM-direct channels;
- marketplaces;
- major appliance, electronics, small-appliance, computing, mobile, power-tool and portable-power brands;
- group/ownership relationships;
- initial service-route classification;
- initial warranty-registration opportunities.

The attached workbook is the working research artifact. CSV extracts are committed alongside it for repository search and future machine ingestion.

## 14. Strategic commercial principle

KADARI Care should become useful **before** retailers and OEMs integrate.

The registry plus self-capture lets KADARI solve the customer's problem first.

Volume then becomes commercial leverage with OEMs, distributors and retailers.

KADARI should monetise software, orchestration, permissioned services and later aggregated intelligence, while preserving the existing KADARI insurance/protection proposition rather than replacing it.

## 15. Build boundary for Pierre

Do **not** rebuild existing KADARI.

Care is additive.

The first build specification should be based on this authority and the market registry, but implementation should only begin after the registry rules and desired V1 scope are converted into a dedicated technical/build handover.
