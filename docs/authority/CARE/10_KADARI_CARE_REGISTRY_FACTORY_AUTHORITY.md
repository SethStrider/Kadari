# KADARI Care — Registry Factory Authority

**Status:** Foundational operating authority  
**Date:** 2026-10-06  
**Scope:** Defines how KADARI Care continuously discovers, verifies, versions and maintains warranty, service, retailer, OEM and after-sales knowledge.

## 1. Core doctrine

KADARI Care cannot rely on static warranty knowledge.

The Care Registry is a living operational knowledge graph.

**Every rule must be sourced, versioned, dated, confidence-scored and refreshable.**

The objective is not to maintain a directory. The objective is to maintain enough verified structured knowledge for KADARI to determine the correct next action for a real asset and issue.

## 2. Registry Factory mission

The Registry Factory must continuously convert fragmented public and partner information into reusable Care knowledge.

Inputs may include:
- OEM warranty pages;
- warranty PDFs;
- product registration pages;
- retailer returns / guarantees;
- authorised service-centre directories;
- distributor information;
- product manuals;
- recalls / safety notices;
- official support channels;
- OEM partner data;
- service-provider confirmations;
- customer-submitted documents;
- resolved KADARI cases;
- Care Ops research.

Outputs are versioned registry entities and executable rules.

## 3. Registry knowledge layers

### Layer A — Identity
- BrandGroup
- Brand
- LegalEntity
- CountryOperator
- Distributor
- WarrantyAdministrator
- ServiceNetwork
- Retailer
- ServiceCentre

### Layer B — Product structure
- ProductCategory
- ProductFamily
- ProductModel
- SKU / GTIN / MPN aliases
- regional model aliases

### Layer C — Coverage
- CoverageRule
- RegistrationRule
- ExtendedCoverageRule
- RetailerGuaranteeRule
- MaintenanceRule
- RecallRule

### Layer D — Resolution
- RoutingRule
- ContactChannel
- ServiceCentre capability
- collection / carry-in / onsite rules
- retailer-credit / replace rules

### Layer E — Evidence
- SourceEvidence
- SourceDocument
- RuleVersion
- VerificationEvent
- ChangeEvent

## 4. Source hierarchy

When sources conflict, prefer evidence in this order unless a later/more specific source clearly overrides it:

1. current official legal/warranty terms for the exact market/model;
2. current official OEM product/warranty/support page;
3. current official retailer return/guarantee policy;
4. current official distributor/warranty administrator information;
5. current authorised service-network information;
6. official manuals / product registration confirmations;
7. partner-provided structured data;
8. documented KADARI case evidence;
9. reputable secondary source;
10. unsupported user statement.

Never convert a low-authority source into a high-confidence production rule without verification.

## 5. Specificity hierarchy

Rules should resolve from most specific to least specific:

1. country + exact model + retailer where relevant;
2. country + product family + retailer;
3. country + brand + category + retailer;
4. country + exact model;
5. country + family;
6. country + brand + category;
7. country + brand generic;
8. retailer + category generic;
9. global fallback where legally/operationally valid;
10. unknown.

## 6. Research object

Every research task creates a `RegistryResearchJob` with:
- id;
- trigger;
- brand / model / retailer / category scope;
- originating asset/case if applicable;
- priority;
- assigned operator/agent;
- startedAt;
- completedAt;
- outcome;
- reusableKnowledgeCreated boolean;
- reviewRequired boolean.

Triggers:
- new brand discovered;
- unknown route;
- stale rule;
- failed registration;
- conflicting evidence;
- unresolved case;
- user uploads unseen warranty document;
- service centre no longer valid;
- high-volume brand gap;
- scheduled refresh.

## 7. Ingestion pipeline

### Stage 1 — Discover
Find candidate sources and entities.

### Stage 2 — Extract
Extract factual fields without silently normalising uncertainty away.

### Stage 3 — Classify
Determine whether source concerns identity, coverage, registration, routing, service infrastructure, recall or maintenance.

### Stage 4 — Resolve entities
Map names to canonical brand/group/operator/retailer records.

### Stage 5 — Propose rule
Create draft structured rule(s).

### Stage 6 — Validate
Check conflicts, effective dates, specificity and source authority.

### Stage 7 — Approve
Auto-approve only low-risk/high-confidence changes that meet policy. Otherwise human review.

### Stage 8 — Publish
Create immutable RuleVersion and mark active.

### Stage 9 — Observe
Monitor usage and exceptions.

### Stage 10 — Refresh
Re-check on schedule or trigger.

## 8. Confidence model

Suggested internal confidence states:
- VERIFIED — direct, current, official, sufficiently specific;
- HIGH — strong official source but some model/category inference;
- MEDIUM — credible but incomplete or partially inferred;
- LOW — weak/secondary or conflicting evidence;
- UNKNOWN — no defensible rule.

Customer-facing UX should not expose these as technical labels by default.

Low/unknown confidence may never silently trigger irreversible external action.

## 9. Freshness policy

Every production rule carries:
- firstObservedAt;
- effectiveFrom;
- effectiveTo where known;
- lastVerifiedAt;
- nextReviewAt;
- source hash / fingerprint where available;
- current status.

