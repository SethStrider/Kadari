# Care registry: researched coverage rules

`coverage-rules.csv` holds the coverage rules KADARI Care's engine may use. It ships **empty**: the founder's research
workbook (`docs/authority/CARE/REGISTRY/*.csv`) names brands, retailers and route archetypes, but it gives no warranty
lengths, start bases or registration deadlines, and KADARI does not guess them (CARE/16 §2, CARE/19 rule 6). Until
rows are added here, every coverage check answers "KADARI hasn't confirmed this yet".

The migration runner imports this file together with the workbook, through a preview and then an apply of the same
plan (`Kadari.Migration --registry-preview` shows the plan without applying it).

## Columns

| Column | Required | Values |
|---|---|---|
| `RuleKey` | yes | A stable name, e.g. `bosch-major-appliance`. Editing a row and re-importing creates the next **version** of this rule; earlier versions stay, and decisions that used them keep pointing at them. |
| `Country` | no | ISO-3166 alpha-2. Default `ZA`. |
| `Brand` | brand rules | Must match a brand in `02_BRAND_REGISTRY.csv` (case and punctuation ignored). |
| `Category` | no | `MajorAppliance`, `SmallAppliance`, `TvAndAudio`, `Mobile`, `Computing`, `PowerTools`, `PortablePower`, `Other`. |
| `Model` | no | A model code. Makes the rule the most specific (needs `Brand`). |
| `Retailer` | retailer rules | Must match `01_RETAILER_UNIVERSE.csv`. A rule without a brand needs a retailer and a category. |
| `CoverageType` | yes | `Manufacturer`, `RetailerGuarantee`, `Extended`, `ServicePlan`. |
| `DurationMonths` | to publish | Whole months. A rule cannot be `Published` without it. |
| `StartBasis` | no | `PurchaseDate` (default), `DeliveryDate`, `RegistrationDate`. |
| `RegistrationRequired` | no | `yes` / `no`. |
| `RegistrationDeadlineDays` | no | Days from the start basis; only with `RegistrationRequired=yes`. |
| `ExtensionMonths` | no | Extra months that registration unlocks. |
| `Summary` | yes | The plain-language sentence the owner reads. |
| `EffectiveFrom`, `EffectiveTo` | no | `yyyy-MM-dd`, matched against the purchase date. |
| `Confidence` | yes | `Low`, `Medium`, `High`, `Verified`. Only `High` and `Verified` rules can confirm a warranty as active. |
| `SourceTitle` | yes | What the source is, e.g. "Bosch ZA warranty terms". |
| `SourceUrl` | no | The page the rule was read from. |
| `LastVerified` | yes | `yyyy-MM-dd`. A rule not re-verified within a year can only produce "likely". |
| `Status` | no | `Draft` (default), `Published`, `Retired`. Only published rules are used. |
