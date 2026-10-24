# KADARI Care — Customer Experience Authority

**Status:** Founder product direction / build authority  
**Date:** 2026-10-06  
**Scope:** Defines the customer-facing experience for KADARI Care as an additive layer on the existing Digital Twin experience.

## 1. Core customer promise

The customer should not need to understand retailer policies, manufacturer warranty systems, distributors, service networks or registration rules.

They should only need to tell KADARI:

- what they bought; and
- what happened.

KADARI should work out the rest.

Primary Care language:

> **Tell KADARI.**

Secondary promise:

> KADARI remembers what is covered, tells you before you lose it, and helps you sort it out when something goes wrong.

## 2. Product boundary

Care is not a separate app or competing asset record.

Care sits inside the existing KADARI Digital Twin.

Recommended Digital Twin top-level surfaces:

- Overview
- Protect
- Care
- Documents
- History

Do not force every asset to display Care with equal prominence. Care prominence should depend on asset type, value, warranty relevance and serviceability.

## 3. Care home / status card

Every Care-eligible Digital Twin should have a compact status card.

Example:

**CARE**

Warranty active  
Ends 14 September 2027  
11 months remaining

Next attention: none

Actions:
- View coverage
- Something's wrong

If additional coverage can still be activated:

**Extra cover available**
Register before 14 October 2026.

Primary CTA:
**Activate extra cover**

If warranty is unknown:

**Coverage not confirmed yet**

CTA:
**Help KADARI work it out**

If expired:

**Manufacturer warranty expired**

Then show other active cover or service options if known. Do not imply the customer has no rights simply because the manufacturer warranty expired.

## 4. New purchase experience

The new-purchase flow should reuse the existing KADARI capture standard.

### Step 1 — Scan the product

Camera-first.

KADARI attempts:
- product category;
- brand;
- model suggestion;
- visible identifiers.

Copy:

> **Let's add this to KADARI.**

### Step 2 — Scan model / serial label

Prompt:

> **Can you find the model or serial label?**

Actions:
- Scan label
- Add manually
- Skip for now

Care should explain why this matters:

> This helps KADARI find the correct warranty and support route later.

### Step 3 — Capture receipt / invoice

Prompt:

> **Add your proof of purchase**

Explain:

> We'll use this to work out purchase date, retailer, price and warranty timing.

Actions:
- Scan receipt
- Upload invoice
- Add later

### Step 4 — Review extracted purchase

Show only important fields:

- retailer;
- purchase date;
- price;
- brand/model;
- serial where captured.

Allow correction.

Do not expose raw OCR complexity.

### Step 5 — Care check

Loading state:

> **Checking your cover...**

KADARI evaluates the Care Registry.

Possible outcomes:

1. warranty found;
2. warranty found + registration opportunity;
3. likely coverage but evidence incomplete;
4. retailer guarantee route;
5. unknown / needs research.

### Step 6 — Result

Example ordinary warranty:

> **You're covered**
> Manufacturer warranty appears active until 14 September 2027.

Example registration opportunity:

> **You can get more cover**
> Register this product before 14 October to unlock additional warranty protection.

Primary CTA:
**Activate it for me**

Secondary:
**Not now**

### Step 7 — Confirmation

When complete:

> **You're set.**
> KADARI will remember the important dates and keep the proof with this asset.

Do not bury the user in legal text. Keep full terms and original source accessible from Coverage Detail.

## 5. Coverage detail

The Coverage Detail page must answer five plain questions:

1. Am I covered?
2. Until when?
3. Who provides the cover?
4. What does it broadly cover?
5. What should I do if something goes wrong?

Recommended structure:

### Status

Active / Expiring / Expired / Unknown / Registration required

### Dates

- purchased;
- warranty start;
- warranty end;
- registration deadline where relevant.

### In plain English

**Usually covered**
- verified manufacturer defects;
- qualifying parts/labour where supported by terms.

**Usually not covered**
- accidental damage;
- misuse;
- unauthorised modification;
- consumables, where excluded.

This summary must be generated from verified source terms and clearly marked as a plain-language summary.

CTA:
**View original terms**

### What to keep

Show evidence KADARI already has:
- receipt ✓
- serial number ✓
- warranty confirmation ✓
- installation certificate if relevant.

Missing evidence should be actionable.

## 6. Warranty activation experience

The customer experience should hide implementation mode unless intervention is required.

Primary CTA:

> **Activate extra cover**

Before action, show:

- benefit;
- deadline;
- information KADARI will submit;
- who receives it.

Consent:

> I authorise KADARI to use the information held on this asset to register this product for the stated warranty/coverage benefit.

Then:

### Automatic

> **Activating your cover...**

Success:

> **Activated**
> Your registration was accepted. Confirmation has been saved to this asset.

### Assisted

KADARI fills what it can, then asks only for the unavoidable step:

> **One step left**
> Bosch requires you to confirm this registration in their site / enter the OTP we send you.

### Guided

> **We'll walk you through it**

KADARI preserves progress and asks the user to confirm when registration is complete.

Never display a successful activation state without external evidence or explicit user-confirmed guided completion.

## 7. Reminder experience

Care reminders must be useful, not noisy.

### Registration deadline

> **Extra warranty still available**
> You have 5 days left to register your Bennett Read appliance for additional cover.

CTA:
**Activate now**

### Warranty 90 days remaining

> **Your warranty ends in 90 days**
> If you've noticed anything unusual, this is a good time to check it rather than waiting until the cover expires.

Actions:
- Everything's fine
- Something's wrong

### Warranty 30 days remaining

> **30 days of warranty left**
> Want to do a quick product check before it expires?

CTA:
**Check my product**

### Maintenance

> **Care due soon**
> Your air conditioner is due for its recommended service next month.

Only send maintenance reminders where KADARI has a verified rule or user-defined schedule.

## 8. Pre-expiry health check

The health check is not a technical inspection unless explicitly supported.

Its purpose is to help a customer notice and record symptoms before cover expires.

Use category-specific simple questions.

Examples for washing machine:
- Any unusual noises?
- Any leaks?
- Any error codes?
- Does it stop mid-cycle?
- Is spinning/draining normal?

Example for TV:
- Any lines, flicker or dark areas?
- Any intermittent power issues?
- Any sound problems?
- Any remote/control issues?

Outcome:

**No issue reported**

or

> **This may be worth reporting before your warranty expires.**

CTA:
**Start a Care case**

Do not claim a product is healthy solely because the customer answered no.

## 9. `Something's wrong` experience

This is the most important Care action.

Primary CTA should be available from the Care card and eligible Digital Twin surfaces:

> **Something's wrong**

### Step 1 — Let the customer speak naturally

Prompt:

> **Tell us what's happening.**

Support:
- text;
- voice;
- photo;
- video;
- error code.

AI may structure the description behind the scenes.

Do not force the customer to diagnose the problem.

### Step 2 — Safety gate

Ask only where relevant:

> **Is there smoke, burning, exposed electricity, leaking gas, severe overheating or another immediate safety concern?**

If yes, show category-appropriate safety instruction and avoid encouraging further operation.

### Step 3 — Physical/accidental damage gate

Where relevant:

> **Was this caused by a drop, impact, liquid spill or another accident?**

This can materially change warranty/insurance routing.

### Step 4 — Assemble evidence automatically

Show:

**KADARI already has**
- receipt ✓
- model ✓
- serial ✓
- purchase date ✓

Then request only missing evidence useful for the case.

### Step 5 — Work out the route

Loading copy:

> **Working out the best next step...**

## 10. Recommended next action

The customer should see **one primary recommendation**, not a directory of choices.

Example retailer-return route:

> **Take this back to Makro**
> This product is normally handled as a retailer warranty-credit item rather than a repair.

Supporting:

> Your proof of purchase and product details are already ready.

Actions:
- Show proof pack
- Open case
- View retailer details

Example OEM route:

> **This should go through LG support**
> Your appliance appears to still be under warranty. KADARI has prepared the receipt, serial number and problem description.

Actions:
- Contact LG
- View what will be sent

Example workshop route:

> **This needs an authorised service centre**
> KADARI found a verified centre for this product category.

Actions:
- View centre
- Prepare proof pack
- Record drop-off

Example unknown route:

> **KADARI hasn't confirmed the correct route yet**
> We won't guess. You can open a case and we'll keep the issue and evidence together while the route is checked.

## 11. Communication handoff

V1 may use customer-controlled channels.

Before handoff:

> **Ready to send**

Show structured preview:
- customer name;
- product/model/serial;
- purchase date;
- case summary;
- requested action;
- attached proof.

Actions may include:
- Open WhatsApp
- Open email
- Open official support page
- Copy case summary

After return to KADARI:

> **Did you send it?**

Actions:
- Yes, record as sent
- Not yet

If sent:

> **Did they give you a reference number?**

Allow scan/paste/manual entry.

## 12. Care Case experience

An open case should feel like a simple live tracker, not enterprise ticketing software.

Header example:

**Washing machine warranty case**
Samsung WW90...