Suggested refresh cadence:
- registration promotions / temporary extensions: 7–30 days;
- retailer returns policies: 30–90 days;
- warranty terms: 90–180 days;
- service-centre lists: 30–90 days;
- brand ownership / distributor structure: 180–365 days;
- stable historical rules: event-driven after retirement.

Higher-usage rules should be refreshed more often.

## 10. Change detection

The Factory should detect material changes such as:
- warranty duration changed;
- registration window changed;
- extension benefit changed;
- service administrator changed;
- support channel changed;
- service centre removed/added;
- retailer return rule changed;
- model family reclassified;
- new recall;
- source disappeared;
- source text materially changed.

Material changes create a review queue before replacing an active production rule where customer outcomes could change.

## 11. Rule versioning

Never overwrite history.

When a rule changes:
1. close old RuleVersion with effective/end date where known;
2. create new RuleVersion;
3. preserve source evidence;
4. record reason for change;
5. re-evaluate affected future reminders/open cases if material.

Past cases must remain explainable according to the rule that existed when the action was taken.

## 12. Unknown-product handling

Unknown is a valid state.

When KADARI encounters an unknown product:
1. preserve the asset and evidence;
2. resolve brand/model aliases if possible;
3. search the registry;
4. create research job if unresolved;
5. give customer safe fallback rather than guessing;
6. convert result into reusable knowledge where possible.

## 13. Exception-to-knowledge loop

Every manually resolved exception must be evaluated:

**Was this reusable?**

If yes:
- create/update registry entity;
- create/update rule;
- attach evidence;
- publish after review;
- link original case as observation.

If no:
- mark as one-off;
- record why it is not reusable.

This is a core KADARI moat.

## 14. Service-centre verification

A service centre record must support:
- brand(s);
- categories/models handled;
- address;
- coordinates where appropriate;
- phone/email;
- carry-in / onsite / collection;
- operating hours if verified;
- authorisation source;
- last verified date;
- active/inactive state.

A general repair shop is not automatically an authorised service centre.

## 15. Registration automation registry

Each RegistrationRule must additionally describe:
- registration benefit;
- deadline;
- required data;
- form fields;
- login/account requirement;
- OTP requirement;
- CAPTCHA / anti-automation restriction;
- customer-consent wording requirement;
- execution mode AUTOMATIC / ASSISTED / GUIDED;
- proof-of-success signal;
- failure signatures;
- last tested date.

## 16. Retailer registry

Retailer records should include:
- group owner;
- trading brands;
- channel type;
- national/regional footprint;
- categories sold;
- relevant return/guarantee policy;
- proof requirements;
- seller-of-record complexity for marketplaces;
- support channels;
- direct OEM handoff behavior;
- last verified date.

Marketplace transactions must preserve seller-of-record where possible.

## 17. Brand-group leverage

The Registry should expose group relationships so commercial teams can see that one relationship may unlock multiple brands.

Examples of relationship types:
- common global parent;
- shared SA legal entity;
- shared distributor;
- shared warranty administrator;
- shared service network;
- licensed local brand rights.

## 18. Registry Factory queues

Minimum operational queues:
- NEW_BRAND;
- NEW_MODEL;
- UNKNOWN_ROUTE;
- STALE_RULE;
- SOURCE_CHANGED;
- SOURCE_MISSING;
- CONFLICTING_RULE;
- FAILED_REGISTRATION;
- SERVICE_CENTRE_UNVERIFIED;
- HIGH_VOLUME_GAP;
- RECALL_REVIEW;
- PARTNER_DATA_UPDATE.

## 19. Prioritisation score

Research priority should be driven by a combination of:
- number of affected assets;
- number of open cases;
- customer harm / urgency;
- warranty deadline proximity;
- transaction value;
- brand volume;
- commercial/OEM relevance;
- repeat exception count;
- source staleness.

High-impact customer deadlines override routine enrichment.

## 20. Human review boundaries

Human review required when:
- sources conflict materially;
- legal/statutory interpretation is required;
- rule would change financial entitlement;
- automation may submit data externally with uncertainty;
- service-centre authorisation is unclear;
- recall/safety information is ambiguous;
- source is low confidence;
- model identity is uncertain and materially affects route.

## 21. Audit requirements

For every registry mutation record:
- who/what proposed it;
- source(s);
- extraction output;
- previous value;
- new value;
- approval actor;
- timestamp;
- reason;
- affected rules/entities;
- rollback reference.

## 22. Factory metrics

Track:
- % of Care assets with resolved brand;
- % with resolved model/family;
- coverage-found rate;
- unknown-route rate;
- stale-rule rate;
- average research turnaround;
- exception recurrence rate;
- manual research per 1,000 assets;
- rules created from exceptions;
- automation completion rate;
- source-change detection accuracy;
- service-centre invalidation rate.

## 23. Definition of done

The Registry Factory is working when a previously unknown product can enter KADARI, trigger research once, and future materially similar products/cases can resolve automatically from reusable verified knowledge.

**Solve once. Learn permanently.**