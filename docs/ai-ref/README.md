# TLio reference

One page per command and per function. Every command and function page ends its examples with a
**JSON / XML / YAML** section — expand the format you work in to see the input, the script and the
result the engine produced. Those sections are generated from the test suite, not typed: see
[Where the examples come from](#where-the-examples-come-from).

**Start here:** [guide.md](guide.md) (task-oriented walkthrough) ·
[overview.md](overview.md) (concepts) · [notation-reference.md](notation-reference.md) (quoting, paths, `=function()` syntax)

## Which format am I in?

| I have | Read | Path language |
|---|---|---|
| JSON (Newtonsoft) | [adapters/json-newtonsoft.md](adapters/json-newtonsoft.md) | JSONPath, `$.a.b[0]` |
| JSON (System.Text.Json) | [adapters/json-systemtext.md](adapters/json-systemtext.md) | JSONPath, `$.a.b[0]` |
| XML | [adapters/xml-slashpath.md](adapters/xml-slashpath.md), [adapters/xml-xpath.md](adapters/xml-xpath.md) | `/root/a/b`, full XPath 1.0 |
| YAML | [adapters/yaml.md](adapters/yaml.md) | dot notation, `$.a.b[0]` |

How the three formats map onto one data model: [adapters/document-shape.md](adapters/document-shape.md).
Writing the *script* itself in JSON, XML or YAML: [adapters/script-notation.md](adapters/script-notation.md).

## Commands

| Group | Commands |
|---|---|
| Write | [add](commands/Add.md) · [set](commands/Set.md) · [put](commands/Put.md) · [remove](commands/Remove.md) · [rename](commands/Rename.md) · [setProperties](commands/SetProperties.md) |
| Move data | [copy](commands/Copy.md) · [move](commands/Move.md) · [merge](commands/Merge.md) |
| Decide | [ifElse](commands/IfElse.md) · [decisionTable](commands/DecisionTable.md) · [compare](commands/Compare.md) |
| Loop (`RegisterLooping`) | [forEach](commands/ForEach.md) · [while](commands/While.md) · [with](commands/With.md) |
| Reshape (ETL pack) | [flatten](commands/Flatten.md) · [restore](commands/Restore.md) · [resolve](commands/Resolve.md) · [toCsv](commands/ToCsv.md) |
| Change format | [convert](commands/Convert.md) (whole document) · [convertValue](commands/ConvertValue.md) (one value) |

Not sure which write command? [add vs set vs put](commands/Add.md#comparison-add-vs-set-vs-put).

## Functions

| Group | Functions |
|---|---|
| Logic and tests | [and](functions/And.md) · [or](functions/Or.md) · [not](functions/Not.md) · [if](functions/If.md) · [coalesce](functions/Coalesce.md) · [between](functions/Between.md) · [in](functions/In.md) · [matches](functions/Matches.md) · [exists](functions/Exists.md) |
| Comparison | [equals](functions/Equals.md) · [notEquals](functions/NotEquals.md) · [greaterThan](functions/GreaterThan.md) · [greaterOrEqual](functions/GreaterOrEqual.md) · [lessThan](functions/LessThan.md) · [lessOrEqual](functions/LessOrEqual.md) |
| Type checks | [isArray](functions/IsArray.md) · [isBoolean](functions/IsBoolean.md) · [isEmpty](functions/IsEmpty.md) · [isNull](functions/IsNull.md) · [isNumber](functions/IsNumber.md) · [isObject](functions/IsObject.md) · [isString](functions/IsString.md) |
| Arithmetic | [sum](functions/Sum.md) · [subtract](functions/Subtract.md) · [multiply](functions/Multiply.md) · [divide](functions/Divide.md) · [modulo](functions/Modulo.md) · [pow](functions/Pow.md) · [sqrt](functions/Sqrt.md) · [abs](functions/Abs.md) · [sign](functions/Sign.md) · [calculate](functions/Calculate.md) |
| Rounding | [round](functions/Round.md) · [ceiling](functions/Ceiling.md) · [floor](functions/Floor.md) · [clamp](functions/Clamp.md) · [toFixed](functions/ToFixed.md) |
| Statistics | [count](functions/Count.md) · [avg](functions/Avg.md) · [min](functions/Min.md) · [max](functions/Max.md) · [median](functions/Median.md) |
| Conditional aggregates | [sumIf](functions/SumIf.md) · [sumIfs](functions/SumIfs.md) · [countIf](functions/CountIf.md) · [countIfs](functions/CountIfs.md) · [averageIf](functions/AverageIf.md) · [averageIfs](functions/AverageIfs.md) · [minIfs](functions/MinIfs.md) · [maxIfs](functions/MaxIfs.md) |
| Text | [concat](functions/Concat.md) · [format](functions/Format.md) · [join](functions/Join.md) · [split](functions/Split.md) · [substring](functions/Substring.md) · [right](functions/Right.md) · [length](functions/Length.md) · [indexOf](functions/IndexOf.md) · [replace](functions/Replace.md) · [toUpper](functions/ToUpper.md) · [toLower](functions/ToLower.md) · [trim](functions/Trim.md) · [trimStart](functions/TrimStart.md) · [trimEnd](functions/TrimEnd.md) · [padLeft](functions/PadLeft.md) · [padRight](functions/PadRight.md) |
| Text tests | [contains](functions/Contains.md) · [startsWith](functions/StartsWith.md) · [endsWith](functions/EndsWith.md) |
| Regular expressions | [regexExtract](functions/RegexExtract.md) · [regexReplace](functions/RegexReplace.md) |
| Dates | [datetime](functions/Datetime.md) · [parseDate](functions/ParseDate.md) · [formatDate](functions/FormatDate.md) · [dateAdd](functions/DateAdd.md) · [dateDiff](functions/DateDiff.md) · [datePart](functions/DatePart.md) · [dateCompare](functions/DateCompare.md) · [isDateBetween](functions/IsDateBetween.md) · [startOfMonth](functions/StartOfMonth.md) · [endOfMonth](functions/EndOfMonth.md) · [minDate](functions/MinDate.md) · [maxDate](functions/MaxDate.md) · [avgDate](functions/AvgDate.md) · [dayCountFraction](functions/DayCountFraction.md) |
| Reading the document | [fetch](functions/Fetch.md) · [indirect](functions/Indirect.md) · [last](functions/Last.md) · [partial](functions/Partial.md) · [path](functions/Path.md) · [resolve](functions/Resolve.md) (lookup by key) · [scriptPath](functions/ScriptPath.md) |
| Shaping values | [toString](functions/ToString.md) · [toArray](functions/ToArray.md) · [parse](functions/Parse.md) · [promote](functions/Promote.md) · [newGuid](functions/NewGuid.md) |
| Arrays | [distinct](functions/Distinct.md) · [sort](functions/Sort.md) · [sortBy](functions/SortBy.md) |
| JSON Schema shape | [orderBySchema](functions/OrderBySchema.md) · [filterBySchema](functions/FilterBySchema.md) · [skeleton](functions/Skeleton.md) |

A function value may also use infix arithmetic — `=round($.principal * $.rate, 2)`;
see [notation-reference.md §3b](notation-reference.md).

## Other references

| Topic | Page |
|---|---|
| Single-file reference for AI agents | [TLio_AI_Reference.md](TLio_AI_Reference.md) |
| Throughput, thread-safety, precompilation | [performance.md](performance.md) |
| Why compiled scripts are safe to share | [concurrency-investigation.md](concurrency-investigation.md) |
| Deliberate cross-format differences | [../behaviour-decisions.md](../behaviour-decisions.md) |
| What changed in each release | [../release-notes/README.md](../release-notes/README.md) |

## Where the examples come from

The "Examples in every format" block on each page is **generated, not hand-written**:

- Each example is either read straight from an existing test fixture (the page names the file) or
  written in `TLio.Parity.Tests/DocExamples/` together with its expected result.
- The script is written out in JSON, XML and YAML notation, and the engine runs *that text* against
  *that document* in each format. The result shown is what came back.
- The JSON result must equal the expected result, or the build fails.

`dotnet test` checks all of it on every change: a page whose examples no longer match the engine
fails `DocsAreUpToDate`, and a command or function with no example fails `EveryPageHasExamples`.
After changing an example or a behaviour, regenerate with

```sh
dotnet test TLio.Parity.Tests --filter "Name~GenerateDocExamples"
```

and review the diff.
