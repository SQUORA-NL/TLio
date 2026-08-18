using Newtonsoft.Json.Linq;
using NUnit.Framework;
using TLio.Commands.Advanced;
using TLio.Commands.Advanced.Settings;
using TLio.Core.Contracts;
using TLio.Json;

namespace TLio.UnitTests.CommandsTests;

/// <summary>
/// Tests for the MergeSettings feature set ported from JLio (issue #30):
/// key-based array matching, unique items, merge strategies and root-level
/// match settings.
/// </summary>
[TestFixture]
public class MergeSettingsTests
{
    private IExecutionContext<JToken> _context = null!;

    [SetUp]
    public void SetUp() => _context = JsonExecutionContext.CreateDefault();

    private static MergeSettings WithArrayKeys(string arrayPath, params string[] keys) =>
        new()
        {
            ArraySettings =
            {
                new MergeArraySettings { ArrayPath = arrayPath, KeyPaths = keys.ToList() }
            }
        };

    // ── Key-based array matching ──────────────────────────────────────────────

    [Test]
    public void KeyPaths_MatchingElements_AreMergedNotAppended()
    {
        var data = JToken.Parse("""
        {
          "source": { "items": [ { "id": 1, "name": "one-updated", "extra": true } ] },
          "target": { "items": [ { "id": 1, "name": "one" }, { "id": 2, "name": "two" } ] }
        }
        """);

        var result = new Merge<JToken>("$.source", "$.target", WithArrayKeys("$.target.items", "id"))
            .Execute(data, _context);

        Assert.That(result.Success, Is.True);
        var items = (JArray)data.SelectToken("$.target.items")!;
        Assert.That(items.Count, Is.EqualTo(2), "matching element must be merged, not appended");
        Assert.That(items[0]["name"]!.Value<string>(), Is.EqualTo("one-updated"));
        Assert.That(items[0]["extra"]!.Value<bool>(), Is.True);
        Assert.That(items[1]["name"]!.Value<string>(), Is.EqualTo("two"));
    }

    [Test]
    public void KeyPaths_NonMatchingElements_AreAppended()
    {
        var data = JToken.Parse("""
        {
          "source": { "items": [ { "id": 3, "name": "three" } ] },
          "target": { "items": [ { "id": 1, "name": "one" } ] }
        }
        """);

        new Merge<JToken>("$.source", "$.target", WithArrayKeys("$.target.items", "id"))
            .Execute(data, _context);

        var items = (JArray)data.SelectToken("$.target.items")!;
        Assert.That(items.Count, Is.EqualTo(2));
        Assert.That(items[1]["id"]!.Value<int>(), Is.EqualTo(3));
    }

    [Test]
    public void KeyPaths_SupportAtNotationAndNestedPaths()
    {
        var data = JToken.Parse("""
        {
          "source": { "rows": [ { "key": { "id": "x" }, "value": 20 } ] },
          "target": { "rows": [ { "key": { "id": "x" }, "value": 10 } ] }
        }
        """);

        new Merge<JToken>("$.source", "$.target", WithArrayKeys("$.target.rows", "@.key.id"))
            .Execute(data, _context);

        var rows = (JArray)data.SelectToken("$.target.rows")!;
        Assert.That(rows.Count, Is.EqualTo(1));
        Assert.That(rows[0]["value"]!.Value<int>(), Is.EqualTo(20));
    }

    [Test]
    public void KeyPaths_MultipleKeys_MustAllMatch()
    {
        var data = JToken.Parse("""
        {
          "source": { "items": [ { "id": 1, "region": "eu", "v": 2 } ] },
          "target": { "items": [ { "id": 1, "region": "us", "v": 1 } ] }
        }
        """);

        new Merge<JToken>("$.source", "$.target", WithArrayKeys("$.target.items", "id", "region"))
            .Execute(data, _context);

        var items = (JArray)data.SelectToken("$.target.items")!;
        Assert.That(items.Count, Is.EqualTo(2), "region differs, so the item must be appended");
        Assert.That(items[0]["v"]!.Value<int>(), Is.EqualTo(1));
    }

    [Test]
    public void KeyPaths_ArrayPath_MatchesWithoutRootIndicator()
    {
        var data = JToken.Parse("""
        {
          "source": { "items": [ { "id": 1, "v": 2 } ] },
          "target": { "items": [ { "id": 1, "v": 1 } ] }
        }
        """);

        new Merge<JToken>("$.source", "$.target", WithArrayKeys("target.items", "id"))
            .Execute(data, _context);

        var items = (JArray)data.SelectToken("$.target.items")!;
        Assert.That(items.Count, Is.EqualTo(1));
        Assert.That(items[0]["v"]!.Value<int>(), Is.EqualTo(2));
    }

    [Test]
    public void KeyPaths_ArraysMergedAtRoot_UseTheirOwnPath()
    {
        var data = JToken.Parse("""
        {
          "source": [ { "id": 1, "v": 2 } ],
          "target": [ { "id": 1, "v": 1 } ]
        }
        """);

        new Merge<JToken>("$.source", "$.target", WithArrayKeys("$.target", "id"))
            .Execute(data, _context);

        var items = (JArray)data.SelectToken("$.target")!;
        Assert.That(items.Count, Is.EqualTo(1));
        Assert.That(items[0]["v"]!.Value<int>(), Is.EqualTo(2));
    }

