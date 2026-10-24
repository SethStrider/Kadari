# KADARI Care — API & Event Contract

**Status:** Build authority  
**Date:** 2026-10-06  
**Scope:** Minimum application/API contract for KADARI Care V1. Endpoint names are illustrative. Existing KADARI conventions take precedence where compatible.

## 1. API principles

- additive to the existing KADARI API;
- asset identity remains owned by core KADARI;
- Care uses the existing auth/tenant/permission model;
- reads should expose human-useful state plus IDs needed by the client;
- writes should be explicit commands, not giant generic PATCH payloads;
- all external actions require an audit record;
- all high-risk writes should accept an idempotency key;
- versioned registry rules must be referenced by ID/version in decisions.

## 2. Asset Care summary

### GET `/v1/assets/{assetId}/care`
Returns the current Care summary.

Response shape:
- assetId
- careStatus
- coverageState
- primaryCoverage
- registrationOpportunity nullable
- nextAttention nullable
- openCases summary
- evidenceGaps
- primaryAction
- lastEvaluatedAt

This is the primary payload for the Digital Twin Care card / overview.

## 3. Evaluate / refresh Care

### POST `/v1/assets/{assetId}/care/evaluate`
Purpose: re-run the current Coverage Engine using latest asset/evidence data and current published registry rules.

Request:
- optional forceReason
- idempotencyKey

Response:
- coverageDecisionId
- coverage records created/updated
- registration opportunities
- confidence
- evidence gaps
- next recommended action

Must not silently delete historical coverage decisions.

## 4. Coverage APIs

### GET `/v1/assets/{assetId}/care/coverage`
Returns all current and historical coverage records appropriate to the caller.

### GET `/v1/assets/{assetId}/care/coverage/{coverageRecordId}`
Returns:
- status;
- dates;
- provider/admin;
- plain-language summary;
- source/proof links the user may view;
- registration details;
- original terms evidence where available.

## 5. Registration opportunity / activation

### GET `/v1/assets/{assetId}/care/registration-opportunities`

### POST `/v1/assets/{assetId}/care/registrations/{opportunityId}/authorise`
Request:
- acceptedAuthorisationTextVersion
- explicitConsent true
- idempotencyKey

Response:
- registrationId
- mode
- status
- nextStep

### POST `/v1/care/registrations/{registrationId}/execute`
Used only after valid authorisation.

Response may be:
- COMPLETED
- WAITING_USER_STEP
- FAILED_RETRYABLE
- FAILED_MANUAL

### POST `/v1/care/registrations/{registrationId}/user-step`
Used for assisted steps such as confirming an OTP result or marking official-site completion.

### GET `/v1/care/registrations/{registrationId}`
Returns audit-safe status and evidence.

## 6. Reminders

### GET `/v1/care/reminders`
Filters:
- assetId
- caseId
- status
- from/to

### POST `/v1/care/reminders/{id}/snooze`
### POST `/v1/care/reminders/{id}/dismiss`
### POST `/v1/care/reminders/{id}/complete`

Reminder creation is generally internal/domain-event driven rather than client-created.

## 7. Report a problem

### POST `/v1/assets/{assetId}/care/issues`
Creates the problem intake and initiates routing evaluation.

Request:
- description
- symptomCodes optional
- errorCode optional
- accidentalDamage nullable
- unsafeSignal nullable
- attachmentIds optional
- idempotencyKey

Response:
- caseDraftId / caseId
- safetyState
- evidenceReadiness
- routingStatus

If unsafe conditions are detected, return a safety interruption state before further diagnostic guidance.

## 8. Routing

### POST `/v1/care/cases/{caseId}/route`
Re-runs route resolution using the current case, coverage and registry state.

Response:
- routingDecisionId
- routeType
- recommendedAction
- reason
- target
- requiredEvidence
- confidence
- alternatives only where verified and genuinely useful

### POST `/v1/care/cases/{caseId}/routing/{routingDecisionId}/accept`
Records user acceptance and progresses the case.

### POST `/v1/care/cases/{caseId}/routing/{routingDecisionId}/request-review`
Creates Care Ops work item when route is uncertain/disputed.

## 9. Cases

### GET `/v1/care/cases`
Filters:
- assetId
- ownerId / workspace
- status
- caseType
- needsAttention

