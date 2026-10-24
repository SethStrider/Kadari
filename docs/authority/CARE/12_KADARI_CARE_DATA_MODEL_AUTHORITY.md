# KADARI Care — Data Model Authority

**Status:** Build authority  
**Date:** 2026-10-06  
**Scope:** Concrete backend model for adding KADARI Care to the existing KADARI Digital Twin architecture.

## 1. Foundational rule

Care is **not** a second asset system.

All Care records attach to the existing KADARI asset / Digital Twin identity.

The same asset identity must continue to support:
- evidence;
- insurance / protection;
- valuation;
- condition;
- maintenance;
- Care;
- repair;
- sale / transfer;
- replacement history.

## 2. Core aggregate boundaries

### Existing aggregate — Asset / Digital Twin
Care references the existing asset by `assetId`.

Care may add history events to the Digital Twin, but must not duplicate core asset identity.

### CareProfile
One current Care profile per eligible asset.

Fields:
- id
- assetId
- status
- careEligibility
- currentCoverageState
- nextAttentionAt nullable
- openCaseCount
- lastEvaluatedAt nullable
- createdAt
- updatedAt

### CoverageRecord
Represents one identified coverage relationship.

Examples:
- manufacturer warranty;
- retailer guarantee;
- extended protection;
- service plan;
- other evidenced cover.

Fields:
- id
- assetId
- coverageType
- providerEntityId nullable
- administratorEntityId nullable
- coverageRuleVersionId nullable
- status
- startDate nullable
- endDate nullable
- startBasis
- durationMonths nullable
- registrationRequired
- registrationStatus nullable
- registrationDeadline nullable
- policyOrReference nullable
- plainLanguageSummary nullable
- confidence
- sourceEvidenceId nullable
- createdAt
- updatedAt

An asset may have multiple CoverageRecords simultaneously.

### CoverageRegistration
Represents an attempt to activate/register/extend cover.

Fields:
- id
- assetId
- coverageRecordId nullable
- registrationRuleVersionId
- mode
- status
- authorisedByUserId
- authorisedAt
- authorisationTextVersion
- externalDestination
- externalReference nullable
- submittedAt nullable
- completedAt nullable
- failedAt nullable
- failureReason nullable
- resultingCoverageEndDate nullable
- confirmationEvidenceId nullable
- idempotencyKey
- createdAt
- updatedAt

### AssetCase
Generic lifecycle case linked to one asset.

Fields:
- id
- assetId
- ownerId
- caseType
- title
- issueSummary
- issueClass nullable
- safetyClass nullable
- counterpartyType nullable
- counterpartyEntityId nullable
- counterpartyFreeform nullable
- coverageRecordId nullable
- routingDecisionId nullable
- externalReference nullable
- status
- priority
- openedAt
- nextActionAt nullable
- closedAt nullable
- outcomeType nullable
- outcomeNotes nullable
- createdAt
- updatedAt

### CaseMessage
- id
- caseId
- direction
- channel
- actorType
- actorId nullable
- externalParty nullable
- bodyText nullable
- sentAt nullable
- receivedAt nullable
- externalMessageId nullable
- deliveryStatus nullable
- createdAt

### CaseAttachment
- id
- caseId
- evidenceId / fileId
- attachmentType
- sharedExternally boolean
- createdAt

### CaseEvent
Append-only material case event.

Fields:
- id
- caseId
- eventType
- actorType
- actorId nullable
- occurredAt
- payloadJson nullable
- createdAt

### CaseCommitment
Promise or required future action.

Fields:
- id
- caseId
- actorType
- actorId nullable
- description
- dueAt
- status
- completedAt nullable
- sourceCaseMessageId nullable
- createdAt
- updatedAt

### CaseAppointment
- id
- caseId
- appointmentType
- serviceProviderEntityId nullable
- serviceProviderFreeform nullable
- startAt nullable
- endAt nullable
- locationText nullable
- instructions nullable
- status
- createdAt
- updatedAt

### RepairOutcome
- id
- caseId
- assetId
- repairerEntityId nullable
- repairerFreeform nullable
- repairDate nullable
- coveredByWarranty boolean nullable
- customerCost nullable
- coveredAmount nullable
- currency nullable
- repairWarrantyEndDate nullable
- partsJson nullable
- jobCardEvidenceId nullable
- createdAt

### ResolutionOutcome
Used for non-repair outcomes.

Fields:
- id
- caseId
- assetId
- outcomeType
- value nullable
- currency nullable
- replacementAssetId nullable
- evidenceId nullable
- occurredAt
- createdAt

## 3. Registry entities

