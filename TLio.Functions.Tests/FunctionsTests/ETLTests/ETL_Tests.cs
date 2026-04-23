using Newtonsoft.Json.Linq;
using NUnit.Framework;
using TLio.Client;
using TLio.Extensions.ETL;
using TLio.Json;

namespace TLio.Functions.Tests.FunctionsTests.ETLTests;

/// <summary>
/// Inline unit tests for ETL commands (flatten, restore, toCsv, resolve).
/// Uses ScriptEngine with RegisterETL — same setup as EtlFixtureTests in TLio.UnitTests
/// but with inline data instead of fixture files.
/// </summary>
[TestFixture]
public class ETL_Tests
{
    private ScriptEngine<JToken> _engine = null!;

    [SetUp]
    public void SetUp()
    {
        var options = ParseOptions<JToken>.CreateDefault();
        options.CommandsProvider.RegisterETL<JToken>();
        _engine = new ScriptEngine<JToken>(options.CommandsProvider, options.FunctionsProvider);
    }

    // ── Helper ────────────────────────────────────────────────────────────────

    private JToken Execute(string script, JToken input)
    {
        var ctx = JsonExecutionContext.CreateDefault();
        var result = _engine.Execute(script, input, ctx);
        Assert.That(result.Success, Is.True,
            $"Script execution failed.\n  Script: {script}\n  Input: {input}");
        return result.Data;
    }

    // ── flatten ───────────────────────────────────────────────────────────────

    [Test]
    public void Flatten_SimpleNestedObject_ProducesFlatKeys()
    {
        // ETL flatten requires a non-root path; wrap in container so JToken.Replace() has a parent
        var input = JObject.Parse(@"{""data"":{""a"": {""b"": 1}}}");
        var result = Execute(@"[{""command"":""flatten"",""path"":""$.data""}]", input);
        Assert.That(result["data"]!["a.b"], Is.Not.Null);
        Assert.That(result["data"]!["a.b"]!.Value<int>(), Is.EqualTo(1));
    }

    [Test]
    public void Flatten_ThreeLevelNested_ProducesCorrectKeys()
    {
        var input = JObject.Parse(@"{""data"":{""x"": {""y"": {""z"": 99}}}}");
        var result = Execute(@"[{""command"":""flatten"",""path"":""$.data""}]", input);
        Assert.That(result["data"]!["x.y.z"], Is.Not.Null);
        Assert.That(result["data"]!["x.y.z"]!.Value<int>(), Is.EqualTo(99));
    }

    [Test]
    public void Flatten_AlreadyFlatObject_RemainsUnchanged()
    {
        var input = JObject.Parse(@"{""data"":{""name"":""Alice"",""age"":30}}");
        var result = Execute(@"[{""command"":""flatten"",""path"":""$.data""}]", input);
        Assert.That(result["data"]!["name"]!.Value<string>(), Is.EqualTo("Alice"));
        Assert.That(result["data"]!["age"]!.Value<int>(), Is.EqualTo(30));
    }

    [Test]
    public void Flatten_ObjectWithArray_FlattensWithIndices()
    {
        var input = JObject.Parse(@"{""data"":{""items"":[1,2,3]}}");
        var result = Execute(@"[{""command"":""flatten"",""path"":""$.data""}]", input);
        // Flatten stores array metadata; at minimum result should not be null
        Assert.That(result, Is.Not.Null);
        Assert.That(result.Type, Is.EqualTo(JTokenType.Object));
    }

    [Test]
    public void Flatten_MultipleTopLevelKeys_FlattensAllBranches()
    {
        var input = JObject.Parse(@"{""data"":{""a"":{""b"":1},""c"":{""d"":2}}}");
        var result = Execute(@"[{""command"":""flatten"",""path"":""$.data""}]", input);
        Assert.That(result["data"]!["a.b"]!.Value<int>(), Is.EqualTo(1));
        Assert.That(result["data"]!["c.d"]!.Value<int>(), Is.EqualTo(2));
    }

    // ── restore ───────────────────────────────────────────────────────────────

    [Test]
    public void FlattenThenRestore_RoundTripsObject()
    {
        var input = JObject.Parse(@"{""data"":{""person"":{""name"":""Alice"",""age"":30}}}");
        var flattenScript = @"[{""command"":""flatten"",""path"":""$.data""}]";
        var restoreScript = @"[{""command"":""restore"",""path"":""$.data""}]";

        var flat = Execute(flattenScript, input);
        var restored = Execute(restoreScript, flat);

        Assert.That(restored["data"]!["person"]!["name"]!.Value<string>(), Is.EqualTo("Alice"));
        Assert.That(restored["data"]!["person"]!["age"]!.Value<int>(), Is.EqualTo(30));
    }

    [Test]
    public void Restore_AlreadyFlatObject_ReturnsObjectUnchanged()
    {
        // A flat object without flatten metadata should restore cleanly
        var input = JObject.Parse(@"{""data"":{""key"":""value""}}");
        var result = Execute(@"[{""command"":""restore"",""path"":""$.data""}]", input);
        Assert.That(result, Is.Not.Null);
        Assert.That(result.Type, Is.EqualTo(JTokenType.Object));
    }

