# KADARI Care — Intelligence & Automation Authority

**Status:** Founder/product authority for build planning  
**Date:** 2026-10-06  
**Scope:** Defines how KADARI Care converts customer evidence and registry knowledge into safe, explainable actions. This is additive to existing KADARI and does not replace the Digital Twin, insurance or evidence architecture.

## 1. Core principle

KADARI Care should automate complexity, not invent certainty.

The automation stack must turn:

**EVIDENCE → STRUCTURED FACTS → RULE MATCH → RECOMMENDATION → AUTHORISED ACTION → CASE MEMORY**

The customer should see one clear next step. Internally, every important action must remain attributable to evidence, registry rules, customer authority and external confirmations.

## 2. Intelligence layers

Care intelligence consists of six layers:

1. **Evidence Intelligence** — understand receipts, serial labels, warranty documents, confirmations and service correspondence.
2. **Product Intelligence** — resolve brand, model, category, identifiers and likely product family.
3. **Coverage Intelligence** — determine what cover is likely or verified to apply and what may be activated.
4. **Routing Intelligence** — determine the correct next counterparty and service path.
5. **Case Intelligence** — understand incoming/outgoing communication, commitments, references, status and likely next action.
6. **Operational Intelligence** — decide whether automation can continue safely or a human operator must intervene.

AI supports these layers. Registry evidence and external confirmation remain the authority for policy and entitlement.

## 3. Evidence ingestion

### 3.1 Receipt / invoice

Extract where possible:
- retailer / seller;
- legal seller name if visible;
- store / branch;
- date/time;
- line item description;
- quantity;
- price;
- VAT / totals where useful;
- SKU / item code;
- order or invoice number;
- payment reference only where needed;
- warranty / protection product line item;
- customer details where present.

The original receipt image/document must remain the source evidence.

Do not treat OCR output as verified fact without preserving confidence and source coordinates / provenance where available.

### 3.2 Product / rating plate / serial label

Extract:
- brand;
- model;
- serial number;
- IMEI / VIN / GTIN / EAN / barcode where relevant;
- manufacture code/date where available;
- electrical/spec data only where useful;
- country/manufacturer/distributor labels where useful.

Serial extraction should use validation rules where known, but must never silently alter a serial to fit a pattern.

### 3.3 Box / packaging

Useful for:
- barcode / GTIN;
- exact commercial model;
- variant / colour / capacity;
- importer/distributor identity;
- warranty or promotional registration instructions.

### 3.4 Warranty / guarantee document

Extract:
- provider;
- duration;
- start basis;
- exclusions;
- registration requirement;
- registration deadline;
- extended-benefit terms;
- repair/replace/credit language;
- proof requirements;
- contact channels;
- service model references;
- applicable geography;
- document version/effective date where visible.

AI may explain these terms in plain English, but the source document remains authoritative.

### 3.5 Registration confirmation

Extract:
- registered owner;
- asset/product;
- registration date;
- confirmation/reference;
- effective coverage period;
- extension granted;
- provider/OEM identity.

A registration is only `COMPLETED` when there is evidence of success, not merely because a form was submitted.

### 3.6 Communication / service evidence

Supported inputs:
- pasted text;
- email copy;
- WhatsApp screenshot;
- SMS screenshot;
- web confirmation;
- PDF / letter;
- call note entered by user/operator;
- job card;
- technician invoice;
- repair report.

Extract where relevant:
- sender / provider;
- external reference;
- request for evidence;
- decision;
- appointment;
- promised response date;
- service centre;
- technician;
- part status;
- rejection reason;
- outcome.

## 4. Fact model

Every extracted fact must retain:
- value;
- source evidence id;
- extraction method;
- confidence;
- observed/declared/inferred/evidenced/verified state;
- timestamp;
- optional bounding/source location;
- user correction history.

Never overwrite user-confirmed facts silently after later AI extraction.

Conflict example:
- receipt says model ABC123;
- label says ABC123-ZA;

The system should reconcile through hierarchy and/or user confirmation rather than discard one source.

## 5. Identity resolution

Resolution order should combine:
1. exact identifiers (serial/model/GTIN);
2. label text;
3. receipt line item / SKU;
4. packaging data;
5. visual recognition;
6. user declaration.

Preferred output:
- resolved brand;
- product family/category;
- exact model where confidence is sufficient;
- variant;
- registry entity ids;
- unresolved fields;
- confidence and rationale internally.

If exact model cannot be established, Care may still proceed using a broader verified brand/category rule.

## 6. Registry matching

Rule matcher should score candidate rules using:
- country;
- brand;
- model;
- product family/category;
- retailer;
- purchase date;
- effective date;
- coverage state;
- issue class;
- registration status;
- seller type / marketplace seller where relevant.

