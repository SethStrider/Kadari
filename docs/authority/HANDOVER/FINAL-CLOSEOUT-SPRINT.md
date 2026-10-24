# KADARI — FINAL CLOSEOUT SPRINT

## Purpose
This document closes the current KADARI definition phase and gives Pierre one final execution authority to reconcile privately against his existing AssetSure/KADARI build.

Pierre is not required to move his source code into this repository. This repository is the product/commercial/design authority only. Pierre should pull what he needs, reconcile it against his codebase, implement what is missing on his side, and report back against the acceptance criteria below.

---

# 1. LOCKED PRODUCT POSITION

KADARI creates living digital twins of physical assets and rolls them into an evidence-backed Asset Position.

Core hierarchy:
1. Digital Twin
2. Asset Position
3. Asset Intelligence

Brand line:
**Every asset. Accounted for.**

Core customer value:
**Know what you have. Know what it’s worth. Keep the proof.**

Mathematical signature:
**Σ(V × C)**

V = relevant asset value
C = confidence/evidence factor

Insurance is a major use case, not the whole category.

---

# 2. CUSTOMER FAMILIES

## Personal
Individuals and households managing their own assets.

## Business
Companies managing their own physical asset estate.

Paid Business plans include unlimited users under the same company account. Branches and locations sit under the company/legal entity. Multi-entity groups may require separate commercial treatment.

## Professional
Firms using KADARI for multiple clients, including accountants, auditors, assessors, brokers, valuers, M&A advisers, financiers, repossession agents, auctioneers, liquidators, estate professionals and business-rescue practitioners.

## Enterprise
Large, complex or multi-entity organisations requiring integrations, APIs, advanced security, data controls or custom workflows.

## Partner
Referral, embedded, insurer, bank, distribution or API partners.

---

# 3. REQUIRED EXPERIENCE LAYERS

Pierre should reconcile his existing build against the following experience layers.

## Capture
- camera-first capture
- photo upload fallback
- rapid recognition
- minimal-friction confirmation
- optional enrichment rather than mandatory long forms

## Identity Evidence
After the initial asset scan, KADARI should intelligently encourage the user to capture additional identity evidence appropriate to the asset type.

Examples:
- TV/appliance: model/serial plate
- vehicle: VIN, registration, odometer
- tractor/heavy equipment: manufacturer plate, serial, engine/chassis number, hours
- IT device: serial, IMEI/service tag
- jewellery: hallmark, certificate, inscription
- machinery: nameplate, control panel, hour meter
- tools: manufacturer label, serial, unique markings

The system should not block the user if these are unavailable.

## Notes
A persistent **Add note** action should be available through the asset lifecycle.

Support:
- written note
- voice note
- optional transcription
- timestamp/history

Notes must add context without slowing automated capture.

## Digital Twin
Where relevant, support:
- identity
- media
- serials/identifiers
- documents
- ownership/custody evidence
- location
- condition
- purchase/acquisition details
- valuations
- book value
- replacement value
- market value
- insurance value
- maintenance/service history
- damage history
- warranty
- transfers
- disposal
- status
- timeline
- confidence/evidence state

## Asset Position
Aggregate assets by:
- company/person
- legal entity
- branch/location
- department/cost centre
- category
- custodian
- status
- value type
- evidence confidence

## Asset Intelligence
Surface useful differences, gaps, changes and exceptions rather than just displaying records.

---

# 4. STORYTELLING AUTHORITY

## Personal Story
Retain the emotional logic of the Sarah story:
- assets accumulate gradually
- life changes
- values change
- evidence is often missing when it is suddenly needed
- the physical world is alive while traditional records are static

Core lines remain valid:
- The policy is static. The home is alive.
- Why are we building the evidence after the loss?
- Walk. Scan. Confirm.
- The scan is the input. The intelligence is the product.
- The evidence existed before the event.

## Commercial Story
The business equivalent must show that businesses accumulate assets over years across branches, teams and systems.

Accounting may know the number, insurance may know the schedule, operations may know the equipment, but no one system necessarily knows the complete physical truth.

Core commercial line:
**Your accounting system knows the number. KADARI knows the asset behind it.**

KADARI should connect physical truth with financial truth.

---

# 5. USE CASE AUTHORITY

Pierre should preserve one engine and create context-specific journeys, not separate products.

Priority use cases include:

## Personal
- home contents
- insurance readiness
- claims evidence
- valuables
- jewellery
- electronics
- vehicles
- art/collectibles
- warranties
- moving/storage
- theft evidence
- estate planning
- inheritance
- selling/transferring assets

