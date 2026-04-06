# Contract: Fluent Builder API

**Project**: `TLio.Client` | **Date**: 2026-04-06

---

## TLioScriptExtensions

Namespace: `TLio.Client`

Extension methods on `TLioScript<TNode>`. All entry points wrap the value in `FixedValue<TNode>` and return an intermediate builder.

```csharp
// Value commands
AddOnPathBuilder<TNode>    Add<TNode>(this TLioScript<TNode> script, TNode value)
ValueOnPathBuilder<TNode>  Set<TNode>(this TLioScript<TNode> script, TNode value)
ValueOnPathBuilder<TNode>  Put<TNode>(this TLioScript<TNode> script, TNode value)

// Remove
RemoveOnPathBuilder<TNode> Remove<TNode>(this TLioScript<TNode> script)

// Copy / Move
CopyMoveFromBuilder<TNode> Copy<TNode>(this TLioScript<TNode> script)
CopyMoveFromBuilder<TNode> Move<TNode>(this TLioScript<TNode> script)

// Compare
CompareFromBuilder<TNode>  Compare<TNode>(this TLioScript<TNode> script)

// Merge
MergeFromBuilder<TNode>    Merge<TNode>(this TLioScript<TNode> script)

// IfElse
IfElseIfBuilder<TNode>     IfElse<TNode>(this TLioScript<TNode> script, IFunctionSupportedValue<TNode> condition)
```

---

## Intermediate Builders

```csharp
// Add / Set / Put
class ValueOnPathBuilder<TNode>
{
    TLioScript<TNode> OnPath(string path)  // adds command, returns script
}

// Remove
class RemoveOnPathBuilder<TNode>
{
    TLioScript<TNode> OnPath(string path)  // adds Remove command, returns script
}

// Copy / Move
class CopyMoveFromBuilder<TNode>
{
    CopyMoveToBuilder<TNode> From(string fromPath)
}
class CopyMoveToBuilder<TNode>
{
    TLioScript<TNode> To(string toPath)    // adds Copy/Move command, returns script
}

// Compare
class CompareFromBuilder<TNode>
{
    CompareToBuilder<TNode> From(string fromPath)
}
class CompareToBuilder<TNode>
{
    CompareResultBuilder<TNode> To(string toPath)
}
class CompareResultBuilder<TNode>
{
    TLioScript<TNode> Result(string resultPath)  // adds Compare command, returns script
}

// Merge
class MergeFromBuilder<TNode>
{
    MergeToBuilder<TNode> From(string fromPath)
}
class MergeToBuilder<TNode>
{
    TLioScript<TNode> To(string toPath)    // adds Merge command, returns script
}

// IfElse
class IfElseIfBuilder<TNode>
{
    IfElseElseBuilder<TNode> If(TLioScript<TNode> ifScript)
}
class IfElseElseBuilder<TNode>
{
    TLioScript<TNode> Else(TLioScript<TNode> elseScript)  // adds IfElse command, returns script
}
```

---

## TLioConvert

Namespace: `TLio.Client`

```csharp
public static class TLioConvert
{
    // Parse a JSON script string into a TLioScript
    public static TLioScript<TNode> Parse<TNode>(
        string scriptJson,
        ParseOptions<TNode> options,
        INodeAdapter<TNode> adapter)

    // Serialize a TLioScript to a JSON string
    public static string Serialize<TNode>(TLioScript<TNode> script)
}
```

**Serialize contract**: Produces a JSON array. Each command element has a `"command"` property and camelCase properties for each public writable property. `string` and `bool` serialize as JSON primitives. `IFunctionSupportedValue<TNode>` serializes via `.ToScript()`. `TLioScript<TNode>` sub-scripts serialize recursively.

---

## Usage Examples

```csharp
// Fluent construction
var script = new TLioScript<TNode>()
    .Add(adapter.CreateString("hello")).OnPath("$.greeting")
    .Set(adapter.CreateNumber(42)).OnPath("$.count")
    .Remove().OnPath("$.temp")
    .Copy().From("$.source").To("$.dest")
    .Compare().From("$.a").To("$.b").Result("$.result")
    .Merge().From("$.patch").To("$.target")
    .IfElse(new FixedValue<TNode>(adapter.CreateBoolean(true)))
        .If(new TLioScript<TNode>().Set(adapter.CreateString("yes")).OnPath("$.x"))
        .Else(new TLioScript<TNode>().Set(adapter.CreateString("no")).OnPath("$.x"));

// Parse from JSON
var parsed = TLioConvert.Parse(scriptJson, ParseOptions<TNode>.CreateDefault(), adapter);

// Serialize to JSON
var json = TLioConvert.Serialize(script);
```