Specificity priority remains:
1. exact model;
2. product family;
3. brand/category;
4. brand generic;
5. retailer/category;
6. generic fallback.

A lower-specificity rule must not override a more specific valid rule without an explicit priority/version reason.

## 7. Coverage confidence states

Customer-facing coverage should use four broad states:

### VERIFIED
Supported by external confirmation or directly applicable, current source evidence with sufficient asset match.

### HIGH CONFIDENCE
Strong registry rule + sufficient asset/purchase evidence, but no external confirmation yet.

### POSSIBLE
Likely based on partial data; customer should be prompted for missing evidence.

### UNKNOWN
Insufficient reliable basis. KADARI must not guess.

Internal scoring can be numeric, but the customer should see plain language.

## 8. Plain-English warranty explanation

AI may generate:
- what appears covered;
- what appears excluded;
- what may invalidate cover;
- what evidence should be kept;
- what to do if a fault occurs;
- key dates;
- relevant registration action.

Every explanation must be grounded in:
- registry rule;
- uploaded warranty terms; and/or
- official source evidence.

Where source language is ambiguous, say so.

Do not present AI paraphrase as legal advice or as stronger than the source terms.

## 9. Registration automation

### 9.1 Automation modes

**AUTOMATIC**
KADARI can submit on the user's behalf where technically and contractually permitted.

**ASSISTED**
KADARI prepares everything but pauses for a customer-only action such as OTP, login, consent checkbox or final submit.

**GUIDED**
KADARI walks the customer through the official process and captures evidence of completion.

### 9.2 Registration decision gate

Before registration automation:
- rule must be current enough;
- asset match must be sufficient;
- deadline must not be expired;
- customer must see the benefit;
- destination/provider must be clear;
- data leaving KADARI must be previewable;
- explicit authority must be captured.

### 9.3 Registration audit

Record:
- rule used;
- source/version;
- authority timestamp;
- submitted fields;
- destination;
- execution mode;
- submission timestamp;
- external response;
- confirmation/reference;
- effective coverage change;
- proof artifact.

## 10. Issue understanding

When user says `Something's wrong`, AI may classify:
- symptom;
- product subsystem where useful;
- error code;
- likely issue class;
- accidental/physical damage signal;
- safety signal;
- urgency;
- evidence that may help routing.

The classifier is not a repair diagnosis unless supported by a verified technical rule.

Customer wording must always be preserved alongside any AI summary.

## 11. Safety gate

Potential hazards must interrupt normal troubleshooting.

Signals include:
- smoke/fire;
- gas smell/leak;
- exposed electricity;
- severe overheating;
- battery swelling/fire;
- dangerous fluid leakage;
- risk of electric shock.

For safety-sensitive cases:
- do not encourage disassembly or risky diagnostics;
- use verified safe wording;
- route to emergency/OEM/professional support as appropriate;
- preserve the case for continuation after immediate safety action.

## 12. Routing engine

Routing combines:

**Asset facts + purchase facts + coverage facts + issue class + registry rules + location where needed**

Output:
- primary route;
- alternate route only where genuinely valid;
- required proof;
- contact/channel;
- customer action;
- expected next state;
- internal confidence and source.

The engine must be deterministic where a high-confidence registry rule exists.

AI may rank candidates but may not override hard policy rules without a human-reviewed exception.

## 13. Message generation

KADARI may draft:
- initial warranty/service request;
- follow-up;
- evidence response;
- escalation;
- appointment confirmation;
- retailer return summary.

Messages should automatically include only relevant facts:
- owner/contact as authorised;
- product/model/serial;
- retailer/purchase date;
- issue summary;
- coverage status where relevant;
- attached evidence;
- requested action.

Never fabricate:
- legal threats;
- warranty entitlement;
- OEM admissions;
- technician findings;
- dates/references not in evidence.

## 14. Case response intelligence

When a reply is captured, AI may identify:
- acknowledgement;
- case/reference number;
- evidence request;
- appointment;
- promised response date;
- service-centre assignment;
- repair approval;
- rejection;
- replacement/credit/refund;
- unresolved/ambiguous response.

The system should propose updates for user/operator confirmation when confidence is not sufficient.

## 15. Commitment monitor

A `Commitment` is a future action promised by a party.

Examples:
- provider will respond by Friday;
- technician will call tomorrow;
- customer must send installation certificate;
- courier will collect Monday.

Automation:
1. extract commitment;
2. identify actor;
3. identify due date/time;
4. create case commitment;
5. schedule reminder/check;
6. mark complete when evidence arrives;
7. if overdue, prepare follow-up.

