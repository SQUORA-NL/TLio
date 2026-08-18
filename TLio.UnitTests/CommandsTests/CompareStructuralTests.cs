using Newtonsoft.Json.Linq;
using NUnit.Framework;
using TLio.Client;
using TLio.Commands.Advanced;
using TLio.Commands.Advanced.Models;
using TLio.Commands.Advanced.Settings;
using TLio.Core.Contracts;
using TLio.Core.Models;
using TLio.Json;

namespace TLio.UnitTests.CommandsTests;

/// <summary>
/// Structural (JLio-style) diff behaviour of the Compare command — object property
/// diff, array diff (index- and key-based), result-type filtering and the
/// structured result node written to ResultPath.
/// </summary>
[TestFixture]
public class CompareStructuralTests
{
    private IExecutionContext<JToken> _context = null!;

    [SetUp]
    public void Setup() => _context = JsonExecutionContext.CreateDefault();

    // ── Helpers ───────────────────────────────────────────────────────────────

    private static JArray Results(JToken data)
    {
        var node = data.SelectToken("$.result");
        Assert.That(node, Is.InstanceOf<JArray>(), $"Expected a structured result array but got: {node}");
        return (JArray)node!;
    }

    private static IEnumerable<JToken> OfType(JArray results, string differenceType)
        => results.Where(r => r["differenceType"]?.Value<string>() == differenceType);

    private static bool AnyDifference(JArray results)
        => results.Any(r => r["foundDifference"]?.Value<bool>() == true);

    // ── Object diff ───────────────────────────────────────────────────────────

    [Test]
    public void ObjectDiff_EqualObjects_ReportsNoDifference()
    {
        var data = JToken.Parse("{ \"first\": { \"a\": 1, \"b\": \"x\" }, \"second\": { \"a\": 1, \"b\": \"x\" } }");

        var result = new Compare<JToken>("$.first", "$.second", "$.result").Execute(data, _context);

        Assert.That(result.Success, Is.True);
        var results = Results(data);
        Assert.That(AnyDifference(results), Is.False);
        Assert.That(OfType(results, "noDifference").Count(), Is.EqualTo(1));
    }

    [Test]
    public void ObjectDiff_DifferentPropertyValue_ReportsValueDifference()
    {
        var data = JToken.Parse("{ \"first\": { \"a\": 1, \"b\": \"x\" }, \"second\": { \"a\": 2, \"b\": \"x\" } }");

        new Compare<JToken>("$.first", "$.second", "$.result").Execute(data, _context);

        var results = Results(data);
        var valueDiffs = OfType(results, "valueDifference").ToList();
        Assert.That(valueDiffs, Has.Count.EqualTo(1));
        Assert.That(valueDiffs[0]["firstPath"]?.Value<string>(), Is.EqualTo("$.first.a"));
        Assert.That(valueDiffs[0]["secondPath"]?.Value<string>(), Is.EqualTo("$.second.a"));
        Assert.That(valueDiffs[0]["differenceSubType"]?.Value<string>(), Is.EqualTo("lessThan"));
        Assert.That(valueDiffs[0]["foundDifference"]?.Value<bool>(), Is.True);
    }

    [Test]
    public void ObjectDiff_PropertyOnOneSideOnly_ReportsStructureDifference()
    {
        var data = JToken.Parse("{ \"first\": { \"a\": 1, \"only\": true }, \"second\": { \"a\": 1 } }");

        new Compare<JToken>("$.first", "$.second", "$.result").Execute(data, _context);

        var results = Results(data);
        var structureDiffs = OfType(results, "structureDifference").ToList();
        Assert.That(structureDiffs, Has.Count.EqualTo(1));
        Assert.That(structureDiffs[0]["firstPath"]?.Value<string>(), Is.EqualTo("$.first.only"));
        Assert.That(structureDiffs[0]["foundDifference"]?.Value<bool>(), Is.True);
    }

    [Test]
    public void ObjectDiff_RecursesIntoNestedObjects()
    {
        var data = JToken.Parse(
            "{ \"first\": { \"nested\": { \"deep\": { \"v\": 1 } } }, \"second\": { \"nested\": { \"deep\": { \"v\": 9 } } } }");

        new Compare<JToken>("$.first", "$.second", "$.result").Execute(data, _context);

        var results = Results(data);
        var valueDiffs = OfType(results, "valueDifference").ToList();
        Assert.That(valueDiffs, Has.Count.EqualTo(1));
        Assert.That(valueDiffs[0]["firstPath"]?.Value<string>(), Is.EqualTo("$.first.nested.deep.v"));
    }

