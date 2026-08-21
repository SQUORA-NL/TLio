# Car insurance rating — Dutch market (two samples)

Two end-to-end samples that turn a **quote request** into a **complete policy setup**:
postcode, vehicle-model and bonus-malus lookups, an age calculation, seven rating factors,
acceptance rules, premium build-up including assurantiebelasting, and the resulting policy
document.

The split is deliberate:

- **`input.json` is only what the insurer receives** — the quote request. No rates, no tables,
  no policy fields.
- **`script.json` carries the rate book**, added by a single `add` command at the top, and
  removed again at the end. Everything after that command only *looks things up* in it.

| | Document model | Scenario |
|---|---|---|
| [`native/`](native/) | The insurer's own request/response shape | 34-year-old in Amsterdam, 2019 Golf, allrisk accepted as requested → **€ 63.77 per month, € 765.24 per year** |
| [`sivi-afd/`](sivi-afd/) | SIVI AFD 2.0-**style** message (entities, code lists) | 23-year-old in Enschede, 2013 Corsa, allrisk **downgraded** to beperkt casco + young-driver clause → **€ 112.13 per quarter, € 448.52 per year** |

Both scripts carry the **same rate book, byte for byte**. Only the paths that address the
document differ, because the message model differs — which is the point of the pair.

## Running them

```sh
dotnet run --project samples/TLio.Sample.Cli -- \
  --input  docs/samples/car-insurance-nl/native/input.json \
  --script docs/samples/car-insurance-nl/native/script.json
```

```sh
dotnet run --project samples/TLio.Sample.Cli -- \
  --input  docs/samples/car-insurance-nl/sivi-afd/input.json \
  --script docs/samples/car-insurance-nl/sivi-afd/script.json
```

The CLI prints compact JSON; the committed `output.json` is the same document, indented.
Both scripts are deterministic — they date everything from `quotedOn` / `messageDate` in the
request rather than `=datetime()`, so a run tomorrow still reproduces `output.json`.

The CLI registers the Math, Text and TimeDate function packs and the ETL command pack
(`resolve`); a host that only calls `ParseOptions.CreateDefault()` will not resolve.

## The rate book in the script

One `add` command, ~235 lines of data, nine tables:

| Table | Rows | Keyed on | Gives |
|---|---|---|---|
| `postcodeZones` | 78 | PC4 | region, zone, `regionFactor` |
| `vehicleModels` | 54 | model code | make, model, fuel, weight class, kW, `waBasePremium`, `cascoRate`, `limitedCascoRate` |
| `bonusMalusLadder` | 20 | BM step | `waFactor`, `cascoFactor`, discount label |
| `coverageTariffs` | 10 | coverage code | description, premium per month |
| `paymentTerms` | 4 | M / K / H / J | months per term, terms per year, `termFactor`, policy costs |
| `excessDiscounts` | 9 | excess amount | `excessFactor` |
| `securityClasses` | 6 | SCM class | `securityFactor` |
| `parkingTypes` | 5 | parking code | `parkingFactor` |
| `usageTypes` | 5 | usage code | `usageFactor` |

Adding a row is a data edit, not a script change — that is what makes this shape worth using.
In a service you would `add` the rate book from your own store instead of inlining it; nothing
below that command changes.

## `description` — scripts can document themselves

A JSON script has no comments, but `CommandConverter` maps known camelCase keys onto command
properties and **silently ignores the rest**, so a `"description"` on any command is carried
through the file and dropped at parse time:

```json
{ "command": "put", "path": "$.calc.driverAge",
  "description": "Age in whole years on the quote date: (20260821 - 19911104) / 10000, floored.",
  "value": "=floor(=calculate(=concat('(',=replace($.request.quotedOn,'-',''),'-',=replace($.request.applicant.birthDate,'-',''),')/10000')))" }
```

Every command in both samples carries one. Two caveats: it is an ignored key, not a feature —
nothing validates or logs it — and `"name"` is **not** free, because `rename` uses it.

## Why the string functions, if this is arithmetic?

Nothing to do with SIVI. Two things about TLio decide it:

**Dates are strings.** In JSON, XML and YAML alike a date is text, and there is no
date-difference function — `dateCompare` answers *which is earlier*, not *how many years*. So
an age is computed from the digits: strip the hyphens, subtract, drop the last four digits.
One command, and correct on the birthday:

```
(20260821 - 19911104) / 10000 = 34.97…  → floor → 34
```

**The Math pack adds but does not multiply.** `sum`, `subtract`, `avg`, `min`, `max` take paths
directly, so additions need no string work at all:

```json
{ "command": "put", "path": "$.calc.netPerMonth", "value": "=round(=sum($.calc.wa,$.calc.casco,$.calc.options),2)" }
{ "command": "put", "path": "$.policy.underwriting.totalExcess", "value": "=sum($.request.cover.voluntaryExcess,$.rates.rules.youngDriverExtraExcess)" }
```

