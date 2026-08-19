using Microsoft.Extensions.Logging;
using Newtonsoft.Json.Linq;
using NUnit.Framework;
using TLio.Core.Contracts;
using TLio.Extensions.ETL.Commands;
using TLio.Extensions.ETL.Commands.Models;
using TLio.Json;

namespace TLio.UnitTests.CommandsTests.ETLTests;

/// <summary>
/// Flatten collapses a tree into delimited keys; restore rebuilds it from the metadata flatten
/// left behind. The pair is only worth anything if it round-trips, so most of this fixture is
/// round-trip cases over the shapes that are easy to lose: arrays, empty containers, nulls,
/// mixed types, and keys that already contain the delimiter.
/// </summary>
[TestFixture]
public class FlattenRestoreDepthTests
{
    private IExecutionContext<JToken> _context = null!;

    [SetUp]
    public void SetUp() => _context = JsonExecutionContext.CreateDefault();

    private bool Warned() => _context.GetLogEntries().Any(e => e.Level >= LogLevel.Warning);

    /// <summary>Flatten $.item, then restore it, and hand back the rebuilt node.</summary>
    private JToken RoundTrip(string documentJson, FlattenSettings? flatten = null)
    {
        var data = JToken.Parse(documentJson);

        var flattenCmd = new Flatten<JToken> { Path = "$.item" };
        if (flatten != null) flattenCmd.FlattenSettings = flatten;
        Assert.That(flattenCmd.Execute(data, _context).Success, Is.True, "flatten failed");

        var restoreCmd = new Restore<JToken>
        {
            Path = "$.item",
            RestoreSettings = new RestoreSettings { MetadataPath = "$", RemoveMetadata = true }
        };
        Assert.That(restoreCmd.Execute(data, _context).Success, Is.True, "restore failed");

        return data["item"]!;
    }

    private static JObject Flatten(string documentJson, FlattenSettings? settings = null,
        IExecutionContext<JToken>? context = null)
    {
        var data = JToken.Parse(documentJson);
        var cmd = new Flatten<JToken> { Path = "$.item" };
        if (settings != null) cmd.FlattenSettings = settings;
        cmd.Execute(data, context ?? JsonExecutionContext.CreateDefault());
        return (JObject)data["item"]!;
    }

    // ── Round-trip fidelity ───────────────────────────────────────────────────

    [Test]
    public void RoundTrip_NestedObjects()
    {
        var item = RoundTrip("""{"item":{"a":{"b":{"c":"deep"}}}}""");

        Assert.That(item.SelectToken("a.b.c")!.Value<string>(), Is.EqualTo("deep"));
    }

    [Test]
    public void RoundTrip_PreservesScalarTypes()
    {
        var item = RoundTrip(
            """{"item":{"s":"text","i":42,"f":3.5,"b":true,"n":null}}""");

        Assert.That(item["s"]!.Type, Is.EqualTo(JTokenType.String));
        Assert.That(item["i"]!.Type, Is.EqualTo(JTokenType.Integer),
            "an integer must not come back as a string or a float");
        Assert.That(item["f"]!.Value<double>(), Is.EqualTo(3.5));
        Assert.That(item["b"]!.Value<bool>(), Is.True);
        Assert.That(item["n"]!.Type, Is.EqualTo(JTokenType.Null));
    }

    [Test]
    public void RoundTrip_ArrayOfScalars()
    {
        // Regression guard. Restore used to rebuild each element as an object keyed by its own
        // index — ["a","b","c"] came back as [{"0":"a"},{"1":"b"},{"2":"c"}] — because a scalar
        // has no property name after its index, and the trailing index was treated as one.
        var item = RoundTrip("""{"item":{"tags":["a","b","c"]}}""");

        Assert.That(item["tags"]!.Type, Is.EqualTo(JTokenType.Array));
        Assert.That(item["tags"]!.Select(t => t.Value<string>()),
            Is.EqualTo(new[] { "a", "b", "c" }), "values and order both survive");
        Assert.That(item["tags"]![0]!.Type, Is.EqualTo(JTokenType.String),
            "a scalar element stays a scalar, not an object wrapper");
    }

    [Test]
    public void RoundTrip_ArrayOfObjects()
    {
        var item = RoundTrip("""{"item":{"lines":[{"sku":"x","qty":1},{"sku":"y","qty":2}]}}""");

        Assert.That(item.SelectToken("lines[0].sku")!.Value<string>(), Is.EqualTo("x"));
        Assert.That(item.SelectToken("lines[1].qty")!.Value<int>(), Is.EqualTo(2));
        Assert.That(item["lines"]![0]!["sku"], Is.Not.Null,
            "arrays of OBJECTS round-trip correctly — contrast with the scalar-array case");
    }