    [Test]
    public void TypeDifference_ObjectVersusPrimitive_IsReported()
    {
        var data = JToken.Parse("{ \"first\": { \"a\": 1 }, \"second\": \"text\" }");

        new Compare<JToken>("$.first", "$.second", "$.result").Execute(data, _context);

        var results = Results(data);
        Assert.That(OfType(results, "typeDifference").Count(), Is.EqualTo(1));
        Assert.That(AnyDifference(results), Is.True);
    }

    // ── Array diff — index based ──────────────────────────────────────────────

    [Test]
    public void ArrayDiff_SameCount_ReportsArrayDifferenceWithEqualsSubType()
    {
        var data = JToken.Parse("{ \"first\": [1, 2], \"second\": [1, 3] }");

        new Compare<JToken>("$.first", "$.second", "$.result").Execute(data, _context);

        var results = Results(data);
        var countEntry = OfType(results, "arrayDifference").First();
        Assert.That(countEntry["differenceSubType"]?.Value<string>(), Is.EqualTo("equals"));
        Assert.That(countEntry["foundDifference"]?.Value<bool>(), Is.False);
        Assert.That(countEntry["description"]?.Value<string>(), Does.Contain("2 items"));
    }

    [Test]
    public void ArrayDiff_DifferentCount_ReportsExtraItems()
    {
        var data = JToken.Parse("{ \"first\": [1, 2, 3], \"second\": [1, 2] }");

        new Compare<JToken>("$.first", "$.second", "$.result").Execute(data, _context);

        var results = Results(data);
        var arrayDiffs = OfType(results, "arrayDifference").ToList();
        Assert.That(arrayDiffs.Any(r =>
            r["differenceSubType"]?.Value<string>() == "notEquals" &&
            r["description"]!.Value<string>()!.Contains("different number of items")), Is.True);
        Assert.That(arrayDiffs.Any(r =>
            r["description"]!.Value<string>()!.Contains("first array only")), Is.True);
    }

    [Test]
    public void ArrayDiff_IndexBased_ComparesElementsPositionally()
    {
        var data = JToken.Parse(
            "{ \"first\": [{ \"id\": 1, \"v\": \"a\" }], \"second\": [{ \"id\": 1, \"v\": \"b\" }] }");

        new Compare<JToken>("$.first", "$.second", "$.result").Execute(data, _context);

        var results = Results(data);
        var valueDiffs = OfType(results, "valueDifference").ToList();
        Assert.That(valueDiffs, Has.Count.EqualTo(1));
        Assert.That(valueDiffs[0]["firstPath"]?.Value<string>(), Is.EqualTo("$.first[0].v"));
    }

    // ── Array diff — key based ────────────────────────────────────────────────

    [Test]
    public void ArrayDiff_KeyBased_MatchesReorderedItems()
    {
        var data = JToken.Parse(
            "{ \"first\": [{ \"id\": 1, \"v\": \"a\" }, { \"id\": 2, \"v\": \"b\" }], " +
            "  \"second\": [{ \"id\": 2, \"v\": \"b\" }, { \"id\": 1, \"v\": \"a\" }] }");

        var settings = new CompareSettings
        {
            ArraySettings = { new CompareArraySettings { ArrayPath = "$.first", KeyPaths = { "@.id" } } }
        };

        new Compare<JToken>("$.first", "$.second", "$.result", settings).Execute(data, _context);

        var results = Results(data);
        Assert.That(AnyDifference(results), Is.False,
            $"Expected reordered-but-equal arrays to match. Got: {results}");
    }

    [Test]
    public void ArrayDiff_KeyBased_ReportsDifferencesInsideMatchedItems()
    {
        var data = JToken.Parse(
            "{ \"first\": [{ \"id\": 1, \"v\": \"a\" }], \"second\": [{ \"id\": 1, \"v\": \"z\" }] }");

        var settings = new CompareSettings
        {
            ArraySettings = { new CompareArraySettings { ArrayPath = "$.first", KeyPaths = { "id" } } }
        };

        new Compare<JToken>("$.first", "$.second", "$.result", settings).Execute(data, _context);

        var results = Results(data);
        var valueDiffs = OfType(results, "valueDifference").ToList();
        Assert.That(valueDiffs, Has.Count.EqualTo(1));
        Assert.That(valueDiffs[0]["firstPath"]?.Value<string>(), Is.EqualTo("$.first[0].v"));
    }