Registry records must be separate from customer transactional data.

### BrandGroup
- id
- name
- countryOfParent nullable
- active

### Brand
- id
- brandGroupId nullable
- name
- canonicalSlug
- active

### CountryOperator
- id
- brandId nullable
- countryCode
- legalName
- operatorType
- active

### Retailer
- id
- parentGroupName nullable
- name
- countryCode
- website nullable
- active

### WarrantyAdministrator
- id
- name
- countryCode
- parentEntityId nullable
- active

### ServiceNetwork
- id
- name
- countryCode
- operatorEntityId nullable
- active

### ServiceCentre
- id
- serviceNetworkId nullable
- name
- addressJson nullable
- geoPoint nullable
- phone nullable
- email nullable
- onsite boolean nullable
- carryIn boolean nullable
- collection boolean nullable
- active
- lastVerifiedAt nullable

### ProductFamily
- id
- brandId
- name
- categoryCode
- active

### ProductModel
- id
- productFamilyId
- modelCode
- gtin nullable
- sku nullable
- active

### ContactChannel
- id
- entityType
- entityId
- channelType
- value / destination
- purpose
- active
- lastVerifiedAt nullable

## 4. Rule versioning

Rules must be immutable once published.

### CoverageRuleVersion
Fields:
- id
- logicalRuleId
- version
- countryCode
- brandId nullable
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
- status
- supersedesVersionId nullable

### RegistrationRuleVersion
- id
- logicalRuleId
- version
- brandId
- productFamilyId nullable
- productModelId nullable
- mode
- destinationType
- destination
- requiredFieldsJson
- deadlineDays nullable
- benefitDescription
- consentTextRef nullable
- sourceEvidenceId
- lastVerifiedAt
- status
- supersedesVersionId nullable

### RoutingRuleVersion
- id
- logicalRuleId
- version
- countryCode
- brandId nullable
- productFamilyId nullable
- productModelId nullable
- retailerId nullable
- coverageState nullable
- issueClass nullable
- routeType
- targetEntityType nullable
- targetEntityId nullable
- requiredEvidenceJson nullable
- instructions
- priority
- effectiveFrom nullable
- effectiveTo nullable
- confidence
- sourceEvidenceId
- lastVerifiedAt
- status
- supersedesVersionId nullable

### SourceEvidence
- id
- sourceType
- title
- url nullable
- fileId nullable
- publisher / owner nullable
- effectiveDate nullable
- retrievedAt
- checksum nullable
- notes nullable

## 5. Decision records

Never store only the final answer. Store **why** the system made it.

### CoverageDecision
- id
- assetId
- evaluatedAt
- inputSnapshotJson
- matchedCoverageRuleVersionIds
- resultState
- confidence
- decisionExplanation
- engineVersion

### RegistrationOpportunity
- id
- assetId
- coverageDecisionId nullable
- registrationRuleVersionId
- status
- benefitSummary
- deadlineAt nullable
- surfacedAt nullable
- resolvedAt nullable

### RoutingDecision
- id
- assetId
- caseId nullable
- evaluatedAt
- inputSnapshotJson
- matchedRoutingRuleVersionId nullable
- routeType
- targetEntityType nullable
- targetEntityId nullable
- confidence
- decisionExplanation
- engineVersion
- humanOverride boolean
- humanOverrideReason nullable

## 6. Reminder model

### CareReminder
- id
- assetId
- caseId nullable
- reminderType
- dueAt
- status
- sourceType
- sourceId nullable
- snoozedUntil nullable
- deliveredAt nullable
- dismissedAt nullable
- createdAt

Reminder creation must be idempotent.

## 7. Care Ops model

### OpsWorkItem
- id
- workType
- severity
- assetId nullable
- caseId nullable
- registryEntityType nullable
- registryEntityId nullable
- reasonCode
- summary
- status
- assignedTo nullable
- dueAt nullable
- resolutionType nullable
- resolutionNotes nullable
- reusableKnowledge boolean nullable
- createdAt
- resolvedAt nullable

### RegistryResearchJob
- id
- triggerType
- triggerRef nullable
- brandId nullable
- productFamilyId nullable
- productModelId nullable
- retailerId nullable
- question
- priority
- status
- researcherId nullable
- findingsJson nullable
- proposedRuleType nullable
- proposedRulePayload nullable
- sourceEvidenceIds
- createdAt
- completedAt nullable

## 8. Authorisation and audit

### ExternalActionAuthorisation
- id
- userId
- assetId nullable
- caseId nullable
- actionType
- scopeJson
- termsVersion
- grantedAt
- expiresAt nullable
- revokedAt nullable