    [Test]
    public void RoundTrip_NestedArrays()
    {
        var item = RoundTrip("""{"item":{"grid":[[1,2],[3,4]]}}""");

        Assert.That(item["grid"]![0]!.Type, Is.EqualTo(JTokenType.Array),
            "the inner arrays stay arrays");
        Assert.That(item.SelectToken("grid[0][1]")!.Value<int>(), Is.EqualTo(2));
        Assert.That(item.SelectToken("grid[1][0]")!.Value<int>(), Is.EqualTo(3));
    }

    [Test]
    public void RoundTrip_ArrayInsideObjectInsideArray()
    {
        var item = RoundTrip(
            """{"item":{"orders":[{"lines":[{"n":1}]},{"lines":[{"n":2}]}]}}""");

        Assert.That(item.SelectToken("orders[0].lines[0].n")!.Value<int>(), Is.EqualTo(1));
        Assert.That(item.SelectToken("orders[1].lines[0].n")!.Value<int>(), Is.EqualTo(2));
    }

    [Test]
    public void RoundTrip_DeeplyNested()
    {
        var item = RoundTrip("""{"item":{"a":{"b":{"c":{"d":{"e":"bottom"}}}}}}""");

        Assert.That(item.SelectToken("a.b.c.d.e")!.Value<string>(), Is.EqualTo("bottom"));
    }

    [Test]
    public void RoundTrip_KeysContainingSpacesAndPunctuation()
    {
        var item = RoundTrip("""{"item":{"first name":"Ada","e-mail":"a@b.com"}}""");

        Assert.That(item["first name"]!.Value<string>(), Is.EqualTo("Ada"));
        Assert.That(item["e-mail"]!.Value<string>(), Is.EqualTo("a@b.com"));
    }

    [Test]
    public void RoundTrip_ArrayOfScalars_KeepsElementTypes()
    {
        var item = RoundTrip("""{"item":{"vals":[1,2.5,true,null]}}""");

        Assert.That(item["vals"]![0]!.Type, Is.EqualTo(JTokenType.Integer));
        Assert.That(item["vals"]![1]!.Value<double>(), Is.EqualTo(2.5));
        Assert.That(item["vals"]![2]!.Value<bool>(), Is.True);
        Assert.That(item["vals"]![3]!.Type, Is.EqualTo(JTokenType.Null),
            "a null element holds its position rather than collapsing the array");
    }

    [Test]
    public void RoundTrip_ScalarArrayBesideObjectArray()
    {
        // The two array shapes take different branches through the rebuild, so exercise both
        // in one document.
        var item = RoundTrip(
            """{"item":{"orders":[{"lines":[{"n":1}],"codes":["p","q"]}]}}""");

        Assert.That(item.SelectToken("orders[0].lines[0].n")!.Value<int>(), Is.EqualTo(1));
        Assert.That(item.SelectToken("orders[0].codes")!.Select(t => t.Value<string>()),
            Is.EqualTo(new[] { "p", "q" }));
    }

    [Test]
    public void RoundTrip_IsExactForAWholeMixedDocument()
    {
        const string original = """{"a":1,"tags":["x","y"],"o":{"b":2},"grid":[[1],[2]]}""";
        var item = RoundTrip($$"""{"item":{{original}}}""");

        Assert.That(JToken.DeepEquals(item, JToken.Parse(original)), Is.True,
            $"round trip must be lossless. Got: {item.ToString(Newtonsoft.Json.Formatting.None)}");
    }

    // ── Flattened key shape ───────────────────────────────────────────────────

    [Test]
    public void NestedKeysAreJoinedWithTheDelimiter()
    {
        var flat = Flatten("""{"item":{"a":{"b":1}}}""");

        Assert.That(flat["a.b"]!.Value<int>(), Is.EqualTo(1));
    }

    [Test]
    public void ACustomDelimiterIsUsed()
    {
        var flat = Flatten("""{"item":{"a":{"b":1}}}""",
            new FlattenSettings { Delimiter = "__" });

        Assert.That(flat["a__b"], Is.Not.Null, "the configured delimiter replaces the default");
        Assert.That(flat["a.b"], Is.Null);
    }