Status:
**Waiting for Samsung**

Next action:
**Follow up after 14:00 tomorrow if no reply**

### Timeline

Human-readable events:

- 6 Oct · Problem reported
- 6 Oct · Warranty checked
- 6 Oct · Message prepared
- 6 Oct · Sent to Samsung
- 6 Oct · Reference 324819 recorded
- 7 Oct · Service agent appointed
- 8 Oct · Visit booked for 10 Oct

Primary action always reflects current state:
- Record reply
- Add reference
- Add appointment
- Follow up
- Add repair outcome
- Close case

## 13. Commitment / follow-up experience

When a provider promises something:

> **They said they'll reply by Friday**

KADARI records a commitment.

When overdue:

> **No response recorded yet**
> Samsung said they would respond by Friday. Want KADARI to prepare a follow-up?

CTA:
**Prepare follow-up**

This is one of the key stickiness behaviours of Care.

## 14. Resolution experience

Closing a case should capture the actual outcome.

Possible outcome types:
- repaired under warranty;
- repaired paid;
- replaced;
- retailer credit;
- refunded;
- rejected;
- no fault found;
- user withdrew;
- other.

Ask:

> **What was the outcome?**

Capture:
- cost paid;
- parts replaced;
- technician/service provider;
- repair warranty if known;
- final documents/photos;
- rejection reason if applicable.

Then:

> **Saved to this asset's history.**

If financial saving is objectively calculable:

> **Covered by warranty: R2,450**

Do not fabricate savings where replacement/repair value is uncertain.

## 15. Rejected case

A rejection should not disappear.

Show:

**Warranty request rejected**

Reason given by provider:
...

KADARI retains:
- rejection message;
- source;
- evidence submitted;
- dates;
- reference;
- next available escalation route if verified.

Potential action:
**Review my options**

Do not claim legal entitlement. Plain-language statutory-right guidance must distinguish verified legal information from warranty terms and should link to authoritative sources where available.

## 16. History integration

Care events that matter must become permanent Digital Twin history events.

Examples:
- warranty activated;
- maintenance completed;
- fault reported;
- repair completed;
- component replaced;
- replacement issued;
- credit/refund;
- recall completed.

The full case remains available, but the main History view should summarise material events rather than show every message.

## 17. Product language rules

Use human language.

Prefer:
- You're covered
- Extra cover available
- Something's wrong
- Take this back to Makro
- This should go through Samsung
- We're waiting for a reply
- Your warranty ends in 30 days

Avoid customer-facing jargon such as:
- RoutingRule
- WarrantyAdministrator
- SLA
- CaseCommitment
- ProductFamily
- Rule confidence
- orchestration

Those may exist internally only.

## 18. Trust and certainty

The UI must distinguish:

### Confirmed
Supported by current source/evidence.

### Likely
KADARI has a strong rule match but some detail is unconfirmed.

### Unknown
KADARI cannot safely determine the answer.

Never use false certainty.

Where necessary:

> **This appears to be covered based on the warranty information KADARI has. The manufacturer will make the final warranty decision.**

## 19. Notification / engagement rule

Care must not become a notification machine.

Prioritise messages that:
- protect money;
- prevent missed cover;
- prevent missed action;
- improve safety;
- resolve an open problem;
- preserve product value.

Suppress low-value reminders for low-risk assets where the notification would create more annoyance than value.

## 20. Care usefulness metric

The customer should be able to see practical value over time.

Potential private summary:

**KADARI Care this year**
- 3 warranties kept active
- 1 extended warranty activated
- 2 service reminders completed
- 1 warranty repair resolved
- R2,450 in verified covered repair value

Only display metrics that can be supported by recorded events.

## 21. Mobile-first acceptance flow

A complete V1 journey should be possible on a phone:

1. photograph product;
2. photograph serial label;
3. photograph receipt;
4. see warranty result;
5. activate extra coverage;
6. receive a reminder later;
7. tap Something's wrong;
8. describe problem by voice;
9. attach photo/video;
10. receive one recommended route;
11. contact provider through customer-controlled channel;
12. record reference;
13. receive follow-up reminder;
14. record repair/replacement result;
15. see permanent history.

No desktop dependency.

## 22. V1 customer acceptance test

A first-time user should be able to answer, without explanation:

- What cover do I have?
- When does it end?
- Can I get more cover?
- Has KADARI registered it?
- What evidence do I already have?
- What do I do if it breaks?
- Who am I waiting for?
- When should I follow up?
- What happened last time?

If those answers are not obvious, the Care UX is not finished.