### ExternalActionAudit
Append-only.

Fields:
- id
- assetId nullable
- caseId nullable
- actionType
- authorisationId nullable
- initiatedByType
- initiatedById nullable
- destination
- requestHash nullable
- requestSummary
- resultStatus
- externalReference nullable
- occurredAt
- metadataJson nullable

## 9. Evidence integrity

Care must reuse KADARI's existing evidence/file model wherever possible.

Do not create duplicate file-storage semantics for:
- receipts;
- warranty PDFs;
- photographs;
- service job cards;
- OEM confirmations;
- screenshots.

Care tables should reference existing evidence/file IDs.

## 10. Required enums

### CoverageState
- UNKNOWN
- LIKELY_ACTIVE
- ACTIVE
- EXPIRING
- EXPIRED
- VOID_OR_INELIGIBLE
- NEEDS_REVIEW

### RegistrationStatus
- NOT_REQUIRED
- AVAILABLE
- AUTHORISED
- SUBMITTING
- WAITING_USER_STEP
- COMPLETED
- FAILED_RETRYABLE
- FAILED_MANUAL
- EXPIRED

### RegistrationMode
- AUTOMATIC
- ASSISTED
- GUIDED

### CaseStatus
- PREPARING
- SENT
- WAITING_PROVIDER
- WAITING_CUSTOMER
- APPOINTMENT_BOOKED
- COLLECTED
- ASSESSING
- REPAIR_UNDERWAY
- WAITING_PART
- READY
- REJECTED
- RESOLVED
- CLOSED_WITHDRAWN

### CaseType
- WARRANTY
- REPAIR
- RETURN
- RECALL
- INSURANCE
- MAINTENANCE
- REPLACEMENT
- OWNERSHIP_TRANSFER
- OTHER

### RouteType
- RETAILER_RETURN
- RETAILER_CREDIT
- OEM_DIRECT
- WARRANTY_ADMIN
- AUTHORISED_SERVICE
- ONSITE
- CARRY_IN
- COLLECTION
- PROTECTION_PROVIDER
- RESEARCH_REQUIRED

### OutcomeType
- REPAIRED_WARRANTY
- REPAIRED_PAID
- REPLACED
- CREDITED
- REFUNDED
- REJECTED
- NO_FAULT_FOUND
- WITHDRAWN
- OTHER

### Confidence
- LOW
- MEDIUM
- HIGH
- VERIFIED

### RuleStatus
- DRAFT
- PUBLISHED
- STALE
- SUPERSEDED
- RETIRED

## 11. Domain events

Care must publish domain events so other KADARI capabilities can react without tight coupling.

Minimum events:
- CareProfileCreated
- CoverageEvaluated
- CoverageActivated
- CoverageExpired
- RegistrationOpportunityFound
- WarrantyRegistrationAuthorised
- WarrantyRegistrationCompleted
- WarrantyRegistrationFailed
- CareReminderScheduled
- CareReminderTriggered
- AssetIssueReported
- RoutingDecisionMade
- AssetCaseOpened
- CaseMessageRecorded
- ExternalReferenceRecorded
- CaseCommitmentCreated
- CaseCommitmentOverdue
- AppointmentRecorded
- RepairCompleted
- AssetReplaced
- CreditOrRefundRecorded
- CaseRejected
- AssetCaseResolved
- RegistryRulePublished
- RegistryRuleSuperseded
- RegistryRuleMarkedStale
- CareOpsWorkItemCreated
- CareOpsWorkItemResolved

Material events should also write an appropriate human-readable event into the asset's existing history/timeline.

## 12. Idempotency requirements

Idempotency is mandatory for:
- registration submissions;
- external case creation;
- outbound provider messages where duplicates matter;
- reminder creation;
- importing registry rules;
- recording external references.

Never allow a retry to create duplicate OEM claims/registrations silently.

## 13. Multi-country readiness

South Africa is the first registry, not a hard-coded market.

Country-sensitive entities/rules must carry `countryCode` or resolve through a country operator.

Do not encode South African warranty logic directly into application code when it belongs in registry rules.

## 14. Data retention / privacy principle

Customer asset and case information is private by default.

Registry rules are operational knowledge and should not contain customer-identifiable information.

External counterparties receive only the minimum data authorised for the case/action.

## 15. Migration / implementation principle

Pierre/Jono should map these concepts onto the existing backend architecture rather than create parallel identity, evidence or audit systems.

Where an existing KADARI entity already satisfies one of these responsibilities, extend/reference it instead of duplicating it.

This document governs **semantic responsibilities**, not table names.
