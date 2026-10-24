# KADARI Care — Screen & State Map

**Status:** Build handover companion to `04_KADARI_CARE_CUSTOMER_EXPERIENCE_AUTHORITY.md`  
**Date:** 2026-10-06

## 1. Purpose

This file turns the Care customer-experience authority into a concrete screen/state inventory for implementation.

Care is additive to the existing Digital Twin. Do not create a second asset system.

## 2. Primary surfaces

### C01 — Digital Twin Care card

Purpose: immediate coverage/attention summary.

States:
- ACTIVE
- EXPIRING
- EXPIRED
- UNKNOWN
- REGISTRATION_AVAILABLE
- REGISTRATION_REQUIRED
- CASE_OPEN
- ATTENTION_REQUIRED
- NOT_CARE_RELEVANT

Core display:
- coverage headline;
- expiry / remaining time where meaningful;
- next attention;
- open case indicator;
- primary CTA.

Primary CTAs by state:
- ACTIVE → `View coverage`
- REGISTRATION_AVAILABLE → `Activate extra cover`
- EXPIRING → `Check my product`
- UNKNOWN → `Work out my cover`
- CASE_OPEN → `View case`
- ATTENTION_REQUIRED → action-specific CTA

Always offer `Something's wrong` where Care is applicable.

---

### C02 — Care overview

Sections:
1. coverage status;
2. next important date;
3. active/open case;
4. maintenance due if verified;
5. key proof completeness;
6. Care history preview.

Actions:
- View coverage
- Activate available cover
- Something's wrong
- Add warranty document
- Add receipt
- View Care history

---

### C03 — Coverage detail

Required blocks:
- status;
- start/end dates;
- provider / brand in human language;
- plain-language summary;
- original terms/source;
- registration confirmation;
- proof checklist;
- other protection where known.

Internal-only indicators must not dominate UI:
- rule source;
- confidence;
- last verified;
- rule version.

Expose source where useful under `How KADARI knows this`.

---

### C04 — Registration opportunity

Required:
- extra benefit;
- deadline;
- who receives data;
- data to be submitted;
- explicit authorisation.

Actions:
- Activate extra cover
- Not now

States:
- ELIGIBLE
- AUTHORISED
- SUBMITTING
- WAITING_USER_STEP
- COMPLETED
- FAILED_RETRYABLE
- FAILED_MANUAL
- EXPIRED

---

### C05 — Registration assisted step

Examples:
- enter OTP;
- sign in to OEM site;
- confirm OEM consent;
- press final submit in official flow.

Rule:
Ask for only the external step KADARI cannot perform.

---

### C06 — Registration success

Display:
- activated status;
- effective coverage dates;
- external reference;
- confirmation evidence saved.

Actions:
- Done
- View coverage

---

### C07 — Care reminder detail

Notification deep-link target.

Types:
- REGISTRATION_DEADLINE
- WARRANTY_90
- WARRANTY_30
- WARRANTY_7_OPEN_ISSUE
- HEALTH_CHECK
- MAINTENANCE
- CASE_FOLLOWUP
- PROVIDER_COMMITMENT_OVERDUE
- RECALL

Every reminder requires:
- reason;
- consequence / value;
- direct next action;
- dismiss/snooze where appropriate.

---

### C08 — Pre-expiry health check

Dynamic question set by product category.

Output states:
- NO_ISSUE_REPORTED
- POSSIBLE_ISSUE
- ISSUE_CONFIRMED_BY_USER

Do not create a technical-health claim.

If possible issue:
Primary CTA `Start a Care case`.

---

### C09 — Something's wrong intake

Step 1:
Natural-language / voice issue description.

Step 2:
Optional media:
- photo;
- video;
- audio;
- error code.

Step 3:
Safety gate where relevant.

Step 4:
Accidental/physical damage gate where relevant.

Step 5:
Evidence completeness check.

Step 6:
Route resolution.

User should not select `warranty case` vs `repair case` manually unless ambiguity requires it.

---

### C10 — Safety interruption

Triggered by relevant signals such as:
- smoke/fire;
- gas smell/leak;
- exposed mains electricity;
- severe overheating;
- dangerous fluid leak;
- battery swelling/fire concern.