    [Test]
    public void KeyPaths_NotConfiguredForThisArray_FallsBackToConcat()
    {
        var data = JToken.Parse("""
        {
          "source": { "items": [ { "id": 1 } ] },
          "target": { "items": [ { "id": 1 } ] }
        }
        """);

        new Merge<JToken>("$.source", "$.target", WithArrayKeys("$.somewhere.else", "id"))
            .Execute(data, _context);

        var items = (JArray)data.SelectToken("$.target.items")!;
        Assert.That(items.Count, Is.EqualTo(2));
    }

    // ── Unique items without keys ─────────────────────────────────────────────

    [Test]
    public void UniqueItemsWithoutKeys_SkipsDuplicates()
    {
        var data = JToken.Parse("""
        { "source": { "tags": ["a", "b", "c"] }, "target": { "tags": ["a", "c"] } }
        """);

        var settings = new MergeSettings
        {
            ArraySettings =
            {
                new MergeArraySettings { ArrayPath = "$.target.tags", UniqueItemsWithoutKeys = true }
            }
        };

        new Merge<JToken>("$.source", "$.target", settings).Execute(data, _context);

        var tags = (JArray)data.SelectToken("$.target.tags")!;
        Assert.That(tags.Select(t => t.Value<string>()), Is.EqualTo(new[] { "a", "c", "b" }));
    }

    [Test]
    public void UniqueItemsWithoutKeys_False_AppendsDuplicates()
    {
        var data = JToken.Parse("""
        { "source": { "tags": ["a"] }, "target": { "tags": ["a"] } }
        """);

        new Merge<JToken>("$.source", "$.target").Execute(data, _context);

        var tags = (JArray)data.SelectToken("$.target.tags")!;
        Assert.That(tags.Count, Is.EqualTo(2));
    }

    [Test]
    public void UniqueItemsWithoutKeys_ComparesObjectsDeeply()
    {
        var data = JToken.Parse("""
        {
          "source": { "items": [ { "a": 1 }, { "a": 2 } ] },
          "target": { "items": [ { "a": 1 } ] }
        }
        """);

        var settings = new MergeSettings
        {
            ArraySettings =
            {
                new MergeArraySettings { ArrayPath = "$.target.items", UniqueItemsWithoutKeys = true }
            }
        };

        new Merge<JToken>("$.source", "$.target", settings).Execute(data, _context);

        var items = (JArray)data.SelectToken("$.target.items")!;
        Assert.That(items.Count, Is.EqualTo(2));
    }

    // ── ArrayMergeMode.MergeByKey ─────────────────────────────────────────────

    [Test]
    public void MergeByKey_WithoutSettings_DeduplicatesOnWholeItem()
    {
        var data = JToken.Parse("""
        { "source": { "tags": ["a", "b"] }, "target": { "tags": ["a"] } }
        """);

        new Merge<JToken>("$.source", "$.target", ArrayMergeMode.MergeByKey).Execute(data, _context);

        var tags = (JArray)data.SelectToken("$.target.tags")!;
        Assert.That(tags.Select(t => t.Value<string>()), Is.EqualTo(new[] { "a", "b" }));
    }

    [Test]
    public void MergeByKey_WithKeyPaths_MergesMatchingElements()
    {
        var data = JToken.Parse("""
        {
          "source": { "items": [ { "id": 1, "v": 2 } ] },
          "target": { "items": [ { "id": 1, "v": 1 } ] }
        }
        """);

        new Merge<JToken>("$.source", "$.target", ArrayMergeMode.MergeByKey,
            WithArrayKeys("$.target.items", "id")).Execute(data, _context);

        var items = (JArray)data.SelectToken("$.target.items")!;
        Assert.That(items.Count, Is.EqualTo(1));
        Assert.That(items[0]["v"]!.Value<int>(), Is.EqualTo(2));
    }

    // ── Strategies ────────────────────────────────────────────────────────────

    [Test]
    public void OnlyStructure_AddsMissingPropertiesButKeepsExistingValues()
    {
        var data = JToken.Parse("""
        {
          "source": { "a": "from-source", "nested": { "x": 9, "y": 2 }, "c": "new" },
          "target": { "a": "keep-me", "nested": { "x": 1 } }
        }
        """);

        var settings = new MergeSettings { Strategy = MergeSettings.StrategyOnlyStructure };
        new Merge<JToken>("$.source", "$.target", settings).Execute(data, _context);

        Assert.That(data.SelectToken("$.target.a")!.Value<string>(), Is.EqualTo("keep-me"));
        Assert.That(data.SelectToken("$.target.c")!.Value<string>(), Is.EqualTo("new"));
        Assert.That(data.SelectToken("$.target.nested.x")!.Value<int>(), Is.EqualTo(1));
        Assert.That(data.SelectToken("$.target.nested.y")!.Value<int>(), Is.EqualTo(2));
    }