There is no `multiply` or `divide`, so a **product** goes through `=calculate('a*b')`, which
takes one expression string — and `concat` is what builds that string from the fetched values:

```json
{ "command": "put", "path": "$.calc.wa",
  "value": "=round(=calculate(=concat($.calc.lookup.waBasePremium,'*',$.calc.lookup.regionFactor,'*',$.calc.ageFactor,'*',$.calc.mileageFactor,'*',$.calc.lookup.usageFactor,'*',$.calc.lookup.bmWaFactor)),2)" }
```

That leaves exactly five `concat`s per script — the five products — where the first version had
one for every arithmetic step.

## The rating model

Everything is a **monthly** amount until the payment-term step.

```
liability (WA) = waBasePremium × regionFactor × ageFactor × mileageFactor × usageFactor × bmWaFactor
casco          = currentValue × cascoRate / 12 × regionFactor × bmCascoFactor × excessFactor × securityFactor × parkingFactor
beperkt casco  = currentValue × limitedCascoRate / 12 × regionFactor × securityFactor × parkingFactor
options        = Σ tariff of the requested optional coverages
net per month  = liability + casco + options
net per term   = net per month × monthsPerTerm × termFactor
taxable base   = net per term + policy costs
tax            = taxable base × 21%   (assurantiebelasting)
gross per term = taxable base + tax
gross per year = gross per term × termsPerYear
```

Worked out for `native/`: `21.40 × 1.28 × 1.00 × 1.00 × 1.00 × 0.40 = 10.96` liability,
`14200 × 0.052 / 12 × 1.28 × 0.50 × 0.92 × 0.97 × 1.00 = 35.14` casco, `3.25 + 2.10 = 5.35`
options → `51.45` net → `52.70` taxable → `11.07` tax → **€ 63.77 per month**.

## How the script gets there, in eleven steps

1. `add` the rate book.
2. Three `put`s: driver age, licence years, vehicle age.
3. One node — `$.calc.lookup` — collects every lookup key. The native sample fills it with two
   `merge`s from the request plus the PC4 `substring`; the SIVI sample maps AFD field names onto
   the same key names in one object `put`.
4. A `decisionTable` turns claim-free years into a bonus-malus step (20 rules).
5. **One `resolve` with eight settings** fills that node from eight tables at once. Both scripts
   contain this block identically.
6. Three more `decisionTable`s: age band, mileage band, granted cover — ranges (`">=24 && <30"`,
   `"<=10"`) are rule text, so they stay rows rather than nested `ifElse`.
7. One `decisionTable` for acceptance over four inputs; `defaultResults` is the accept path.
8. A second `resolve` prices the requested optional coverages, per array element.
9. Nine `put`s build the premium: one product each for WA and casco, then sums, tax and totals.
10. Object-valued `put`s write whole entities — policyholder, vehicle, risk profile, premium —
    with their `=fetch(...)` strings evaluated inside the object. `ifElse` adds the young-driver
    and cover-downgrade clauses.
11. `remove` drops the working area and the rate book; the native sample `rename`s `request` to
    `quote`, so what remains is the request as received plus the policy it produced.

## Notes and constraints worth knowing

- **`resolve` writes with `"value": "@.field"`** — a value-level relative path, resolved against
  the *matched reference entry*. A `"sourcePath"` key (as used in `docs/showcase/`) is not read
  by the converter and silently writes nothing.
- **`referencesCollectionPath` must select the elements**, i.e. `$.rates.postcodeZones[*]`, not
  `$.rates.postcodeZones`.
- **Function arguments resolve against the document root.** `@.field` inside a function call
  does not mean "this array element", so per-element arithmetic is not available. Both samples
  are shaped around that: per-line amounts come from a lookup (`resolve`), and everything
  computed is computed once on scalar fields and written to a known index.
- **Optional coverages must be present.** `=sum()` over an empty match fails; a request without
  optional coverages needs an `ifElse` guard around that step.
- The SIVI script assumes `coverages[0]` is WA and `coverages[1]` the casco entity — the order of
  the requested coverages is part of the message contract there.

## About the SIVI AFD 2.0 sample

`sivi-afd/` is **shaped after** SIVI AFD 2.0 / AFS: an envelope with a message header, a `policy`
entity holding `parties`, `objects`, `coverages` and `premium` entities, each tagged with
`entityType`, and coded values (`partyRoleCode`, `objectTypeCode`, `coverageCode`,
`premiumFrequencyCode`). The attribute names follow that style but are an **illustrative subset,
not a certified AFD schema** — `riskProfile` and `acceptance` in particular are insurer
extensions, not AFD entities. Map the names onto your own AFD dictionary before using this
against a real trading partner; only the paths change when you do.
