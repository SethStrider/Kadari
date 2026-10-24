# KADARI — Capture & Evidence Standard

## Status
Founder direction. This file governs how KADARI turns a physical object into a useful Digital Twin without making capture feel like a form-filling exercise.

## Core principle

**Scan first. Enrich intelligently. Never hold up capture unnecessarily.**

KADARI should make it possible to create a useful Digital Twin in seconds, then progressively improve the evidence quality over time.

The product must distinguish between:

1. **Overview evidence** — what the asset is and that it exists.
2. **Identity evidence** — make/model/serial/VIN/IMEI/nameplate/hallmark/asset tag.
3. **Condition evidence** — what state it is in at a point in time.
4. **Documentary evidence** — invoice, receipt, valuation, warranty, certificate, finance/lease document, insurance schedule.
5. **Context evidence** — location, custodian, branch/site, room, installation context.
6. **Human context** — optional written or voice notes.

## Capture flow

### Step 1 — See the asset
User points the camera at the asset.

KADARI should attempt to identify:
- category;
- likely object;
- make/brand;
- model where visually inferable;
- obvious identifying characteristics;
- visible condition;
- confidence.

### Step 2 — Confirm
The user confirms or corrects the suggested identity.

Do not require perfect completeness to continue.

### Step 3 — Improve identity evidence
After the initial capture, KADARI should intelligently prompt:

> **Can you find the model or serial label?**

Examples:
- TV / appliance: rear or side manufacturer label;
- vehicle: VIN plate / licence / odometer where relevant;
- machinery: nameplate, serial plate, asset tag, hour meter;
- electronics: serial / IMEI label or device information;
- jewellery: hallmark, certificate or valuation reference;
- art / collectible: signature, edition number, maker mark, backstamp, provenance label;
- tool/equipment: model plate, serial, asset tag;
- furniture: maker label where available.

This must be **recommended, not blocking**.

The user can choose:
- **Scan label**
- **Add manually**
- **Skip for now**

### Step 4 — Condition capture
KADARI should recommend repeatable useful views based on asset category.

Examples:
- overview/front;
- rear;
- left/right side;
- control panel;
- working end;
- nameplate;
- close-up of visible defects;
- context image showing where a defect sits.

For later inspections, KADARI should guide the user toward matching previous viewpoints where practical so condition changes can be compared.

### Step 5 — Supporting evidence
Offer simple actions:
- Add receipt/invoice
- Add valuation
- Add warranty
- Add certificate
- Add other document
- Add more photos/video
- Add note

Do not present every field as mandatory.

## Add Note

**Add note must remain available throughout the capture and Digital Twin experience.**

Supported modes:
- written note;
- voice note;
- voice-to-text transcription where available.

A note may relate to:
- condition;
- ownership/custody;
- history;
- damage;
- maintenance;
- purchase context;
- missing documentation;
- uncertainty;
- anything the user wants retained with the asset.

Notes are evidence context, not automatically verified facts.

## Progressive completeness

KADARI should progressively show Digital Twin completeness rather than force a long form.

Example:
- Asset identified ✓
- Overview captured ✓
- Identity plate missing
- Purchase evidence missing
- Condition baseline ✓
- Value estimated

CTA:
> **Improve this Digital Twin**

## Confidence and evidence integrity

The system must preserve the distinction between:
- observed;
- user-declared;
- system-inferred;
- evidenced;
- corroborated;
- independently verified.

AI recognition must never silently convert inference into verified fact.

## Industry evidence basis

The capture standard reflects common field practices across equipment registers, facilities management, audit preparation, insurance contents surveying and auction cataloguing: record unique identifiers, manufacturer/model/serial information, location, condition, photographs, maintenance/verification history and source documents; take overview/context and close-up/detail images; preserve notes and exceptions; and export structured records downstream.

## Product outcome

The first scan creates the Digital Twin.

Everything after that increases its **confidence, usefulness and commercial value**.
