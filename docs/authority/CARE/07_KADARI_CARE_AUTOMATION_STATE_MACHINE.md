# KADARI Care — Automation State Machine

**Status:** Implementation companion to Care Intelligence Authority  
**Date:** 2026-10-06

## 1. Purpose

This file defines the core operational states and transitions for KADARI Care automation. It is not a UI map. UI surfaces should translate these internal states into plain language.

## 2. Asset Care assessment state

`UNASSESSED`
→ `EVIDENCE_PARTIAL`
→ `IDENTITY_RESOLVED`
→ `COVERAGE_MATCHING`
→ one of:
- `COVERAGE_VERIFIED`
- `COVERAGE_HIGH_CONFIDENCE`
- `COVERAGE_POSSIBLE`
- `COVERAGE_UNKNOWN`

Transitions back to assessment are allowed when new evidence or a newer registry rule arrives.

## 3. Evidence extraction state

For each EvidenceItem:
- `UPLOADED`
- `PROCESSING`
- `EXTRACTED`
- `NEEDS_CONFIRMATION`
- `CONFIRMED`
- `CONFLICT`
- `FAILED`

Rules:
- extraction failure must not block manual entry;
- user correction creates a new fact version, not a destructive overwrite;
- conflicting evidence creates an explicit conflict object.

## 4. Identity resolution state

- `IDENTITY_UNKNOWN`
- `BRAND_RESOLVED`
- `FAMILY_RESOLVED`
- `MODEL_CANDIDATE`
- `MODEL_RESOLVED`
- `IDENTITY_CONFLICT`

Resolution confidence must remain separate from user-facing evidence status.

## 5. Coverage state

CoverageRecord state:
- `NOT_ASSESSED`
- `LIKELY_ACTIVE`
- `ACTIVE_VERIFIED`
- `REGISTRATION_REQUIRED`
- `REGISTRATION_AVAILABLE`
- `EXPIRING`
- `EXPIRED`
- `REJECTED_OR_INVALID`
- `UNKNOWN`

Coverage can coexist. Example: manufacturer warranty may be expired while extended protection remains active.

## 6. Registration state machine

- `OPPORTUNITY_DETECTED`
- `WAITING_AUTHORITY`
- `AUTHORISED`
- `PREPARING`
- `SUBMITTING_AUTOMATIC`
- `WAITING_USER_STEP`
- `WAITING_EXTERNAL_CONFIRMATION`
- `COMPLETED`
- `FAILED_RETRYABLE`
- `FAILED_MANUAL`
- `EXPIRED`
- `CANCELLED`

### Valid principles

`COMPLETED` requires proof of success.

`SUBMITTING_AUTOMATIC` is permitted only when:
- rule mode allows AUTOMATIC;
- user authority is valid;
- required fields exist;
- source/rule is current enough;
- no blocking conflict exists.

If CAPTCHA/OTP/login appears unexpectedly:
`SUBMITTING_AUTOMATIC` → `WAITING_USER_STEP`.

Repeated execution failure:
`FAILED_RETRYABLE` → `FAILED_MANUAL` → Care Ops.

## 7. Problem intake state

- `DRAFT`
- `ISSUE_CAPTURED`
- `SAFETY_CHECK`
- `SAFETY_INTERRUPTED`
- `EVIDENCE_CHECK`
- `ROUTING`
- `ROUTE_RESOLVED`
- `ROUTE_AMBIGUOUS`
- `RESEARCH_REQUIRED`
- `READY_TO_OPEN_CASE`

Customer can save/leave while in any non-terminal state.

## 8. Safety branch

If dangerous signal detected:
`SAFETY_CHECK` → `SAFETY_INTERRUPTED`.

System performs verified safety guidance and preserves case draft.

After immediate safety action is acknowledged:
`SAFETY_INTERRUPTED` → `EVIDENCE_CHECK` or `RESEARCH_REQUIRED`.

No risky troubleshooting should be inserted to avoid the safety branch.

## 9. Routing state machine

- `ROUTE_PENDING`
- `RULE_MATCHED_GREEN`
- `RULE_MATCHED_AMBER`
- `MULTIPLE_VALID_ROUTES`
- `RULE_STALE`
- `NO_RULE`
- `ROUTE_CONFIRMED`
- `ROUTE_HUMAN_REVIEW`

### GREEN
High-specificity/current rule + strong identity/evidence.

May proceed automatically to recommendation.

### AMBER
Partial identity, older rule, material ambiguity or multiple legitimate routes.

Requires customer/operator confirmation.

### RED / review
No reliable route, conflict, unsafe route or material uncertainty.

Must not guess.

## 10. Case lifecycle state

Internal CaseState:
- `PREPARING`
- `READY_TO_SEND`
- `SENT`
- `WAITING_PROVIDER`
- `WAITING_CUSTOMER`
- `APPOINTMENT_BOOKED`
- `COLLECTION_BOOKED`
- `IN_ASSESSMENT`
- `REPAIR_APPROVED`
- `REPAIR_IN_PROGRESS`
- `WAITING_PART`
- `REPLACEMENT_APPROVED`
- `CREDIT_APPROVED`
- `REFUND_APPROVED`
- `REJECTED`
- `ESCALATION_REVIEW`
- `RESOLVED`
- `CLOSED`
- `WITHDRAWN`

UI may group these into simpler statuses.

## 11. Outbound communication state

For each OutboundAction:
- `DRAFTED`
- `USER_REVIEW`
- `AUTHORISED`
- `HANDED_OFF`
- `SENT_CONFIRMED`
- `SEND_FAILED`
- `CANCELLED`