    [Test]
    public void ArrayDiff_KeyBased_UnmatchedItemsAreReportedForBothSides()
    {
        var data = JToken.Parse(
            "{ \"first\": [{ \"id\": 1 }], \"second\": [{ \"id\": 2 }] }");

        var settings = new CompareSettings
        {
            ArraySettings = { new CompareArraySettings { ArrayPath = "$.first", KeyPaths = { "id" } } }
        };

        new Compare<JToken>("$.first", "$.second", "$.result", settings).Execute(data, _context);

        var results = Results(data);
        var descriptions = results.Select(r => r["description"]!.Value<string>()!).ToList();
        Assert.That(descriptions.Any(d => d.Contains("first array only")), Is.True);
        Assert.That(descriptions.Any(d => d.Contains("second array only")), Is.True);
    }

    [Test]
    public void ArrayDiff_KeyBased_UniqueIndexMatching_ReportsIndexDifference()
    {
        var data = JToken.Parse(
            "{ \"first\": [{ \"id\": 1 }, { \"id\": 2 }], \"second\": [{ \"id\": 2 }, { \"id\": 1 }] }");

        var settings = new CompareSettings
        {
            ArraySettings =
            {
                new CompareArraySettings
                {
                    ArrayPath = "$.first", KeyPaths = { "id" }, UniqueIndexMatching = true
                }
            }
        };

        new Compare<JToken>("$.first", "$.second", "$.result", settings).Execute(data, _context);

        var results = Results(data);
        var indexDiffs = results
            .Where(r => r["differenceSubType"]?.Value<string>() == "indexDifference")
            .ToList();
        Assert.That(indexDiffs, Has.Count.EqualTo(2));
        Assert.That(indexDiffs.All(r => r["foundDifference"]!.Value<bool>()), Is.True);
    }

    [Test]
    public void ArrayDiff_ArraySettingsMatchOnSecondPathToo()
    {
        var data = JToken.Parse(
            "{ \"first\": [{ \"id\": 1 }, { \"id\": 2 }], \"second\": [{ \"id\": 2 }, { \"id\": 1 }] }");

        var settings = new CompareSettings
        {
            ArraySettings = { new CompareArraySettings { ArrayPath = "$.second", KeyPaths = { "id" } } }
        };

        new Compare<JToken>("$.first", "$.second", "$.result", settings).Execute(data, _context);

        Assert.That(AnyDifference(Results(data)), Is.False);
    }

    // ── Result type filtering ─────────────────────────────────────────────────

    [Test]
    public void ResultTypes_FilterKeepsOnlyRequestedDifferenceTypes()
    {
        var data = JToken.Parse(
            "{ \"first\": { \"a\": 1, \"only\": true }, \"second\": { \"a\": 2 } }");

        var settings = new CompareSettings { ResultTypes = { DifferenceType.StructureDifference } };

        new Compare<JToken>("$.first", "$.second", "$.result", settings).Execute(data, _context);

        var results = Results(data);
        Assert.That(results, Has.Count.EqualTo(1));
        Assert.That(results[0]["differenceType"]?.Value<string>(), Is.EqualTo("structureDifference"));
    }

    [Test]
    public void ResultTypes_EmptyFilterKeepsEverything()
    {
        var data = JToken.Parse("{ \"first\": { \"a\": 1 }, \"second\": { \"a\": 2 } }");

        new Compare<JToken>("$.first", "$.second", "$.result", CompareSettings.CreateDefault())
            .Execute(data, _context);

        Assert.That(Results(data), Is.Not.Empty);
    }

    // ── Result shape / back-compat ────────────────────────────────────────────

    [Test]
    public void StructuredResult_ContainsAllDocumentedProperties()
    {
        var data = JToken.Parse("{ \"first\": { \"a\": 1 }, \"second\": { \"a\": 2 } }");

        new Compare<JToken>("$.first", "$.second", "$.result").Execute(data, _context);

        var entry = Results(data)[0];
        Assert.That(entry["foundDifference"], Is.Not.Null);
        Assert.That(entry["differenceType"], Is.Not.Null);
        Assert.That(entry["differenceSubType"], Is.Not.Null);
        Assert.That(entry["firstPath"], Is.Not.Null);
        Assert.That(entry["secondPath"], Is.Not.Null);
        Assert.That(entry["description"], Is.Not.Null);
    }

