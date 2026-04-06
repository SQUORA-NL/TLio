# Quickstart: JLio API Parity

**Branch**: `008-jlio-api-parity` | **Date**: 2026-04-06

---

## Scenario 1 — JLio Script Runs Unchanged (US1)

A JLio `compare` script using `fromPath`/`toPath` executes on TLio without modification.

```json
[
  { "command": "compare", "fromPath": "$.a", "toPath": "$.b", "resultPath": "$.result" }
]
```

Input:
```json
{ "a": 10, "b": 20 }
```

Expected output:
```json
{ "a": 10, "b": 20, "result": "less" }
```

---

## Scenario 2 — JLio Merge Aliases (US1)

A JLio `merge` script using `fromPath`/`toPath`.

```json
[
  { "command": "merge", "fromPath": "$.patch", "toPath": "$.target" }
]
```

Input:
```json
{ "patch": { "name": "Alice" }, "target": { "id": 1 } }
```

Expected output:
```json
{ "patch": { "name": "Alice" }, "target": { "id": 1, "name": "Alice" } }
```

---

## Scenario 3 — DecisionTable JLio Key (US1)

A JLio `decisionTable` script with config under the `"decisionTable"` key.

```json
[
  {
    "command": "decisionTable",
    "path": "$",
    "decisionTable": {
      "inputs": [{ "name": "status", "path": "$.status" }],
      "outputs": [{ "name": "label", "path": "$.label" }],
      "rules": [
        { "priority": 1, "conditions": { "status": "=active" }, "results": { "label": "Active" } }
      ]
    }
  }
]
```

Input:
```json
{ "status": "active" }
```

Expected output:
```json
{ "status": "active", "label": "Active" }
```

---

## Scenario 4 — ETL Flatten with Settings (US1)

`flattenSettings` configured via JSON (currently broken — this scenario validates the fix).

```json
[
  { "command": "flatten", "path": "$", "flattenSettings": { "delimiter": "_" } }
]
```

Input:
```json
{ "user": { "name": "Alice", "age": 30 } }
```

Expected output:
```json
{ "user_name": "Alice", "user_age": 30 }
```

---

## Scenario 5 — `newGuid` Function (US2)

```json
[
  { "command": "add", "path": "$.id", "value": "=newGuid()" }
]
```

Input: `{}`

Expected output: `{ "id": "xxxxxxxx-xxxx-xxxx-xxxx-xxxxxxxxxxxx" }` (any valid UUID)

---

## Scenario 6 — `fetch` with Default (US2)

```json
[
  { "command": "add", "path": "$.name", "value": "=fetch($.user.name,'Unknown')" }
]
```

Input: `{}`

Expected output: `{ "name": "Unknown" }`

---

## Scenario 7 — `path` Alias (US2)

```json
[
  { "command": "add", "path": "$.items[*].loc", "value": "=path()" }
]
```

Input: `{ "items": [{ "id": 1 }, { "id": 2 }] }`

Expected output: `{ "items": [{ "id": 1, "loc": "$.items[0]" }, { "id": 2, "loc": "$.items[1]" }] }`

---

## Scenario 8 — `promote` with Explicit Name (US2)

```json
[
  { "command": "add", "path": "$.wrapped", "value": "=promote($.rawValue,'data')" }
]
```

Input: `{ "rawValue": 42 }`

Expected output: `{ "rawValue": 42, "wrapped": { "data": 42 } }`

---

## Scenario 9 — Fluent Builder API (US3)

```csharp
var adapter = JsonExecutionContext.Create().NodeAdapter;
var script = new TLioScript<JToken>()
    .Add(JValue.CreateString("hello")).OnPath("$.greeting")
    .Set(new JValue(42)).OnPath("$.count")
    .Remove().OnPath("$.temp");

var json = TLioConvert.Serialize(script);
// Expected: [{"command":"add","path":"$.greeting","value":"hello"},
//            {"command":"set","path":"$.count","value":42},
//            {"command":"remove","path":"$.temp"}]
```

---

## Scenario 10 — Text Pack: concat and toLower (US4)

```json
[
  { "command": "add", "path": "$.fullName", "value": "=concat($.first,' ',$.last)" },
  { "command": "add", "path": "$.emailLower", "value": "=toLower($.email)" }
]
```

Input: `{ "first": "Alice", "last": "Smith", "email": "Alice@Example.COM" }`

Expected output:
```json
{ "first": "Alice", "last": "Smith", "email": "Alice@Example.COM", "fullName": "Alice Smith", "emailLower": "alice@example.com" }
```