    [Test]
    public void RoundTrip_WithACustomDelimiter()
    {
        var data = JToken.Parse("""{"item":{"a":{"b":1}}}""");
        var settings = new FlattenSettings { Delimiter = "__" };

        new Flatten<JToken> { Path = "$.item", FlattenSettings = settings }.Execute(data, _context);
        new Restore<JToken>
        {
            Path = "$.item",
            RestoreSettings = new RestoreSettings { MetadataPath = "$", Delimiter = "__" }
        }.Execute(data, _context);

        Assert.That(data.SelectToken("item.a.b")!.Value<int>(), Is.EqualTo(1));
    }

    [Test]
    public void ArrayIndicesAppearInTheFlattenedKeys()
    {
        var flat = Flatten("""{"item":{"tags":["a","b"]}}""");

        Assert.That(flat.Properties().Any(p => p.Name.Contains('0')), Is.True,
            "positions have to survive flattening or the array cannot be rebuilt");
    }

    [Test]
    public void TypeIndicatorsAreAddedWhenTypesArePreserved()
    {
        var flat = Flatten("""{"item":{"count":5}}""",
            new FlattenSettings { PreserveTypes = true });

        Assert.That(flat["count_type"]!.Value<string>(), Is.EqualTo("Integer"));
    }

    [Test]
    public void TypeIndicatorsAreOmittedWhenTypesAreNotPreserved()
    {
        var flat = Flatten("""{"item":{"count":5}}""",
            new FlattenSettings { PreserveTypes = false });

        Assert.That(flat["count_type"], Is.Null);
        Assert.That(flat["count"]!.Value<int>(), Is.EqualTo(5));
    }

    [Test]
    public void ACustomTypeIndicatorSuffixIsUsed()
    {
        var flat = Flatten("""{"item":{"count":5}}""",
            new FlattenSettings { PreserveTypes = true, TypeIndicator = "__t" });

        Assert.That(flat["count__t"]!.Value<string>(), Is.EqualTo("Integer"));
        Assert.That(flat["count_type"], Is.Null);
    }

    // ── MaxDepth ──────────────────────────────────────────────────────────────

    [Test]
    public void MaxDepthStopsTheWalkAndLeavesTheRemainderIntact()
    {
        var flat = Flatten("""{"item":{"a":{"b":{"c":1}}}}""",
            new FlattenSettings { MaxDepth = 1 });

        Assert.That(flat.Properties().Any(p => p.Name == "a.b.c"), Is.False,
            "depth 1 must not walk all the way to c");
    }

    [Test]
    public void UnlimitedDepthIsTheDefault()
    {
        var flat = Flatten("""{"item":{"a":{"b":{"c":1}}}}""");

        Assert.That(flat["a.b.c"]!.Value<int>(), Is.EqualTo(1));
    }

    // ── Metadata ──────────────────────────────────────────────────────────────

    [Test]
    public void FlattenWritesMetadataAtTheConfiguredKey()
    {
        var data = JToken.Parse("""{"item":{"a":{"b":1}}}""");
        new Flatten<JToken>
        {
            Path = "$.item",
            FlattenSettings = new FlattenSettings { MetadataKey = "_meta" }
        }.Execute(data, _context);

        Assert.That(data["_meta"], Is.Not.Null);
        Assert.That(data["_flattenMetadata"], Is.Null);
    }

    [Test]
    public void RestoreRemovesTheMetadataWhenAsked()
    {
        var data = JToken.Parse("""{"item":{"a":{"b":1}}}""");
        new Flatten<JToken> { Path = "$.item" }.Execute(data, _context);
        new Restore<JToken>
        {
            Path = "$.item",
            RestoreSettings = new RestoreSettings { MetadataPath = "$", RemoveMetadata = true }
        }.Execute(data, _context);

        Assert.That(data["_flattenMetadata"], Is.Null);
    }

    [Test]
    public void RestoreKeepsTheMetadataWhenNotAsked()
    {
        var data = JToken.Parse("""{"item":{"a":{"b":1}}}""");
        new Flatten<JToken> { Path = "$.item" }.Execute(data, _context);
        new Restore<JToken>
        {
            Path = "$.item",
            RestoreSettings = new RestoreSettings { MetadataPath = "$", RemoveMetadata = false }
        }.Execute(data, _context);

        Assert.That(data["_flattenMetadata"], Is.Not.Null,
            "keeping it allows a second restore of the same shape");
    }

    // ── Restore without metadata ──────────────────────────────────────────────

