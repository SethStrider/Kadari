# KADARI — Existing Build Reconciliation Pack

## Purpose
Pierre has already built an AssetSure implementation. KADARI now materially broadens and commercialises that product.

**Do not rebuild blindly and do not assume the existing implementation covers the new authority.**

Before further development, reconcile the existing build against this repository.

## Current repository observation
At the time this authority was created, the `RealMrGrey/KADARI` repository contains KADARI product/commercial/design authority but does **not** contain the existing AssetSure application source code.

No separately named AssetSure repository was discoverable through the connected GitHub installation.

Therefore the implementation status below must initially be treated as **UNVERIFIED** until Pierre links, migrates or identifies the existing source.

## Required reconciliation states
For every item classify:

- **EXISTS / KEEP** — already meets KADARI authority.
- **EXISTS / ADAPT** — capability exists but copy, UX, data model or scope must change.
- **PARTIAL** — some capability exists; material work remains.
- **BUILD** — not implemented.
- **REMOVE / RETIRE** — old AssetSure behaviour conflicts with KADARI.
- **UNKNOWN** — not yet inspected.

Each line must record:
- implementation location;
- current status;
- gap;
- action;
- owner;
- proof link / screenshot / route / endpoint;
- completion state.

## Reconciliation matrix

### A. Brand / product identity
- AssetSure → KADARI naming migration
- KADARI visual identity
- `Σ(V × C)` mathematical signature
- `Every asset. Accounted for.`
- personal + business + professional positioning
- Trustli relationship only where relevant

### B. Capture experience
- camera-first capture
- object recognition
- category suggestion
- confidence display
- user confirm/correct
- multi-angle capture
- category-aware prompts
- serial/model/nameplate capture
- VIN / IMEI / hallmark / asset tag capture where relevant
- condition baseline
- optional video
- add document
- add text note
- add voice note / transcription
- skip-and-complete-later pattern

### C. Digital Twin
- stable KADARI asset ID
- identity fields
- evidence/media
- documents
- location
- ownership/custody relationships
- condition
- value fields
- replacement value
- financial fields
- timeline/events
- status
- confidence/evidence model
- private vs shareable view
- source/provenance metadata

### D. Asset Position
- personal Asset Position
- company Asset Position
- multi-location roll-up
- asset count/value summaries
- evidence completeness
- confidence measures
- attention items
- stale valuation/evidence alerts
- value gaps

### E. Personal
- room/location hierarchy
- household categories
- insurance readiness
- claims/pre-loss evidence
- valuables
- warranties/receipts
- estate/transfer use cases

### F. Business
- company/legal entity model
- unlimited users
- roles/permissions
- branches/sites/locations
- departments/cost centres
- custodians
- bulk import/export
- asset assignments
- business reports
- physical verification campaigns

### G. Professional
- master professional firm account
- multiple client workspaces
- client isolation
- unlimited internal firm users
- Engagement Mode
- Catalogue Mode
- white-label/co-branded reports where applicable
- client permissions
- cross-client dashboard
- evidence requests
- field assignments
- review/sign-off

### H. Catalogue / Disposal
- item/lot creation
- AI-assisted description
- quantity/group lots
- condition notes
- value/estimate fields
- photos and ordering
- serial/VIN/model fields
- CSV/XLSX export
- PDF catalogue
- JSON/API output
- platform-specific adapters

### I. Try It Now / conversion
- anonymous or low-friction demo
- live camera or photo upload
- sample recognition
- Digital Twin reveal
- first Asset Position reveal
- conversion to account
- personal/business routing
- mobile-first performance

### J. Commercial / billing
- Free
- Personal
- Personal Plus
- Business Start
- Business
- Business Pro
- Professional tiers
- Enterprise
- storage allowances
- storage add-ons
- USD/global pricing support
- localised currencies/tax presentation
- Mission Partner commission attribution where applicable

### K. Integrations
- canonical asset schema
- accounting connectors
- ERP connectors
- audit/professional outputs
- insurance schedule integration
- banking/finance integration
- auction/disposal export
- CMMS/EAM integration
- webhooks
- API auth/permissions
- reconciliation/conflict handling
- audit logging

### L. Intelligence/value accelerators
- book-to-floor reconciliation
- floor-to-book reconciliation
- ghost asset detection
- unrecorded assets
- duplicate detection
- valuation freshness
- replacement-value gaps
- insurance gap logic
- depreciation vs physical condition context
- condition drift
- next-best evidence action

### M. Security/governance
- tenant isolation
- client isolation
- field-level sharing
- consent/authorisation
- audit trail
- private-by-default
- source attribution
- evidence-state integrity
- export permissions

## Required output from Pierre

Pierre should return one reconciliation document/table with every line above classified.

Then produce:

1. **Existing capability estate** — what already works.
2. **Adaptation estate** — what exists but needs KADARI alignment.
3. **Missing build estate** — what is new.
4. **Retirement estate** — what old AssetSure assumptions should disappear.
5. **Execution sequence** — dependency-aware order.
6. **Proof** — routes, screenshots, endpoints, tests or code references.

## Migration principle
Do not create a parallel KADARI application if the existing AssetSure implementation is the correct technical base.

Prefer:

**existing AssetSure code → reconcile → rename/restructure → KADARI**

unless a technical review proves migration is materially worse than replacement.

## Release gate
KADARI is not commercially complete merely because the original AssetSure application runs.

Release requires reconciliation against:
- product authority;
- brand/copy authority;
- Personal;
- Business;
- Professional;
- conversion/Try It Now;
- pricing/storage;
- integration/API factory;
- industry capture requirements.