Display:
- stop-use guidance appropriate to verified safe wording;
- emergency/escalation instruction where required;
- do not encourage diagnostic manipulation.

Then preserve issue and allow case continuation once safe.

---

### C11 — Evidence readiness

Display:

**Already ready**
- receipt;
- serial;
- model;
- purchase date;
- warranty confirmation.

**Still useful**
- requested fault photo;
- error code;
- installation certificate;
- other rule-specific evidence.

Actions:
- Add evidence
- Continue

---

### C12 — Resolving route

Transient state:

> **Working out the best next step...**

Never display raw rule-engine output.

Possible internal outcomes:
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

---

### C13 — Recommended next action

Display exactly one primary recommendation.

Required:
- action headline;
- why;
- what KADARI has prepared;
- what customer must do;
- expected next state if known.

Secondary option may expose `Other options` only when more than one verified route is genuinely valid.

---

### C14 — Case pack preview

Display information that will leave KADARI:
- owner/contact details required;
- product identity;
- serial/model;
- purchase date;
- retailer;
- issue summary;
- evidence attachments;
- requested resolution.

Actions:
- Continue to WhatsApp
- Continue to email
- Open support page
- Copy summary

User must be able to see what is being shared before sharing.

---

### C15 — Post-handoff confirmation

Prompt:

> **Did you send it?**

States/actions:
- SENT
- NOT_SENT
- SEND_FAILED

If sent:
- timestamp;
- channel;
- recipient/channel identity where available.

Then ask for reference number if applicable.

---

### C16 — External reference capture

Methods:
- paste;
- manual entry;
- scan screenshot/message;
- capture from supported inbound integration later.

Examples:
- warranty case number;
- job card number;
- repair reference;
- return authorisation number.

---

### C17 — Open Care Case

Header:
- case title;
- asset;
- current human-readable status.

Status vocabulary should remain simple:
- Preparing
- Sent
- Waiting for provider
- Waiting for you
- Appointment booked
- Product collected
- Being assessed
- Repair underway
- Waiting for part
- Ready / completed
- Rejected
- Resolved

Do not show internal workflow enum names.

---

### C18 — Case timeline

Material events only by default.

Expandable detail for:
- messages;
- attachments;
- internal timestamps;
- provider details.

Each event:
- date/time;
- human-readable event;
- actor;
- optional document/reference.

---

### C19 — Record provider response

Inputs:
- what did they say?;
- screenshot/photo/document;
- new reference;
- promised response date;
- appointment details;
- requested additional evidence.

AI may extract these from a screenshot/message, subject to user confirmation.

---

### C20 — Appointment

Fields:
- service provider;
- date/time/window;
- onsite/drop-off/collection;
- location;
- contact;
- instructions.

Reminder behavior:
- appointment reminder;
- post-appointment outcome prompt.

---

### C21 — Commitment

Use when provider/customer commits to future action.

Examples:
- `Samsung will reply by Friday`
- `Technician will call tomorrow`
- `Customer must upload installation certificate`

Fields:
- actor;
- commitment;
- due date/time;
- completion state.

Overdue → follow-up prompt.

---

### C22 — Follow-up

Display:

> **No response recorded yet**

Show:
- promised deadline;
- last interaction;
- prepared follow-up message.

Actions:
- Send follow-up
- They already replied
- Remind me later

---

### C23 — Repair / resolution outcome

Required outcome selection:
- repaired under warranty;
- paid repair;
- replaced;
- credited;
- refunded;
- rejected;
- no fault found;
- withdrawn;
- other.

Then collect outcome-specific details.

---

### C24 — Repaired outcome

Fields:
- repairer;
- date;
- parts/components replaced;
- customer cost;
- warranty-covered amount if verified;
- repair warranty;
- repair document/job card;
- post-repair photo optional.

Writes material repair event to Digital Twin history.

---

### C25 — Replacement / credit / refund outcome

Fields:
- replacement product if applicable;
- credit/refund value;
- date;
- retailer/OEM;
- supporting evidence.

If replacement creates a new physical asset:
- offer to create new Digital Twin from replacement evidence;
- preserve relationship from old asset → replacement asset.

---

### C26 — Rejection outcome

Fields:
- rejection reason as provided;
- evidence/message;
- date/reference.

Display:
- what provider said;
- what KADARI has on record;
- verified escalation route if available.

