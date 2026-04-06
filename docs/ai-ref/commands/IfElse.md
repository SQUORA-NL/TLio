# ifElse

> Evaluates a condition and executes one of two script branches. The condition can be a
> literal boolean, a value path, or a `=function()` expression that returns a truthy node.

## Syntax

```json
{
  "command": "ifElse",
  "condition": <TLioValue>,
  "ifScript":   [ <commands> ],
  "elseScript": [ <commands> ]
}
```

## Options

| Option | Type | Required | Default | Description |
|--------|------|----------|---------|-------------|
| condition | TLioValue | yes | — | Evaluated for truthiness. Use `true`/`false` literals, a path, or `=function()`. |
| ifScript | array | yes | — | Script executed when condition is truthy. |
| elseScript | array | no | — | Script executed when condition is falsy. Omit to do nothing on false. |

**Supports functions**: ✅ (condition only)

## Formats

Works with all adapters. Path syntax differs per adapter — see [overview.md](../overview.md).

## Example

```json
{
  "command": "ifElse",
  "condition": "=fetch($.user.active)",
  "ifScript":   [{ "command": "set", "path": "$.status", "value": "enabled" }],
  "elseScript": [{ "command": "set", "path": "$.status", "value": "disabled" }]
}
```

## C# Fluent API

```csharp
var condition = new FixedValue<JToken>(JValue.FromObject(true));
var script = new TLioScript<JToken>()
    .IfElse(condition)
    .If(new TLioScript<JToken>().Set(JValue.CreateString("enabled")).OnPath("$.status").Build())
    .Else(new TLioScript<JToken>().Set(JValue.CreateString("disabled")).OnPath("$.status").Build());
```