    [Test]
    public void OnlyValues_UpdatesExistingPropertiesButAddsNothing()
    {
        var data = JToken.Parse("""
        {
          "source": { "a": "from-source", "nested": { "x": 9, "y": 2 }, "c": "new" },
          "target": { "a": "overwrite-me", "nested": { "x": 1 } }
        }
        """);

        var settings = new MergeSettings { Strategy = MergeSettings.StrategyOnlyValues };
        new Merge<JToken>("$.source", "$.target", settings).Execute(data, _context);

        Assert.That(data.SelectToken("$.target.a")!.Value<string>(), Is.EqualTo("from-source"));
        Assert.That(data.SelectToken("$.target.c"), Is.Null);
        Assert.That(data.SelectToken("$.target.nested.x")!.Value<int>(), Is.EqualTo(9));
        Assert.That(data.SelectToken("$.target.nested.y"), Is.Null);
    }

    [Test]
    public void FullMerge_IsTheDefaultStrategy()
    {
        var data = JToken.Parse("""
        { "source": { "a": 2, "b": 3 }, "target": { "a": 1 } }
        """);

        new Merge<JToken>("$.source", "$.target").Execute(data, _context);

        Assert.That(data.SelectToken("$.target.a")!.Value<int>(), Is.EqualTo(2));
        Assert.That(data.SelectToken("$.target.b")!.Value<int>(), Is.EqualTo(3));
    }

    // ── Match settings ────────────────────────────────────────────────────────

    [Test]
    public void MatchSettings_MergesOnlyWhenKeysMatch()
    {
        var data = JToken.Parse("""
        {
          "source": { "id": 1, "extra": "added" },
          "matching": { "id": 1 },
          "nonMatching": { "id": 2 }
        }
        """);

        var settings = new MergeSettings { MatchSettings = { KeyPaths = { "id" } } };

        new Merge<JToken>("$.source", "$.matching", settings).Execute(data, _context);
        new Merge<JToken>("$.source", "$.nonMatching", settings).Execute(data, _context);

        Assert.That(data.SelectToken("$.matching.extra")!.Value<string>(), Is.EqualTo("added"));
        Assert.That(data.SelectToken("$.nonMatching.extra"), Is.Null);
    }

    [Test]
    public void MatchSettings_AlsoGuardNestedObjects()
    {
        var data = JToken.Parse("""
        {
          "source": { "id": 1, "child": { "id": 8, "v": "source" } },
          "target": { "id": 1, "child": { "id": 9, "v": "target" } }
        }
        """);

        var settings = new MergeSettings { MatchSettings = { KeyPaths = { "id" } } };
        new Merge<JToken>("$.source", "$.target", settings).Execute(data, _context);

        Assert.That(data.SelectToken("$.target.child.v")!.Value<string>(), Is.EqualTo("target"),
            "nested child ids differ, so the nested object must not be merged");
    }

    // ── Validation ────────────────────────────────────────────────────────────

    [Test]
    public void SamePathAndTargetPath_FailsValidation()
    {
        var command = new Merge<JToken>("$.same", "$.same");
        var validation = command.ValidateCommandInstance();

        Assert.That(validation.IsValid, Is.False);
        Assert.That(validation.ValidationMessages.Any(m => m.Contains("cannot be the same")), Is.True);
    }

    [Test]
    public void SamePathAndTargetPath_ReturnsFailedResult()
    {
        var data = JToken.Parse("""{ "same": { "a": 1 } }""");
        var result = new Merge<JToken>("$.same", "$.same").Execute(data, _context);

        Assert.That(result.Success, Is.False);
    }

    // ── Backwards compatibility ───────────────────────────────────────────────

    [Test]
    public void WithoutSettings_NestedArraysStillConcat()
    {
        var data = JToken.Parse("""
        { "source": { "list": [3] }, "target": { "list": [1, 2] } }
        """);

        new Merge<JToken>("$.source", "$.target").Execute(data, _context);

        var list = (JArray)data.SelectToken("$.target.list")!;
        Assert.That(list.Select(t => t.Value<int>()), Is.EqualTo(new[] { 1, 2, 3 }));
    }

    [Test]
    public void WithoutSettings_ReplaceModeStillReplacesNestedArrays()
    {
        var data = JToken.Parse("""
        { "source": { "list": [3] }, "target": { "list": [1, 2] } }
        """);

        new Merge<JToken>("$.source", "$.target", ArrayMergeMode.Replace).Execute(data, _context);

        var list = (JArray)data.SelectToken("$.target.list")!;
        Assert.That(list.Select(t => t.Value<int>()), Is.EqualTo(new[] { 3 }));
    }

    [Test]
    public void DeepClonedValues_DoNotShareStateWithSource()
    {
        var data = JToken.Parse("""
        { "source": { "nested": { "a": 1 } }, "target": {} }
        """);

        new Merge<JToken>("$.source", "$.target").Execute(data, _context);
        ((JObject)data.SelectToken("$.target.nested")!)["a"] = 99;

        Assert.That(data.SelectToken("$.source.nested.a")!.Value<int>(), Is.EqualTo(1));
    }
}