    [Test]
    public void RestoreWithoutMetadata_IsBestEffortAndSucceeds()
    {
        var data = JToken.Parse("""{"item":{"a.b":1}}""");

        var result = new Restore<JToken>
        {
            Path = "$.item",
            RestoreSettings = new RestoreSettings { StrictMode = false }
        }.Execute(data, _context);

        Assert.That(result.Success, Is.True);
        Assert.That(data.SelectToken("item.a.b")!.Value<int>(), Is.EqualTo(1),
            "the delimiter alone is enough to rebuild the shape, just not the types");
    }

    [Test]
    public void RestoreWithoutMetadata_FailsInStrictMode()
    {
        var data = JToken.Parse("""{"item":{"a.b":1}}""");

        var result = new Restore<JToken>
        {
            Path = "$.item",
            RestoreSettings = new RestoreSettings { StrictMode = true }
        }.Execute(data, _context);

        Assert.That(result.Success, Is.False, "strict mode is the opt-in to fail loudly");
        Assert.That(Warned(), Is.True);
    }

    [Test]
    public void RestoreWithoutMetadata_RebuildsArraysFromNumericSegments()
    {
        // Without metadata the array-ness is inferred from the keys: a numeric next segment
        // means the container is an array. Types are lost in this mode, the shape is not.
        var data = JToken.Parse("""{"item":{"tags.0":"a","tags.1":"b"}}""");

        var result = new Restore<JToken>
        {
            Path = "$.item",
            RestoreSettings = new RestoreSettings { StrictMode = false }
        }.Execute(data, _context);

        Assert.That(result.Success, Is.True);
        Assert.That(data.SelectToken("item.tags")!.Type, Is.EqualTo(JTokenType.Array));
        Assert.That(data.SelectToken("item.tags")!.Select(t => t.Value<string>()),
            Is.EqualTo(new[] { "a", "b" }));
    }

    // ── Edge shapes ───────────────────────────────────────────────────────────

    [Test]
    public void AnEmptyObjectFlattensWithoutError()
    {
        var data = JToken.Parse("""{"item":{}}""");

        Assert.That(new Flatten<JToken> { Path = "$.item" }.Execute(data, _context).Success, Is.True);
    }

    [Test]
    public void AnEmptyArrayRoundTrips()
    {
        var item = RoundTrip("""{"item":{"tags":[]}}""");

        Assert.That(item["tags"], Is.Not.Null);
    }

    [Test]
    public void AFlatObjectIsUnchangedByFlattening()
    {
        var flat = Flatten("""{"item":{"a":1,"b":"two"}}""",
            new FlattenSettings { PreserveTypes = false });

        Assert.That(flat["a"]!.Value<int>(), Is.EqualTo(1));
        Assert.That(flat["b"]!.Value<string>(), Is.EqualTo("two"));
    }

    [Test]
    public void APathThatMatchesNothing_IsANoOpAndDoesNotThrow()
    {
        var data = JToken.Parse("""{"item":{"a":1}}""");
        var before = data.DeepClone();

        var result = new Flatten<JToken> { Path = "$.nowhere" }.Execute(data, _context);

        Assert.That(result.Success, Is.True);
        Assert.That(JToken.DeepEquals(data, before), Is.True);
    }

    [Test]
    public void FlatteningAScalarDoesNotThrow()
    {
        var data = JToken.Parse("""{"item":42}""");

        Assert.That(() => new Flatten<JToken> { Path = "$.item" }.Execute(data, _context),
            Throws.Nothing);
    }

    [Test]
    public void ANullValueSurvivesTheRoundTrip()
    {
        var item = RoundTrip("""{"item":{"maybe":null}}""");

        Assert.That(item["maybe"], Is.Not.Null, "the key must not vanish");
        Assert.That(item["maybe"]!.Type, Is.EqualTo(JTokenType.Null));
    }

    [Test]
    public void RestoringTwiceIsNotDestructive()
    {
        var data = JToken.Parse("""{"item":{"a":{"b":1}}}""");
        new Flatten<JToken> { Path = "$.item" }.Execute(data, _context);
        var settings = new RestoreSettings { MetadataPath = "$", RemoveMetadata = false };

        new Restore<JToken> { Path = "$.item", RestoreSettings = settings }.Execute(data, _context);
        var afterFirst = data["item"]!.DeepClone();
        new Restore<JToken> { Path = "$.item", RestoreSettings = settings }.Execute(data, _context);

        Assert.That(JToken.DeepEquals(data["item"]!, afterFirst), Is.True,
            "a second restore of an already-restored node changes nothing");
    }
}