### GET `/v1/care/cases/{caseId}`
Returns:
- case summary;
- asset summary;
- current status;
- counterparty;
- route;
- coverage link;
- next action;
- commitments;
- appointments;
- outcome;
- timeline preview.

### POST `/v1/care/cases/{caseId}/close`
Request:
- outcomeType
- outcome fields
- evidenceIds optional

Must emit the correct domain event and write material outcome back to Digital Twin history.

## 10. Case messages / handoff

### POST `/v1/care/cases/{caseId}/messages/prepare`
Generates a structured draft / case pack.

Request:
- channel
- intendedRecipient / targetEntityId
- requestedResolution optional

Response:
- messageDraftId
- body
- attachments / proof-pack items
- destination
- dataDisclosureSummary

### POST `/v1/care/cases/{caseId}/messages/{messageDraftId}/mark-sent`
Request:
- channel
- sentAt
- externalMessageId nullable
- idempotencyKey

### POST `/v1/care/cases/{caseId}/responses`
Used to record provider response manually or through supported ingestion.

Request:
- text nullable
- attachmentIds optional
- receivedAt
- externalReference nullable

System may derive proposed commitments/references, subject to confirmation where confidence is insufficient.

## 11. External reference

### POST `/v1/care/cases/{caseId}/external-reference`
Request:
- referenceType
- referenceValue
- source
- evidenceId nullable
- idempotencyKey

Do not overwrite previous references. Preserve history.

## 12. Commitments / follow-ups

### POST `/v1/care/cases/{caseId}/commitments`
### PATCH `/v1/care/cases/{caseId}/commitments/{id}`
### POST `/v1/care/cases/{caseId}/commitments/{id}/complete`

### POST `/v1/care/cases/{caseId}/follow-up/prepare`
Returns a proposed follow-up message based on current case history.

No automatic outbound escalation without appropriate user/organisational authority and policy.

## 13. Appointments

### POST `/v1/care/cases/{caseId}/appointments`
### PATCH `/v1/care/cases/{caseId}/appointments/{id}`
### POST `/v1/care/cases/{caseId}/appointments/{id}/complete`

Appointment creation should schedule appropriate reminder events.

## 14. Outcomes

### POST `/v1/care/cases/{caseId}/outcomes/repair`
### POST `/v1/care/cases/{caseId}/outcomes/replacement`
### POST `/v1/care/cases/{caseId}/outcomes/credit`
### POST `/v1/care/cases/{caseId}/outcomes/refund`
### POST `/v1/care/cases/{caseId}/outcomes/rejection`

Do not use one opaque outcome JSON endpoint if it makes validation/auditability weaker.

If a replacement creates a new physical asset, return/accept `replacementAssetId` and create the relationship old asset → replacement asset.

## 15. Proof Pack

### GET `/v1/care/cases/{caseId}/proof-pack`
Response:
- selected owner/contact data;
- asset identity;
- receipt/purchase proof;
- warranty proof;
- issue summary;
- selected evidence;
- sharing scope.

### POST `/v1/care/cases/{caseId}/proof-pack/share`
Future/optional where KADARI directly generates a share link.

Must create an audit record and respect expiry/permissions.

## 16. Registry read APIs

Customer clients should not receive the entire internal Registry.

Internal/business services may use:

### GET `/v1/care/registry/brands`
### GET `/v1/care/registry/brands/{id}`
### GET `/v1/care/registry/product-families`
### GET `/v1/care/registry/service-centres`
### GET `/v1/care/registry/rules/coverage`
### GET `/v1/care/registry/rules/registration`
### GET `/v1/care/registry/rules/routing`

All rule reads must expose version/status/source/freshness to authorised Ops/admin callers.

## 17. Registry admin commands

Prefer explicit publish lifecycle:

### POST `/v1/care/ops/registry/research-jobs`
### POST `/v1/care/ops/registry/rules/coverage/draft`
### POST `/v1/care/ops/registry/rules/registration/draft`
### POST `/v1/care/ops/registry/rules/routing/draft`
### POST `/v1/care/ops/registry/rules/{ruleType}/{logicalRuleId}/publish`
### POST `/v1/care/ops/registry/rules/{ruleType}/{logicalRuleId}/mark-stale`
### POST `/v1/care/ops/registry/rules/{ruleType}/{logicalRuleId}/retire`

Publishing creates a new immutable version.

## 18. Care Ops APIs