Do not repeatedly chase a counterparty without customer authority and anti-spam controls.

## 16. Human intervention boundary

Send to Care Ops when any of the following applies:
- conflicting high-confidence rules;
- unknown route on a material asset;
- stale source controlling a material decision;
- registration automation failed repeatedly;
- external form/channel changed materially;
- ambiguous ownership/serial evidence;
- warranty terms conflict with registry;
- provider rejects a seemingly valid claim;
- legal/statutory-rights interpretation becomes material;
- safety wording requires review;
- case value or complexity exceeds configured threshold;
- customer requests human help.

## 17. Automation confidence thresholds

Recommended conceptual thresholds:

### GREEN — automate
- strong identity match;
- current rule;
- no conflict;
- low-risk reversible action;
- explicit authority where needed.

### AMBER — assist / confirm
- partial identity;
- older source;
- multiple valid routes;
- material outbound data;
- ambiguous provider response.

### RED — human review / stop
- conflicting policy evidence;
- potentially unsafe instruction;
- legal ambiguity;
- destructive/non-reversible action;
- material value at risk;
- uncertain counterparty.

Thresholds should be configurable by country/category and not hard-coded globally.

## 18. Care Ops work queues

Minimum queues:
- UNKNOWN_ROUTE;
- STALE_RULE;
- REGISTRATION_FAILED;
- EVIDENCE_CONFLICT;
- PROVIDER_REJECTION;
- SAFETY_REVIEW;
- HIGH_VALUE_CASE;
- CUSTOMER_REQUESTED_HELP;
- SOURCE_CHANGED;
- UNMATCHED_PRODUCT;

Each queue item should show:
- asset;
- case;
- reason;
- blocking uncertainty;
- source evidence;
- recommended operator action.

## 19. Learning loop

Resolved cases should improve the system without automatically rewriting policy.

KADARI can learn:
- actual routes used;
- provider response time;
- service centres actually assigned;
- evidence commonly requested;
- registration flows that changed;
- repeated unknown brands/models;
- common issue categories;
- unsuccessful routing attempts.

Operational learning may suggest registry changes, but a human-reviewed source update is required before changing authoritative warranty/routing rules.

## 20. Source freshness

Every rule needs:
- source URL/document;
- source type;
- effective date where known;
- last verified date;
- next review date;
- confidence;
- status: ACTIVE / REVIEW_DUE / STALE / RETIRED.

Routing or entitlement decisions should become more conservative as source freshness degrades.

## 21. Data minimisation

Automation must send only what is required for the action.

Example: a service centre may need serial, proof of purchase and fault evidence. It does not automatically need the user's full asset portfolio, insurance data or unrelated KADARI history.

## 22. Explainability

For every material Care decision KADARI should be able to answer internally:
- what did we believe?;
- why?;
- which evidence supported it?;
- which rule fired?;
- how fresh was the rule?;
- what did the customer authorise?;
- what was sent externally?;
- what came back?;
- who/what changed the case state?

Customer-facing explanation should remain simple, e.g.:

> **Why KADARI recommends this**  
> Your product type is handled through the retailer during this warranty route, and your purchase proof shows it is still within the relevant period.

## 23. Model/provider agnosticism

Do not couple KADARI Care to one AI vendor.

Separate capabilities:
- OCR/document extraction;
- image/label extraction;
- language reasoning;
- classification;
- structured output;
- embeddings/search if used.

Implement provider abstraction and cost/quality telemetry so models can be changed by task.

## 24. Cost controls

Prefer deterministic/rule execution once data has been structured.

Do not call an LLM repeatedly for facts already extracted and stored.

Cache:
- product identity;
- warranty explanation tied to rule version;
- rule matches;
- standard OEM/retailer templates;
- registry entity metadata.

Use AI when it adds interpretive value, not as the database.

## 25. Acceptance tests

The intelligence layer is not complete until it can prove:
1. receipt extraction with user correction;
2. serial/model extraction without silent serial mutation;
3. model-level rule beats brand-level rule;
4. warranty explanation cites the governing source internally;
5. registration opportunity detected and action gated by authority;
6. registration success requires confirmation evidence;
7. problem description becomes structured issue without losing original text;
8. safety signal interrupts normal routing;
9. hard routing rule cannot be silently overridden by AI;
10. provider response creates a proposed reference/commitment update;
11. overdue commitment prepares follow-up;
12. unknown route enters Care Ops rather than guessing;
13. stale rule reduces automation confidence;
14. human resolution can update case without corrupting source facts;
15. every outbound action is auditable.

## 26. Locked automation doctrine

**AI interprets. Rules govern. Evidence proves. The customer authorises. KADARI remembers.**

That is the operating doctrine for Care automation.