Direct automated sending later may add:
- `QUEUED`
- `PROVIDER_ACCEPTED`
- `DELIVERED`

V1 customer-channel handoff must not claim `SENT_CONFIRMED` unless user/system evidence confirms sending.

## 12. Inbound response state

- `CAPTURED`
- `PARSING`
- `UNDERSTOOD_HIGH_CONFIDENCE`
- `NEEDS_CONFIRMATION`
- `AMBIGUOUS`
- `APPLIED_TO_CASE`

Possible extracted actions:
- set external reference;
- request evidence;
- create appointment;
- create commitment;
- mark rejection;
- propose outcome.

Material case decisions should be user/operator confirmed when confidence is insufficient.

## 13. Commitment state

- `OPEN`
- `DUE_SOON`
- `OVERDUE`
- `FULFILLED`
- `CANCELLED`
- `SUPERSEDED`

Transition rules:
- future due date → `OPEN`;
- configured pre-due threshold → `DUE_SOON`;
- due date passed without evidence → `OVERDUE`;
- matching response/event received → `FULFILLED`;
- provider replaces commitment with new date → prior `SUPERSEDED`, new commitment `OPEN`.

## 14. Follow-up automation

When commitment becomes `OVERDUE`:
1. check whether reply/outcome has already arrived;
2. if not, prepare follow-up;
3. show user/operator latest history;
4. request/send according to permitted mode;
5. create follow-up event;
6. optionally create a new commitment.

Never enter a self-triggering follow-up loop.

## 15. Outcome state

- `REPAIRED_COVERED`
- `REPAIRED_PAID`
- `REPLACED`
- `CREDITED`
- `REFUNDED`
- `REJECTED`
- `NO_FAULT_FOUND`
- `WITHDRAWN`
- `OTHER`

Outcome is written to Digital Twin history only when user/operator confirms or evidence is sufficient.

## 16. Human review state

CareOpsTask state:
- `OPEN`
- `CLAIMED`
- `IN_REVIEW`
- `WAITING_CUSTOMER`
- `WAITING_EXTERNAL`
- `RESOLVED`
- `CANCELLED`

A human decision must record:
- operator;
- reason;
- evidence reviewed;
- change made;
- whether registry update is suggested.

## 17. Registry rule lifecycle

- `DRAFT`
- `ACTIVE`
- `REVIEW_DUE`
- `STALE`
- `SUPERSEDED`
- `RETIRED`

Automation rules:
- `ACTIVE`: normal confidence calculation;
- `REVIEW_DUE`: allowed but confidence penalty/configurable review alert;
- `STALE`: material decisions move to AMBER/RED unless corroborated;
- `SUPERSEDED/RETIRED`: never selected for new cases except historical reconstruction.

## 18. Confidence composition

Do not rely on one opaque AI confidence number.

Care decision confidence should consider at least:
- identity confidence;
- purchase evidence completeness;
- rule specificity;
- rule freshness;
- source authority;
- conflict count/severity;
- external confirmation;
- issue-class confidence.

Store component scores/flags so operators can understand why a decision was GREEN/AMBER/RED.

## 19. Event sourcing / audit principle

Material state transitions should produce immutable CaseEvents / AuditEvents containing:
- entity id;
- prior state;
- new state;
- actor (customer/operator/system/external);
- action/reason;
- rule/source ids where relevant;
- timestamp;
- correlation/idempotency key for automated actions.

This prevents silent state mutation and supports dispute reconstruction.

## 20. Idempotency

External registrations and communications must use idempotency where possible.

The system must prevent:
- duplicate warranty registrations;
- duplicate case creation;
- duplicate OEM messages from retries;
- duplicate reminders after confirmed completion.

If destination does not support idempotency, Care must track local execution fingerprints and require review before uncertain repeat submissions.

## 21. Retry policy

Retry automatically only for technical/transient failures.

Examples:
- timeout;
- temporary network error;
- 5xx response;
- rate limit with valid retry window.

Do not blindly retry:
- invalid customer data;
- rejection response;
- changed form;
- CAPTCHA/OTP;
- consent failure;
- policy/eligibility denial.

Those transition to assisted/manual/review states.

## 22. Manual override

Operators may override routing/case state only with reason and audit event.

An override does not silently alter the underlying registry rule.

If override reveals a bad/outdated rule, create `RegistryChangeSuggestion` for separate review.

## 23. Failure containment

Failure of an external OEM site must not corrupt the Digital Twin or lose the customer's case draft.

External-system status and KADARI internal status must remain separate.

Example:
- KADARI case = `READY_TO_SEND`
- Samsung form = unavailable

The UI should say the external channel is unavailable, not that the customer's KADARI case failed.

## 24. Automation telemetry

Track at minimum:
- extraction success/error rate by evidence type;
- identity resolution rate;
- rule match specificity;
- GREEN/AMBER/RED rate;
- registration automatic/assisted/guided mix;
- registration completion rate;
- routing unknown rate;
- human intervention rate;
- false/changed-route corrections;
- external channel failure rate;
- commitment extraction accuracy after confirmation;
- follow-up conversion to response;
- time to resolution.

## 25. Definition of done

Automation is ready for production only when:
- deterministic state transitions exist;
- every material transition is auditable;
- unsafe/unknown conditions fail closed;
- external failures preserve customer work;
- customer authority gates material outbound actions;
- human review can enter/exit without breaking the case;
- registry version/source is attached to decisions;
- retries cannot generate duplicate external actions;
- Care can reconstruct why a recommendation/action occurred.

## 26. Final rule

**Automation may remove effort. It may not remove accountability.**