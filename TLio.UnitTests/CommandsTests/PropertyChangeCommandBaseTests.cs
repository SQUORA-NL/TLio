using Newtonsoft.Json.Linq;
using NUnit.Framework;
using TLio.Commands;
using TLio.Core.Contracts;
using TLio.Core.Models;
using TLio.Json;

namespace TLio.UnitTests.CommandsTests;

/// <summary>
/// Tests the shared behaviours of PropertyChangeCommand&lt;TNode&gt; — the abstract base for
/// Add, Set, and Put — that are not exercised by the individual command test files:
///   - Validation: missing Path or missing Value
///   - Legacy syntax: leaf is derived from Path; parent is created via EnsurePath when absent
///   - New syntax: Path selects target objects; Property names the field on each object
///   - Recursive-descent path ($..): matched nodes are replaced in-place
///   - New-syntax warning when no objects match the path
///
/// Uses Set&lt;JToken&gt; as the concrete vehicle for all base-class tests because Set has
/// the simplest ApplyValueToTarget (pure replace — no add-if-missing or skip-if-present
/// side effects that would obscure base-class behaviour).
/// </summary>
[TestFixture]
public class PropertyChangeCommandBaseTests
{
    private JToken data = null!;
    private IExecutionContext<JToken> context = null!;

    [SetUp]
    public void Setup()
    {
        context = JsonExecutionContext.CreateDefault();
        data = JToken.Parse(
            "{ \"myString\": \"hello\", \"myObject\": { \"child\": 1 }, \"myArray\": [1, 2, 3]," +
            " \"items\": [ { \"id\": 1 }, { \"id\": 2 }, { \"id\": 3 } ] }");
    }

    // ── Validation ────────────────────────────────────────────────────────────

    [Test]
    public void MissingPath_ReturnsFailed_AndLogsMessage()
    {
        var command = new Set<JToken> { Value = new FixedValue<JToken>(new JValue("x")) };

        var result = command.Execute(data, context);

        Assert.That(result.Success, Is.False);
        Assert.That(context.GetLogEntries().Any(e => e.Message.Contains("Path property")), Is.True);
    }

    [Test]
    public void MissingValue_ReturnsFailed_AndLogsMessage()
    {
        // Value is null — base class validation should catch this
        var command = new Set<JToken> { Path = "$.myString" };

        var result = command.Execute(data, context);

        Assert.That(result.Success, Is.False);
        Assert.That(context.GetLogEntries().Any(e => e.Message.Contains("Value property")), Is.True);
    }

    // ── Legacy syntax: leaf from Path ─────────────────────────────────────────

    [Test]
    public void LegacySyntax_UpdatesExistingLeaf()
    {
        var result = new Set<JToken>("$.myString", new FixedValue<JToken>(new JValue("updated")))
            .Execute(data, context);

        Assert.That(result.Success, Is.True);
        Assert.That(data.SelectToken("$.myString")?.Value<string>(), Is.EqualTo("updated"));
    }

    [Test]
    public void LegacySyntax_CreatesNewIntermediateParentAndLeaf()
    {
        // Parent "$.newObj" does not exist — EnsurePath must create it before Set writes "newProp"
        var result = new Put<JToken>("$.newObj.newProp", new FixedValue<JToken>(new JValue(42)))
            .Execute(data, context);

        Assert.That(result.Success, Is.True);
        Assert.That(data.SelectToken("$.newObj.newProp")?.Value<int>(), Is.EqualTo(42));
    }

    [Test]
    public void LegacySyntax_DeepNewPath_CreatesEntireChain()
    {
        var result = new Put<JToken>("$.a.b.c.d", new FixedValue<JToken>(new JValue("deep")))
            .Execute(data, context);

        Assert.That(result.Success, Is.True);
        Assert.That(data.SelectToken("$.a.b.c.d")?.Value<string>(), Is.EqualTo("deep"));
    }