## Business
- fixed-asset verification
- insurance schedules
- branch/location asset visibility
- tools/equipment custody
- audit preparation
- valuation freshness
- maintenance history
- capex/replacement planning
- disposal
- asset finance support
- multi-site reconciliation

## Professional
- audit verification
- accounting reconciliation
- insurance survey/assessment
- valuation engagement
- business sale/M&A asset schedule
- due diligence
- repossession catalogue
- liquidation inventory
- auction catalogue
- estate inventory
- business-rescue/insolvency asset establishment
- financed-asset monitoring

---

# 6. PROFESSIONAL WORKSPACE

KADARI Professional requires a master firm console with separate client workspaces.

Required principles:
- unlimited firm users
- permissioned client workspaces
- strict client data separation
- cross-client dashboard/search where permission allows
- engagements
- field assignments
- evidence requests
- review/sign-off
- client portal where appropriate
- white-label/co-branded outputs where commercially suitable

## Engagement Mode
Professional users must be able to create a defined engagement such as:
- physical asset verification
- insurance survey
- valuation
- audit support
- repossession
- auction catalogue
- estate inventory
- M&A due diligence

An engagement should contain:
- client
- scope
- locations
- asset categories
- evidence requirements
- assignees
- due date
- progress
- exceptions
- final outputs

## Catalogue Mode
Support rapid cataloguing:
**scan → classify → photograph → describe → value → organise → export**

---

# 7. OUTPUT / EXPORT AUTHORITY

KADARI must not trap customer data inside the application.

Required export/output capability should include, where commercially appropriate:
- PDF
- CSV
- XLSX
- structured API/JSON
- shareable controlled reports
- catalogues

Target outputs include:
- Asset Position report
- Digital Twin record
- Physical Asset Verification Pack
- Audit Support Pack
- Insurance Asset Schedule
- Statement of Values support schedule
- Valuation Schedule
- Transaction Asset Schedule
- Repossession Catalogue
- Auction/Liquidation Catalogue
- Estate Inventory
- Condition Report
- Exception/Reconciliation Report

---

# 8. VALUE ACCELERATORS

KADARI should deliberately deliver high-value software intelligence that does not materially increase marginal COGS.

Priority accelerators:
- book-to-floor reconciliation
- floor-to-book reconciliation
- ghost asset detection
- unrecorded asset detection
- stale evidence alerts
- stale valuation alerts
- condition drift
- location/custody discrepancies
- replacement value gap
- insurance alignment/gap
- warranty expiry
- maintenance evidence
- disposal mismatches
- acquisition matching
- document matching
- portfolio heatmaps
- evidence/confidence completeness
- branch/entity comparison

The guiding principle is to create more useful intelligence from information already captured or connected.

---

# 9. API CONNECTOR FACTORY

KADARI must be designed for enterprise connectivity from the start.

The Connector Factory should support reusable adapters around a canonical KADARI asset model.

Integration families:
- accounting
- ERP
- fixed-asset systems
- CMMS/EAM
- insurance systems
- broker/assessor systems
- banking/asset finance
- valuation systems
- auction/disposal systems
- fleet/telematics
- document systems
- identity/verification where required

Priority named systems include:
- Xero
- QuickBooks
- Sage
- Zoho Books
- Microsoft Dynamics 365
- NetSuite
- SAP
- Odoo

The engineering contract should allow:
- REST APIs
- webhooks
- OAuth/API keys as appropriate
- inbound/outbound sync
- field mapping
- idempotency
- retry handling
- conflict resolution
- audit logging
- permission scopes
- per-connector configuration
- batch import/export
- enterprise custom connectors

Principle:
**Capture once. Reconcile once. Reuse everywhere.**

---

# 10. COMMERCIAL AUTHORITY

## South Africa monthly pricing
- Free: R0
- Personal: R79
- Personal Plus: R159
- Business Start: R399
- Business: R899
- Business Pro: R1,999
- Enterprise: custom
- Professional: separate multi-client pricing architecture

## Global USD monthly pricing
- Free: $0
- Personal: $7.99
- Personal Plus: $14.99
- Business Start: $29
- Business: $59
- Business Pro: $129
- Enterprise: custom

## Storage included
- Free: 1 GB
- Personal: 15 GB
- Personal Plus: 50 GB
- Business Start: 50 GB
- Business: 150 GB
- Business Pro: 500 GB
- Enterprise: custom pooled storage