### GET `/v1/care/ops/work-items`
Filters:
- type
- severity
- status
- due
- assignedTo

### GET `/v1/care/ops/work-items/{id}`
### POST `/v1/care/ops/work-items/{id}/assign`
### POST `/v1/care/ops/work-items/{id}/resolve`

Resolution request must capture:
- resolution type;
- notes;
- whether reusable knowledge was created;
- resulting registry research/rule refs where applicable.

## 19. Intelligence / extraction service contracts

These may be internal service commands rather than public HTTP endpoints.

### Receipt extraction
Input:
- evidenceId

Output proposal:
- retailer
- purchaseDate
- total / itemPrice where inferable
- product description/SKU
- tax/invoice reference
- confidence per field

### Product-label extraction
Output proposal:
- brand
- model
- serial
- GTIN/barcode
- manufacture/batch info where readable
- confidence per field

### Warranty document interpretation
Output proposal only:
- apparent coverage duration
- registration condition
- exclusions/conditions
- key dates
- source spans
- confidence

Important: interpreted text does not become a published Registry rule without evidence/governance.

### Provider response extraction
Output proposal:
- external reference
- appointment
- promised response date
- evidence request
- rejection reason
- status change
- confidence

## 20. Domain event envelope

Recommended minimum envelope:
- eventId
- eventType
- aggregateType
- aggregateId
- assetId nullable
- tenant/workspaceId
- occurredAt
- actorType
- actorId nullable
- correlationId
- causationId nullable
- schemaVersion
- payload

Events should be durable/outbox-backed if that is consistent with the existing backend architecture.

## 21. Core event payload expectations

### `CoverageEvaluated`
- coverageDecisionId
- assetId
- coverageStates
- registrationOpportunityIds
- confidence

### `WarrantyRegistrationCompleted`
- registrationId
- assetId
- provider
- externalReference
- resultingCoverageEndDate
- evidenceId nullable

### `RoutingDecisionMade`
- routingDecisionId
- caseId
- assetId
- routeType
- targetEntityId nullable
- confidence

### `AssetCaseOpened`
- caseId
- assetId
- caseType
- issueClass
- counterparty nullable

### `CaseCommitmentOverdue`
- commitmentId
- caseId
- assetId
- dueAt

### `RepairCompleted`
- caseId
- assetId
- repairOutcomeId
- coveredByWarranty nullable
- customerCost nullable

### `AssetCaseResolved`
- caseId
- assetId
- outcomeType
- durationSeconds

### `RegistryRulePublished`
- ruleType
- logicalRuleId
- ruleVersionId
- version
- effectiveFrom nullable

## 22. Error semantics

Distinguish at minimum:
- validation error;
- permission/authority error;
- registry unknown;
- registry stale / review required;
- external provider unavailable;
- external provider rejected submission;
- duplicate/idempotent replay;
- manual review required.

Do not collapse external-provider failure into generic 500 errors.

Example domain error codes:
- CARE_ROUTE_UNKNOWN
- CARE_RULE_STALE
- CARE_AUTHORITY_REQUIRED
- CARE_EXTERNAL_UNAVAILABLE
- CARE_REGISTRATION_MANUAL_REQUIRED
- CARE_DUPLICATE_EXTERNAL_ACTION
- CARE_OPS_REVIEW_REQUIRED

## 23. Security / disclosure

Before an outbound communication or proof-pack share:
- validate actor permission;
- validate active authorisation where required;
- compute the minimum disclosure set;
- audit what was shared;
- never expose unrelated asset/household information.

## 24. API compatibility principle

If existing KADARI conventions use CQRS commands, handlers, resource naming or `/v1` patterns differently, preserve those conventions.

This document defines **behavioural contracts and capabilities**, not a requirement to rewrite working backend infrastructure.

## 25. V1 backend definition of done

The backend is ready for the Care V1 customer flow when it can:
1. evaluate Care for an existing asset;
2. persist multiple coverage records;
3. identify and execute an authorised registration workflow;
4. schedule/serve reminders;
5. open a generic AssetCase;
6. resolve and persist a routing decision with its exact rule version;
7. prepare and record an external handoff;
8. retain messages/references/appointments/commitments;
9. record a repair/replacement/credit/rejection outcome;
10. write material outcome to the existing asset history;
11. create Ops work for uncertainty/failure;
12. publish/supersede versioned registry rules without losing historical decision provenance.
