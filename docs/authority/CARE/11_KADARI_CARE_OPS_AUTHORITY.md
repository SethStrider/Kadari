# KADARI Care — Care Ops Authority

**Status:** Foundational operating authority  
**Date:** 2026-10-06  
**Scope:** Defines the human operations layer that handles exceptions, ambiguity, customer deadlines and external-channel failures without becoming a generic call centre.

## 1. Core doctrine

Care Ops exists to finish what automation cannot safely or confidently complete.

It is not the primary product and must not become a substitute for fixing weak automation or registry gaps.

**Every Care Ops intervention must end in one of three outcomes:**
1. customer issue resolved;
2. reusable knowledge created;
3. explicit one-off exception recorded.

## 2. Care Ops mission

Care Ops should:
- protect the customer from complexity;
- prevent missed warranty/registration deadlines;
- research unknown routes;
- resolve conflicting information;
- assist failed registrations;
- prepare/escalate communication;
- verify service centres;
- support high-friction OEM/retailer cases;
- feed learning back into the Registry Factory.

## 3. What Care Ops is not

Care Ops is not:
- a general consumer complaints bureau;
- a legal-advice service;
- an insurer claims department;
- an OEM call centre replacement;
- an unstructured inbox;
- a place to permanently park automation failures.

## 4. Ops queue model

Minimum queues:
- DEADLINE_RISK;
- REGISTRATION_FAILED;
- ROUTE_UNKNOWN;
- RULE_CONFLICT;
- OEM_UNRESPONSIVE;
- RETAILER_DISPUTE;
- SERVICE_CENTRE_VERIFICATION;
- SAFETY_REVIEW;
- REJECTION_REVIEW;
- RECALL_REVIEW;
- CASE_STALLED;
- DATA_QUALITY;
- MANUAL_RESEARCH;
- COMMERCIAL_SIGNAL.

Each queue item should include:
- customer/asset/case reference;
- urgency;
- financial/customer-impact estimate;
- relevant deadline;
- automation history;
- registry rules considered;
- evidence available;
- next recommended operator action.

## 5. Priority model

Suggested severity:

### P0 — Immediate
- safety concern;
- warranty/registration expires within 24 hours;
- customer may lose material entitlement;
- active recall with plausible risk.

### P1 — Urgent
- deadline within 72 hours;
- high-value unresolved warranty issue;
- repeated external-channel failure;
- provider commitment overdue materially.

### P2 — Standard
- unknown route;
- service-centre verification;
- normal case escalation;
- failed registration with time remaining.

### P3 — Enrichment
- registry cleanup;
- low-volume long-tail brand research;
- non-urgent source refresh.

## 6. Operator workspace

Care Ops should show one consolidated case workspace containing:
- asset identity;
- purchase evidence;
- coverage state;
- registration state;
- case timeline;
- messages/references;
- registry rules used;
- sources;
- automation attempts;
- customer-authorised data-sharing scope;
- deadlines/commitments;
- recommended next actions;
- registry-improvement prompt.

The operator should not need to open multiple internal systems to understand the case.

## 7. Intervention modes

### Research
Find and verify the correct rule/route.

### Assist
Help customer complete an external step such as OTP/login/final submit.

### Prepare
Draft escalation correspondence or evidence pack.

### Verify
Confirm service centre, warranty administrator, support channel or registration outcome.

### Escalate
Use verified higher-level route where one exists.

### Correct
Fix a bad/obsolete registry rule and trigger re-evaluation.

### Close
Record confirmed outcome and write material history back to asset.

## 8. Customer-authority boundary

Care Ops may only act within authority already granted or newly requested from the customer.

Authority must be specific enough to distinguish:
- prepare only;
- send on my behalf;
- submit warranty registration;
- share selected evidence;
- communicate with retailer/OEM/service provider;
- accept/decline proposed resolution.

Material decisions such as accepting a replacement, settlement, paid repair or waiver require customer confirmation unless governed by a clearly authorised workflow.

## 9. Communication standard

Care Ops communication should be:
- concise;
- factual;
- evidence-backed;
- non-confrontational;
- specific about requested resolution;
- linked to case references;
- free from invented legal threats.

Every outbound communication should record:
- sender mode;
- recipient/channel;
- content snapshot;
- attachments;
- customer authority;
- timestamp;
- delivery/sent state where known.

## 10. Escalation ladder

Where verified routes exist, escalation may progress through:
1. normal support channel;
2. case follow-up;
3. supervisor/escalation channel;
4. warranty administrator/distributor;
5. retailer escalation where appropriate;
6. verified statutory/ombud/regulatory path where applicable and responsibly presented;
7. legal advice referral if necessary.

