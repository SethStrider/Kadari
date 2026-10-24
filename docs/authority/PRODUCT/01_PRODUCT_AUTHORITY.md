# KADARI — Product Authority

## What KADARI is
KADARI creates living digital twins of physical assets and rolls them into an evidence-backed Asset Position.

It works for individuals, households, businesses, professional firms and enterprises.

## Core product thesis
KADARI gives physical assets a persistent digital identity and living record.

Each digital twin may contain:
- photographs and video
- category, make, model and identifiers
- serial / VIN / IMEI / other unique identifiers
- purchase evidence
- ownership or custody evidence
- documents and warranties
- condition
- replacement, market and recorded values where appropriate
- valuation history
- maintenance and service history
- damage and repair events
- location and custodian
- insurance information where relevant
- timeline and transfer history
- verification status

## The customer questions
KADARI should help answer:
- What do I have?
- What is it worth?
- Can I prove it exists?
- What condition is it in?
- What evidence do I have?
- Who owns, holds or manages it?
- What happened to it over time?
- How confident can I be in the position being presented?

## Core architecture
**CAPTURE → DIGITAL TWIN → EVIDENCE → VALUE → HISTORY → ASSET POSITION → INTELLIGENCE**

## Camera-first capture
The camera remains a core product behaviour.

The ideal flow is:
1. Scan or photograph an asset.
2. Identify or suggest category / make / model.
3. Capture multiple angles.
4. Read serials, labels, VINs or identifiers where possible.
5. Capture condition baseline.
6. Attach receipts and documents.
7. Establish value where possible.
8. Create the Digital Twin.
9. Add the asset to the Asset Position.

## Proof of Existence
KADARI records that an asset demonstrably existed at a point in time and in a particular state, supported by available evidence.

Proof of existence is not automatically proof of ownership.

## Evidence model
Recommended status model:
- **Declared** — user supplied the information.
- **Evidenced** — supporting evidence exists.
- **Corroborated** — multiple independent evidence points support it.
- **Verified** — a defined verification process confirmed it.

Do not collapse all evidence into one generic green tick.

## Asset Position
All Digital Twins roll up into the customer’s Asset Position.

The Asset Position can show:
- asset count
- recorded value
- replacement value
- market value where appropriate
- insured value where relevant
- evidence completeness
- valuation freshness
- condition freshness
- missing documentation
- assets requiring attention
- custody / location exceptions
- potential insurance gaps

## Mathematical signature
**Σ(V × C)**

Where:
- **V** = value of the asset
- **C** = confidence / evidentiary strength behind that asset record

The principle is not that KADARI discounts the legal or market value of an asset. The formula expresses the confidence-backed portion of an Asset Position.

Potential metric:
- AP = Asset Position
- EAP = Evidenced Asset Position
- Position Confidence = EAP / AP

## Asset Intelligence
KADARI should increasingly interpret the estate rather than merely store it.

Examples:
- book vs physical reconciliation
- ghost assets
- unrecorded assets
- valuation freshness
- replacement-value gap
- insurance gap
- condition drift
- custody discrepancies
- warranty expiry
- maintenance history
- visual comparison
- evidence gaps
- portfolio heatmaps

## Private vs shareable record
KADARI must support a private record and a permissioned shareable record.

The customer controls what is exposed to buyers, insurers, auditors, valuers, advisers or other parties.

## Physical ↔ digital resolution
Architect for:
- QR
- NFC
- RFID
- serials
- VINs
- IMEI
- barcodes / GTIN
- manufacturer identifiers

Avoid a closed identifier architecture that blocks future interoperability.