    [Test]
    public void Primitives_WithDefaultSettings_StillWriteScalarResult()
    {
        var data = JToken.Parse("{ \"a\": 2, \"b\": 1 }");

        new Compare<JToken>("$.a", "$.b", "$.result").Execute(data, _context);

        Assert.That(data.SelectToken("$.result")?.Value<string>(), Is.EqualTo("greater"));
    }

    [Test]
    public void Primitives_WithExplicitSettings_WriteStructuredResult()
    {
        var data = JToken.Parse("{ \"a\": 2, \"b\": 1 }");
        var settings = new CompareSettings { ResultTypes = { DifferenceType.ValueDifference } };

        new Compare<JToken>("$.a", "$.b", "$.result", settings).Execute(data, _context);

        var results = Results(data);
        Assert.That(results, Has.Count.EqualTo(1));
        Assert.That(results[0]["differenceSubType"]?.Value<string>(), Is.EqualTo("greaterThan"));
    }

    // ── Script parsing + fluent API ───────────────────────────────────────────

    [Test]
    public void CanParseSettingsFromScriptJson()
    {
        var data = JToken.Parse(
            "{ \"first\": [{ \"id\": 1, \"v\": \"a\" }, { \"id\": 2, \"v\": \"b\" }], " +
            "  \"second\": [{ \"id\": 2, \"v\": \"b\" }, { \"id\": 1, \"v\": \"a\" }] }");

        var scriptJson = """
        [{
          "command": "compare",
          "firstPath": "$.first",
          "secondPath": "$.second",
          "resultPath": "$.result",
          "settings": {
            "arraySettings": [{ "arrayPath": "$.first", "keyPaths": ["@.id"], "uniqueIndexMatching": false }],
            "resultTypes": ["valueDifference", "structureDifference"]
          }
        }]
        """;

        var options = ParseOptions<JToken>.CreateDefault();
        var script = new CommandConverter<JToken>(
            options.CommandsProvider, options.FunctionsProvider, new JsonNodeAdapter()).ParseScript(scriptJson);

        var compare = (Compare<JToken>)script[0];
        Assert.That(compare.Settings, Is.Not.Null);
        Assert.That(compare.Settings!.ArraySettings, Has.Count.EqualTo(1));
        Assert.That(compare.Settings.ArraySettings[0].KeyPaths, Is.EqualTo(new[] { "@.id" }));
        Assert.That(compare.Settings.ResultTypes,
            Is.EqualTo(new[] { DifferenceType.ValueDifference, DifferenceType.StructureDifference }));

        var result = script.Execute(data, _context);
        Assert.That(result.Success, Is.True);
        // Only value/structure differences pass the filter, and the arrays match by key.
        Assert.That(Results(result.Data), Is.Empty);
    }

    [Test]
    public void FluentBuilder_WithUsingSetResultOn_BuildsCompareCommand()
    {
        var settings = new CompareSettings { ResultTypes = { DifferenceType.ValueDifference } };

        var script = new TLioScript<JToken>()
            .Compare("$.first").With("$.second").Using(settings).SetResultOn("$.result");

        Assert.That(script, Has.Count.EqualTo(1));
        var compare = (Compare<JToken>)script[0];
        Assert.That(compare.FirstPath, Is.EqualTo("$.first"));
        Assert.That(compare.SecondPath, Is.EqualTo("$.second"));
        Assert.That(compare.ResultPath, Is.EqualTo("$.result"));
        Assert.That(compare.Settings, Is.SameAs(settings));
    }

    [Test]
    public void FluentBuilder_WithoutSettings_LeavesSettingsNull()
    {
        var script = new TLioScript<JToken>()
            .Compare("$.a").With("$.b").SetResultOn("$.result");

        var compare = (Compare<JToken>)script[0];
        Assert.That(compare.Settings, Is.Null);
    }

    [Test]
    public void CompareResults_ContainsDifference_ReflectsEntries()
    {
        var results = new CompareResults
        {
            CompareResult.Equal("$.a", "$.b", "same")
        };
        Assert.That(results.ContainsDifference, Is.False);

        results.Add(CompareResult.TypeDifference("$.a", "$.b", "different"));
        Assert.That(results.ContainsDifference, Is.True);
    }
}