KADARI must distinguish procedural guidance from legal advice.

## 11. Failed registration handling

When warranty registration fails:
1. preserve all entered data;
2. preserve timestamp and attempted submission evidence;
3. identify failure type;
4. retry only where safe and idempotent;
5. switch AUTOMATIC → ASSISTED/GUIDED if required;
6. escalate to Ops if deadline risk exists;
7. record successful confirmation or unresolved status;
8. update RegistrationRule failure signature if reusable.

## 12. Unknown-route handling

For unknown route:
1. inspect exact brand/model/category/retailer;
2. search official sources first;
3. identify warranty administrator/service operator;
4. verify route and evidence required;
5. update customer case;
6. create or update registry rule;
7. mark confidence and source;
8. close research job.

## 13. OEM-unresponsive handling

Care Ops should track:
- first contact date;
- expected response time if known;
- follow-ups;
- escalation attempts;
- alternate official channels;
- final resolution.

Repeated brand-level unresponsiveness becomes both:
- Registry/Intelligence data;
- commercial signal for OEM outreach.

## 14. Rejection review

When a warranty/return/service request is rejected:
- capture exact stated reason;
- compare against stored terms/evidence;
- identify missing proof or exclusion;
- identify verified escalation route;
- prepare options for customer;
- avoid declaring rejection unlawful unless qualified legal review supports it.

Outcome categories:
- rejection accepted;
- additional evidence submitted;
- escalated;
- overturned;
- alternative repair/replacement path;
- legal/regulatory referral.

## 15. Case closure standard

Do not close a case merely because communication stopped.

Closure requires a clear outcome such as:
- repaired;
- replaced;
- credited;
- refunded;
- rejected final;
- customer withdrew;
- no fault found;
- duplicate/invalid case;
- other documented resolution.

Write material outcome into Digital Twin history.

## 16. Ops-to-Registry feedback

At case closure ask:
- Did we discover a new brand/entity?
- Did a rule change?
- Did the route differ from registry?
- Did registration behavior differ?
- Was a service centre invalid?
- Was there a new failure signature?
- Is the result reusable?

If yes, open/update RegistryResearchJob automatically.

## 17. Ops-to-Commercial feedback

Create commercial signals when:
- brand asset volume passes threshold;
- recurring cases use the same OEM channel;
- repeated support friction is visible;
- manual effort would be reduced by direct integration;
- group-level brand relationship is identifiable;
- OEM response performance becomes measurable.

Care Ops should not sell. It should surface evidence for commercial teams.

## 18. Quality assurance

Randomly review completed operator cases for:
- correct route;
- evidence sufficiency;
- customer authority;
- tone/communication quality;
- correct case outcome;
- reusable learning captured;
- no unnecessary disclosure.

High-risk categories require higher review rate.

## 19. Operator performance metrics

Do not optimise only for ticket closure speed.

Track:
- time to safe next action;
- deadline saved rate;
- first-time route accuracy;
- registration rescue rate;
- case resolution rate;
- customer rework required;
- knowledge reuse created;
- repeat-exception reduction;
- average manual minutes per case;
- escalation success;
- QA error rate.

## 20. Automation displacement goal

Care Ops should get smaller per 1,000 assets as the registry and automation improve.

A rising manual-intervention rate is a product problem to investigate, not a staffing target.

Metric:

**Manual interventions per 1,000 Care-active assets**

This should trend down over time, except when deliberately entering new categories/countries.

## 21. Internal tooling principles

Care Ops tooling must support:
- one-click source opening;
- rule comparison;
- case/evidence preview;
- timeline editing with audit history;
- suggested outbound message;
- customer-authority view;
- deadline highlighting;
- registry update proposal;
- reusable note templates;
- source/version attachment;
- escalation macros with human review.

## 22. Sensitive-data principle

Operators should see only what is needed for the task.

Sensitive evidence should not be copied into ordinary notes or analytics.

All access to customer asset/evidence records must be permissioned and auditable.

## 23. Operating model by maturity

### Stage 1 — Founder/central Ops
Small expert team handles all exceptions and builds registry rapidly.

### Stage 2 — Specialist queues
Split by product family / registration / routing / escalations where volume justifies it.

### Stage 3 — Country/category cells
Local expertise for market-specific rules and language.

### Stage 4 — OEM-partnered Ops
Direct OEM queues reduce manual public-channel work.

Automation remains the default throughout.

## 24. Definition of done

Care Ops is successful when a difficult customer case is resolved without exposing the customer to internal complexity, and the manual work makes future similar cases easier or automatic.

**Human intervention should create compounding knowledge, not recurring labour.**