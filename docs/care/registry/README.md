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

---

# Registration rules

`registration-rules.csv` holds the registrations KADARI can offer ("Extra cover available"). It also ships **empty**:
the workbook's `04_WARRANTY_AUTOMATION.csv` lists leads ("research exact model rules before production"), not rules.

| Column | Required | Values |
|---|---|---|
| `RuleKey` | yes | A stable name, e.g. `bosch-mybosch-major`. |
| `Country` | no | Default `ZA`. |
| `Brand` | yes | Must match `02_BRAND_REGISTRY.csv`. |
| `Category`, `Model` | no | As for coverage rules; narrow the rule. |
| `Mode` | yes | `Guided` (KADARI walks the owner through the official site), `Assisted` (KADARI prepares everything; the owner does the one step only they can), or `Automatic` (runs as Assisted until an OEM integration exists). |
| `DestinationType` | no | `WebPage` (default, must be https), `Email`, `Phone`. |
| `Destination` | yes | The official registration page, address or number. |
| `RequiredFields` | no | `;`-separated: `OwnerName;Email;Mobile;Brand;Model;SerialNumber;PurchaseDate;Retailer;ProofOfPurchase`. Only these are shown to and shared by the owner. |
| `DeadlineDays` | no | Days from purchase to register. |
| `ExtensionMonths` | no | Informational here; the coverage rule's `ExtensionMonths` is what extends the cover once registered. |
| `Benefit` | yes | The sentence the owner reads, e.g. "Register within 28 days to add 2 years to your motor warranty." |
| `Instructions` | no | Steps on the official site. |
| `Confidence`, `SourceTitle`, `SourceUrl`, `LastVerified`, `Status` | as for coverage rules | |

---

# Routing rules

`routing-rules.csv` says where a problem goes ("Something's wrong", CARE/14 C8). It ships **empty** for the same
reason as the coverage rules: the workbook's `03_ROUTING_MODELS.csv` describes the ten kinds of route, not which brand
or retailer uses which. Until it holds researched rows every report reads "a person will find the right place" and
goes to Care Ops — the engine never guesses.

The most specific matching rules win (model → brand + category → brand → retailer + category), then the lowest
`Priority`. If the winners still name different routes, the answer is "research required", never a pick.

| Column | Required | Values |
|---|---|---|
| `RuleKey` | yes | A stable name, e.g. `hisense-small-appliance-fault`. |
| `Country` | no | Default `ZA`. |
| `Brand`, `Category`, `Model`, `Retailer` | as for coverage rules | A rule without a brand needs a retailer and a category. |
| `Coverage` | no | `Any` (default), `InCover`, `OutOfCover`. A cover-dependent rule never applies while the cover is unknown. |
| `IssueClass` | no | `Any` (default), `Fault`, `AccidentalDamage`, `DeadOnArrival`. |
| `RouteType` | yes | `RetailerReturn`, `RetailerCredit`, `OemDirect`, `WarrantyAdmin`, `AuthorisedService`, `Onsite`, `CarryIn`, `Collection`, `ProtectionProvider`. |
| `RoutingModel` | no | A code from `03_ROUTING_MODELS.csv` (`R1`…). |
| `Target` | yes | Who the owner deals with, e.g. "Bosch Home Service". |
| `ChannelType` | yes | `WebPage` (must be https), `Email`, `Phone`, `WhatsApp`, `InStore`. |
| `Channel` | unless `InStore` | The page, address or number. |
| `RequiredEvidence` | no | `;`-separated: `ProofOfPurchase;SerialNumber;FaultPhoto;FaultVideo;ErrorCode;InstallationCertificate`. |
| `Instructions` | yes | What the owner does, in a sentence or two. |
| `Priority` | no | 0–1000, lower wins; default 100. |
| `Confidence`, `SourceTitle`, `SourceUrl`, `LastVerified`, `Status` | as for coverage rules | Only `High`/`Verified`, current rules give a green (go-ahead) route. |