Action:
`Review my options`

---

### C27 — Case closed

Display:
- result;
- duration;
- verified cost/saving where available;
- history saved;
- next maintenance/warranty state.

Actions:
- Done
- View asset history

---

### C28 — Care history

Filterable timeline of material Care events:
- registration;
- warranty changes;
- maintenance;
- faults;
- repairs;
- replacements;
- recalls;
- case outcomes.

Do not duplicate every message from Case Timeline here.

---

### C29 — Care value summary

Optional later V1/V1.1 surface.

Display only evidence-backed outcomes:
- warranties activated;
- days/months of extra cover secured;
- warranty repairs resolved;
- verified covered value;
- maintenance actions completed.

This is not a gamified engagement score.

---

## 3. Global state rules

### Empty state

Care-eligible asset with no warranty data:

> **Let's work out your cover**
> Add a receipt or warranty document and KADARI will help work out the important dates and next steps.

### Partial state

KADARI knows brand/product but lacks proof:

> **Likely warranty found**
> Add your proof of purchase so KADARI can work out the dates more confidently.

### Unknown state

> **KADARI hasn't confirmed this yet**
> We won't guess. Add more information or let Care Ops review the route.

### Offline / external failure

Never lose user work.

Store case draft and evidence locally/server-side as available.

Explain external failure separately from KADARI failure.

Example:

> **Samsung's support page isn't responding right now. Your case is saved in KADARI.**

## 4. Navigation rules

- `Something's wrong` must never be more than one obvious action away from an eligible asset.
- Open cases should surface at asset level and in a global `Needs attention` / activity surface if KADARI has one.
- Care must not create a new bottom navigation item unless broader KADARI information architecture requires it.
- Deep links from notifications must land directly on the relevant Care action/state.

## 5. Accessibility / mobile rules

- phone-first;
- camera actions reachable with one hand where practical;
- large primary actions;
- voice input allowed for issue description;
- never require typing a serial if scan succeeds;
- dates expressed clearly, not only countdowns;
- status cannot rely on colour alone;
- plain language before technical terms;
- support older customers who may be carrying a product into a retailer with proof on screen.

## 6. Proof Pack

The Proof Pack is a reusable Care presentation/export object, not a static PDF requirement.

Minimum contents:
- owner identity/contact only as authorised;
- asset/product identity;
- serial/model;
- purchase retailer/date;
- proof of purchase;
- warranty/registration proof;
- current issue summary;
- evidence attachments selected for the case.

Potential presentations:
- on-screen retailer view;
- shareable link;
- PDF/export later;
- email attachments;
- structured OEM payload later.

## 7. Telemetry by screen

Track customer funnel without collecting unnecessary sensitive content.

Minimum events:
- care_viewed;
- coverage_checked;
- registration_offer_shown;
- registration_authorised;
- registration_completed;
- registration_failed;
- reminder_opened;
- health_check_started/completed;
- problem_started;
- evidence_added;
- route_resolved;
- route_unknown;
- case_opened;
- external_handoff_started;
- external_reference_recorded;
- provider_reply_recorded;
- followup_sent;
- case_resolved;
- outcome_type;
- value_saved_verified where applicable.

Do not log warranty documents, messages or sensitive evidence into ordinary analytics payloads.

## 8. Build order by screens

### CX Sprint A — Care foundation
C01, C02, C03, core empty/partial/unknown states.

### CX Sprint B — Activation
C04, C05, C06, C07.

### CX Sprint C — Problem intake and route
C09, C10, C11, C12, C13, C14, C15, C16.

### CX Sprint D — Case memory
C17, C18, C19, C20, C21, C22.

### CX Sprint E — Resolution
C23, C24, C25, C26, C27, C28.

### CX Sprint F — Health / retention
C08, C29 and category-specific reminder refinements.

## 9. UX definition of done

Care UX is complete only when a user can move through the entire journey without needing to understand:
- warranty-administrator terminology;
- distributor relationships;
- service-network ownership;
- internal routing rules;
- internal case status enums;
- how retailer and OEM processes differ.

KADARI carries that complexity.

The customer sees:

> **What is covered?**  
> **What should I do?**  
> **What happens next?**  
> **Who are we waiting for?**  
> **What was the outcome?**
