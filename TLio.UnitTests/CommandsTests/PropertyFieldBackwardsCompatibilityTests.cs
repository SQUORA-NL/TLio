using Newtonsoft.Json.Linq;
using NUnit.Framework;
using TLio.Commands;
using TLio.Core.Contracts;
using TLio.Core.Models;
using TLio.Json;

namespace TLio.UnitTests.CommandsTests;

/// <summary>
/// Ported from JLio.UnitTests.CommandsTests.PropertyFieldBackwardsCompatibilityTests.
/// Verifies that the two equivalent syntaxes for PropertyChangeCommand-derived commands
/// produce identical results:
///   Legacy: path = "$.parent.newProp"   (leaf name derived from path tail)
///   New:    path = "$.parent", property = "newProp"  (explicit property field)
///
/// Uses Add, Set, and Put as concrete vehicles because each has a different
/// ApplyValueToTarget strategy (add-only, replace-only, upsert).
/// </summary>
[TestFixture]
public class PropertyFieldBackwardsCompatibilityTests
{
    private IExecutionContext<JToken> _context = null!;

    private static JToken BaseData() => JToken.Parse(
        "{ \"obj\": { \"existing\": 1 }, \"arr\": [10, 20, 30] }");

    [SetUp]
    public void Setup()
    {
        _context = JsonExecutionContext.CreateDefault();
    }

    // ── Add ───────────────────────────────────────────────────────────────────

    [Test]
    public void Add_LegacySyntax_AddsNewProperty()
    {
        var data = BaseData();
        var result = new Add<JToken>("$.obj.newProp", new FixedValue<JToken>(new JValue("v")))
            .Execute(data, _context);

        Assert.That(result.Success, Is.True);
        Assert.That(data.SelectToken("$.obj.newProp")?.Value<string>(), Is.EqualTo("v"));
    }

    [Test]
    public void Add_NewSyntax_AddsNewProperty()
    {
        var data = BaseData();
        var result = new Add<JToken>("$.obj", "newProp", new FixedValue<JToken>(new JValue("v")))
            .Execute(data, _context);

        Assert.That(result.Success, Is.True);
        Assert.That(data.SelectToken("$.obj.newProp")?.Value<string>(), Is.EqualTo("v"));
    }

    [Test]
    public void Add_LegacyAndNewSyntax_ProduceSameResult()
    {
        var legacy = BaseData();
        var newer = BaseData();

        new Add<JToken>("$.obj.newProp", new FixedValue<JToken>(new JValue(42))).Execute(legacy, _context);
        new Add<JToken>("$.obj", "newProp", new FixedValue<JToken>(new JValue(42))).Execute(newer, _context);

        Assert.That(JToken.DeepEquals(legacy, newer), Is.True);
    }

    [Test]
    public void Add_NewSyntax_MultipleMatchingObjects()
    {
        var data = JToken.Parse("{ \"items\": [{ \"id\": 1 }, { \"id\": 2 }] }");

        var result = new Add<JToken>("$.items[*]", "flag", new FixedValue<JToken>(new JValue(true)))
            .Execute(data, _context);

        Assert.That(result.Success, Is.True);
        Assert.That(data.SelectTokens("$.items[*].flag").Count(), Is.EqualTo(2));
    }

    [Test]
    public void Add_NewSyntax_SkipsWhenPropertyAlreadyExists()
    {
        var data = JToken.Parse("{ \"obj\": { \"existing\": 1 } }");

        var result = new Add<JToken>("$.obj", "existing", new FixedValue<JToken>(new JValue(99)))
            .Execute(data, _context);

        // Add skips (does not overwrite) — existing value must remain unchanged
        Assert.That(data.SelectToken("$.obj.existing")?.Value<int>(), Is.EqualTo(1));
        Assert.That(_context.GetLogEntries().Any(e => e.Message.Contains("already exists")), Is.True);
    }

    // ── Set ───────────────────────────────────────────────────────────────────

    [Test]
    public void Set_LegacySyntax_ReplacesExistingProperty()
    {
        var data = BaseData();
        var result = new Set<JToken>("$.obj.existing", new FixedValue<JToken>(new JValue(99)))
            .Execute(data, _context);

        Assert.That(result.Success, Is.True);
        Assert.That(data.SelectToken("$.obj.existing")?.Value<int>(), Is.EqualTo(99));
    }

    [Test]
    public void Set_NewSyntax_ReplacesExistingProperty()
    {
        var data = BaseData();
        var result = new Set<JToken>("$.obj", "existing", new FixedValue<JToken>(new JValue(99)))
            .Execute(data, _context);

        Assert.That(result.Success, Is.True);
        Assert.That(data.SelectToken("$.obj.existing")?.Value<int>(), Is.EqualTo(99));
    }

    [Test]
    public void Set_LegacyAndNewSyntax_ProduceSameResult()
    {
        var legacy = BaseData();
        var newer = BaseData();

        new Set<JToken>("$.obj.existing", new FixedValue<JToken>(new JValue(55))).Execute(legacy, _context);
        new Set<JToken>("$.obj", "existing", new FixedValue<JToken>(new JValue(55))).Execute(newer, _context);

        Assert.That(JToken.DeepEquals(legacy, newer), Is.True);
    }

    // ── Put ───────────────────────────────────────────────────────────────────

    [Test]
    public void Put_LegacySyntax_UpsertsMissingProperty()
    {
        var data = BaseData();
        var result = new Put<JToken>("$.obj.newProp", new FixedValue<JToken>(new JValue("upserted")))
            .Execute(data, _context);

        Assert.That(result.Success, Is.True);
        Assert.That(data.SelectToken("$.obj.newProp")?.Value<string>(), Is.EqualTo("upserted"));
    }

    [Test]
    public void Put_NewSyntax_UpsertsMissingProperty()
    {
        var data = BaseData();
        var result = new Put<JToken>("$.obj", "newProp", new FixedValue<JToken>(new JValue("upserted")))
            .Execute(data, _context);

        Assert.That(result.Success, Is.True);
        Assert.That(data.SelectToken("$.obj.newProp")?.Value<string>(), Is.EqualTo("upserted"));
    }

    [Test]
    public void Put_LegacyAndNewSyntax_ProduceSameResult()
    {
        var legacy = BaseData();
        var newer = BaseData();

        new Put<JToken>("$.obj.existing", new FixedValue<JToken>(new JValue(77))).Execute(legacy, _context);
        new Put<JToken>("$.obj", "existing", new FixedValue<JToken>(new JValue(77))).Execute(newer, _context);

        Assert.That(JToken.DeepEquals(legacy, newer), Is.True);
    }

    // ── New-syntax validation ─────────────────────────────────────────────────

    [Test]
    public void NewSyntax_NoMatchingObjects_LogsWarning()
    {
        var data = BaseData();
        var result = new Add<JToken>("$.nonExistent", "prop", new FixedValue<JToken>(new JValue("v")))
            .Execute(data, _context);

        // No error, but a warning should be logged for no matching path
        Assert.That(result.Success, Is.True);
        Assert.That(_context.GetLogEntries().Any(), Is.True);
    }
}
