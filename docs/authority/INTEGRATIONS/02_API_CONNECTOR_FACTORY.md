# KADARI — API Connector Factory

## Status
Required architecture and commercial capability.

KADARI must not become an isolated asset database. It must be able to connect the physical evidence layer to the financial, insurance, operational and transactional systems already used by customers and partners.

## Core principle

**Capture once. Reconcile once. Reuse everywhere.**

The same Digital Twin should be capable of supplying authorised data to multiple downstream systems without re-keying.

## Integration classes

### 1. Accounting / Fixed Assets / ERP
Priority targets:
- Xero
- QuickBooks Online
- Sage
- Zoho Books
- Microsoft Dynamics 365
- NetSuite
- SAP
- Oracle
- Odoo

Core use cases:
- import existing fixed asset register;
- create draft KADARI Digital Twins from financial asset records;
- map acquisition cost, book value and depreciation data;
- reconcile book-to-floor and floor-to-book;
- send approved additions, disposals and transfers back downstream;
- preserve KADARI as the physical evidence layer, not the accounting ledger.

### 2. Audit / Assurance / Professional Services
Capabilities:
- engagement-scoped API access;
- sampled asset lists;
- physical verification results;
- exception status;
- evidence links;
- immutable/auditable event references;
- exportable verification packs.

### 3. Insurance / Broker / Risk Survey
Capabilities:
- ingest policy schedules / statements of values;
- compare insured values with KADARI evidence/value position;
- push approved asset schedules;
- share pre-loss evidence;
- receive renewal/review triggers;
- support assessor/risk-surveyor workflows.

### 4. Banking / Asset Finance / Repossession
Capabilities:
- ingest financed asset schedules;
- create or match Digital Twins;
- periodic condition/location confirmations;
- recovery/repossessed asset intake;
- catalogue-ready export;
- disposal/auction handoff;
- transaction status updates.

### 5. Auction / Disposal / Marketplace Export
KADARI should be able to produce structured outputs containing, where permitted:
- lot/item number;
- title;
- description;
- category;
- make/model/year;
- VIN/serial/asset tag;
- condition;
- quantity;
- value/estimate fields;
- location;
- photo references;
- supporting notes.

Outputs:
- CSV/XLSX;
- JSON;
- REST API;
- webhook event;
- platform-specific export adapters where commercially justified.

### 6. CMMS / EAM / Maintenance
Potential systems:
- IBM Maximo
- SAP EAM
- Dynamics Asset Management
- Fiix
- MaintainX
- UpKeep
- other CMMS/EAM platforms

Use cases:
- asset identity sync;
- maintenance history linkage;
- work order references;
- warranty and manual references;
- condition and inspection updates;
- criticality / status where available.

### 7. Fleet / Telematics / IoT
Potential capabilities:
- VIN/device matching;
- odometer/hours;
- current location where authorised;
- utilisation;
- service triggers;
- condition/telemetry snapshots.

## Connector Factory architecture principles

Every connector must use a common internal canonical asset schema.

Recommended layers:

1. **Connector Adapter** — provider-specific authentication and API translation.
2. **Canonical Mapper** — maps provider fields to/from KADARI schema.
3. **Identity Resolver** — matches records using stable identifiers and confidence rules.
4. **Permission Gate** — enforces tenant, client, engagement and field-level authorisation.
5. **Sync Engine** — import, export, scheduled sync and event-based sync.
6. **Conflict/Reconciliation Engine** — identifies mismatches rather than silently overwriting.
7. **Audit Log** — records source, time, direction and changes.
8. **Webhook/Event Layer** — supports downstream event notifications.
9. **Export Layer** — CSV/XLSX/JSON/PDF/platform-specific outputs.

## Identity matching hierarchy

Where available, match by strong stable identifiers first:
- VIN;
- serial number + manufacturer/model;
- IMEI;
- manufacturer product identifier;
- KADARI asset ID;
- external system asset ID;
- asset tag/barcode/RFID;
- combination of weaker attributes only with confidence and review.

Do not auto-merge ambiguous assets.

## Integration value accelerators

KADARI should surface value from connected systems rather than merely copying data:
- book-to-floor discrepancy;
- floor-to-book discrepancy;
- ghost assets;
- unrecorded assets;
- stale valuations;
- depreciation vs condition/value variance;
- insured vs replacement value gap;
- duplicate asset records;
- missing identifiers;
- incomplete custody/location;
- disposal mismatch;
- acquisition not yet physically evidenced.

## Banking / enterprise requirement

A bank, insurer or enterprise customer must be able to integrate KADARI into its own systems without forcing users to manually re-enter asset data.

Therefore API access is not a future nice-to-have. It is part of the enterprise product architecture.

## Commercial packaging

- Personal: no API requirement.
- Business Start: CSV/XLSX import/export.
- Business: selected native integrations.
- Business Pro: richer integrations + API access subject to limits.
- Professional: client/engagement integrations and exports.
- Enterprise/Partner: custom APIs, webhooks, bulk pipelines, SLA and bespoke connector adapters.

## Governance

A connector may never silently turn third-party data into KADARI-verified evidence.

Every imported field must retain:
- source;
- timestamp;
- confidence/evidence status;
- last sync;
- permission context.