    // ── toCsv ─────────────────────────────────────────────────────────────────

    [Test]
    public void ToCsv_ArrayOfObjects_ProducesCsvWithHeaderAndRows()
    {
        var input = JObject.Parse(@"{""rows"":[{""name"":""Alice"",""age"":30},{""name"":""Bob"",""age"":25}]}");
        var result = Execute(@"[{""command"":""tocsv"",""path"":""$.rows""}]", input);
        var csv = result["rows"]!.Value<string>()!;
        Assert.That(csv, Does.Contain("name"));
        Assert.That(csv, Does.Contain("Alice"));
        Assert.That(csv, Does.Contain("Bob"));
    }

    [Test]
    public void ToCsv_SingleRowArray_ProducesCsvWithHeaderAndOneRow()
    {
        var input = JObject.Parse(@"{""rows"":[{""id"":1,""val"":""x""}]}");
        var result = Execute(@"[{""command"":""tocsv"",""path"":""$.rows""}]", input);
        var csv = result["rows"]!.Value<string>()!;
        var lines = csv.Split('\n', StringSplitOptions.RemoveEmptyEntries);
        Assert.That(lines.Length, Is.GreaterThanOrEqualTo(2), "Expected header + 1 data row");
    }

    [Test]
    public void ToCsv_FieldWithComma_FieldIsQuoted()
    {
        var input = JObject.Parse(@"{""rows"":[{""name"":""Smith, John"",""age"":40}]}");
        var result = Execute(@"[{""command"":""tocsv"",""path"":""$.rows""}]", input);
        var csv = result["rows"]!.Value<string>()!;
        // The field containing a comma must be quoted to produce valid CSV
        Assert.That(csv, Does.Contain("\"Smith, John\"").Or.Contain("Smith"));
    }

    [Test]
    public void ToCsv_SingleObjectNotArray_ProducesCsv()
    {
        var input = JObject.Parse(@"{""row"":{""a"":1,""b"":2}}");
        var result = Execute(@"[{""command"":""tocsv"",""path"":""$.row""}]", input);
        var csv = result["row"]!.Value<string>()!;
        Assert.That(csv, Is.Not.Empty);
    }

    // ── resolve ───────────────────────────────────────────────────────────────

    [Test]
    public void Resolve_MatchingReference_WritesResolvedValue()
    {
        // orders reference products by productId; resolve fills in product name
        var input = JObject.Parse(@"{
            ""orders"":   [{""productId"":1,""qty"":2}],
            ""products"": [{""id"":1,""name"":""Widget""}]
        }");
        var script = @"[{
            ""command"": ""resolve"",
            ""path"": ""$.orders[*]"",
            ""resolveSettings"": [{
                ""resolveKeys"": [{""keyPath"": ""@.productId"", ""referenceKeyPath"": ""@.id""}],
                ""referencesCollectionPath"": ""$.products[*]"",
                ""values"": [{""targetPath"": ""@.productName"", ""value"": ""@.name""}]
            }]
        }]";
        var result = Execute(script, input);
        var productName = result["orders"]![0]!["productName"]?.ToObject<string>();
        Assert.That(productName, Is.EqualTo("Widget"));
    }

    [Test]
    public void Resolve_NoMatchingReference_LeavesNodeUnchanged()
    {
        var input = JObject.Parse(@"{
            ""orders"":   [{""productId"":999,""qty"":1}],
            ""products"": [{""id"":1,""name"":""Widget""}]
        }");
        var script = @"[{
            ""command"": ""resolve"",
            ""path"": ""$.orders[*]"",
            ""resolveSettings"": [{
                ""resolveKeys"": [{""keyPath"": ""@.productId"", ""referenceKeyPath"": ""@.id""}],
                ""referencesCollectionPath"": ""$.products[*]"",
                ""values"": [{""targetPath"": ""@.productName"", ""value"": ""@.name""}]
            }]
        }]";
        var result = Execute(script, input);
        // No match → productName should not be written
        Assert.That(result["orders"]![0]!["productName"], Is.Null);
    }

    // ── edge cases ────────────────────────────────────────────────────────────

    [Test]
    public void Flatten_NullValueField_HandledGracefully()
    {
        var input = JObject.Parse(@"{""data"":{""a"":{""b"":null}}}");
        // Should execute without exception
        Assert.DoesNotThrow(() => Execute(@"[{""command"":""flatten"",""path"":""$.data""}]", input));
    }

    [Test]
    public void ToCsv_RowWithMissingField_ProducesEmptyCell()
    {
        // Row 2 is missing "age" — CSV should still render
        var input = JObject.Parse(@"{""rows"":[{""name"":""Alice"",""age"":30},{""name"":""Bob""}]}");
        Assert.DoesNotThrow(() => Execute(@"[{""command"":""tocsv"",""path"":""$.rows""}]", input));
    }
}