    [Test]
    public void LegacySyntax_PathSelectsObjectThenWritesLeaf_ViaParentSplit()
    {
        // "$.myObject.newField" — parent "$.myObject" exists; leaf "newField" is new
        var result = new Put<JToken>("$.myObject.newField", new FixedValue<JToken>(new JValue(99)))
            .Execute(data, context);

        Assert.That(result.Success, Is.True);
        Assert.That(data.SelectToken("$.myObject.newField")?.Value<int>(), Is.EqualTo(99));
    }

    // ── Legacy syntax: recursive-descent ($..prop) ────────────────────────────

    [Test]
    public void LegacySyntax_RecursiveDescent_ReplacesAllMatchedNodes()
    {
        // "$.." finds "child" on both the root object and any nested occurrences.
        // Only $.myObject.child exists here.
        var result = new Set<JToken>("$..child", new FixedValue<JToken>(new JValue(999)))
            .Execute(data, context);

        Assert.That(result.Success, Is.True);
        Assert.That(data.SelectTokens("$..child").All(t => t.Value<int>() == 999), Is.True);
        Assert.That(data.SelectTokens("$..child").Any(), Is.True);
    }

    [Test]
    public void LegacySyntax_RecursiveDescent_MultipleOccurrences_AllReplaced()
    {
        // Add a second "child" property deeper so we have two matches
        var multiData = JToken.Parse(
            "{ \"a\": { \"child\": 1 }, \"b\": { \"nested\": { \"child\": 2 } } }");

        var result = new Set<JToken>("$..child", new FixedValue<JToken>(new JValue(0)))
            .Execute(multiData, context);

        Assert.That(result.Success, Is.True);
        Assert.That(multiData.SelectTokens("$..child").Count(), Is.EqualTo(2));
        Assert.That(multiData.SelectTokens("$..child").All(t => t.Value<int>() == 0), Is.True);
    }

    // ── New syntax: explicit Property field ───────────────────────────────────

    [Test]
    public void NewSyntax_SetsNamedPropertyOnEachMatchedObject()
    {
        // Path "$.items[*]" selects all 3 item objects; Property "label" is written on each.
        var command = new Put<JToken>
        {
            Path = "$.items[*]",
            Property = "label",
            Value = new FixedValue<JToken>(new JValue("tagged"))
        };

        var result = command.Execute(data, context);

        Assert.That(result.Success, Is.True);
        Assert.That(data.SelectTokens("$.items[*].label").Count(), Is.EqualTo(3));
        Assert.That(data.SelectTokens("$.items[*].label").All(t => t.Value<string>() == "tagged"), Is.True);
    }

    [Test]
    public void NewSyntax_OverwritesExistingPropertyOnEachMatchedObject()
    {
        // "$.items[*]" objects already have "id"; Property "id" should overwrite each
        var command = new Put<JToken>
        {
            Path = "$.items[*]",
            Property = "id",
            Value = new FixedValue<JToken>(new JValue(0))
        };

        var result = command.Execute(data, context);

        Assert.That(result.Success, Is.True);
        Assert.That(data.SelectTokens("$.items[*].id").All(t => t.Value<int>() == 0), Is.True);
    }

    [Test]
    public void NewSyntax_NoMatchingObjects_LogsWarning_ReturnsSuccess()
    {
        var command = new Put<JToken>
        {
            Path = "$.nonExistent[*]",
            Property = "field",
            Value = new FixedValue<JToken>(new JValue("v"))
        };

        var result = command.Execute(data, context);

        Assert.That(result.Success, Is.True);
        Assert.That(context.GetLogEntries().Any(e => e.Message.Contains("no nodes matched")), Is.True);
    }

    [Test]
    public void NewSyntax_SingleObject_SetsProperty()
    {
        var command = new Put<JToken>
        {
            Path = "$.myObject",
            Property = "added",
            Value = new FixedValue<JToken>(new JValue(true))
        };

        var result = command.Execute(data, context);

        Assert.That(result.Success, Is.True);
        Assert.That(data.SelectToken("$.myObject.added")?.Value<bool>(), Is.True);
    }
}