## Storage add-ons
Indicative:
- +25 GB
- +100 GB
- +500 GB

Exact local/global add-on pricing may be refined during implementation but should preserve strong margins.

## Commercial principles
- allow 10–20% partner commission
- target high recurring gross margins
- separate expensive third-party services from normal subscription economics
- avoid unlimited expensive verification/data-provider events
- use credits, higher tiers or separate charges for high-cost services
- annual pricing may approximate two months free

---

# 11. CONVERSION AUTHORITY

## Primary front-end journeys
- Personal
- Business
- Professional
- Enterprise/Partner

## Try It Now
Visitors should be able to understand the product before committing.

Preferred demo paths:
- live camera scan
- photo upload
- guided interactive demo

The conversion moment should communicate:
**This is the beginning of its Digital Twin. Add more assets and KADARI builds your Asset Position.**

## Primary CTAs
- Try KADARI
- See how it works
- Build my Asset Position
- Build our Asset Position
- Book a business demo
- KADARI Professional
- Partner with KADARI

---

# 12. TRUST / DATA PRINCIPLES

The implementation must clearly support and communicate:
- private by default
- permissioned sharing
- data separation between tenants/clients
- evidence history
- user-controlled sharing
- auditability
- deletion/retention rules
- role-based permissions
- secure API access
- no automatic insurer/bank/third-party access to customer data without an appropriate permission basis

Enterprise security detail should be reconciled with Pierre's underlying architecture and documented back to us.

---

# 13. RECONCILIATION INSTRUCTION TO PIERRE

Pierre should reconcile this authority against his existing build privately.

For every major capability, classify:
- EXISTS / KEEP
- EXISTS / ADAPT
- PARTIAL
- BUILD
- REMOVE / RETIRE
- NOT APPLICABLE

He should return:
1. what already exists
2. what needs adaptation
3. what must be added
4. what he believes conflicts technically with this authority
5. any architecture changes required
6. any missing external dependencies/providers
7. rough sequencing/dependency order
8. questions requiring founder decision

He does not need to publish his source code into this repository.

---

# 14. EXECUTION PRIORITY

Recommended order:

## P0 — Reconcile and protect
- current build mapping
- brand rename/application
- current working functionality preserved
- auth/tenant/security confirmation

## P1 — Commercially complete core
- Personal
- Business
- Digital Twins
- Asset Position
- capture enrichment
- identity evidence prompts
- notes
- storage/tiers
- website/conversion
- Try It Now

## P2 — Business intelligence and integrations
- accounting/ERP connector foundation
- reconciliation logic
- branches/locations
- unlimited business users
- outputs/reports

## P3 — Professional
- multi-client workspaces
- Engagement Mode
- Catalogue Mode
- professional outputs

## P4 — Enterprise/scale
- API/Connector Factory hardening
- SSO/custom security if required
- enterprise APIs
- custom integrations
- high-volume workflows

---

# 15. FINAL ACCEPTANCE JOURNEYS

Before this phase is considered implementation-complete, Pierre should be able to demonstrate at least these five journeys end-to-end:

## A. Personal
landing → try/scan → first Digital Twin → identity evidence → Asset Position → value/evidence → subscription

## B. Business
company setup → branches/locations → unlimited users → assets → accounting import/integration → reconciliation → Asset Position/report

## C. Professional Audit/Accounting
firm workspace → client workspace → engagement → verification assignment → exceptions → export/report

## D. Insurance/Risk Assessment
client engagement → physical capture → values/evidence → schedule/report → client handover/update path

## E. Repossession/Auction
engagement → rapid catalogue → photos/details/condition/value → notes → organise lots/items → export/publish-ready data

---

# 16. WHAT PIERRE SHOULD NOT DO

Do not reduce KADARI back to:
- a home inventory app
- an insurance-only product
- a generic fixed-asset register
- a spreadsheet replacement
- a consumer-only app

Do not create friction-heavy data entry before the user experiences value.

Do not force every asset to have every possible field.

Do not hide expensive services inside low-price plans without COGS control.

Do not create different engines for each industry when the same Digital Twin / Asset Position / Intelligence architecture can serve them.

---

# 17. STATUS AFTER THIS DOCUMENT

From the founder/product side, KADARI is sufficiently defined for Pierre to reconcile and execute.

Any further change should fall into one of four classes:
1. implementation feedback from Pierre
2. a newly identified commercial opportunity
3. a new external dependency/integration
4. a founder-approved new product/add-on

The next step is Pierre reconciliation and feedback, not additional broad ideation.