using Microsoft.Extensions.Logging;
using Newtonsoft.Json.Linq;
using NUnit.Framework;
using TLio.Commands;
using TLio.Core.Contracts;
using TLio.Core.Models;
using TLio.Client;
using TLio.Functions;
using TLio.Json;

namespace TLio.UnitTests.CommandsTests;

/// <summary>
/// setProperties: set, but keyed by a selection (Properties) instead of a single path
/// expression. Deliberately generic — Value is any function; these tests exercise it with
/// toArray() (bare, so currentNode is each selected node's own value) since that is the
/// motivating use case, and one test with a literal to show it is not array-specific.
/// </summary>
[TestFixture]
public class SetPropertiesTests
{
    private JToken data = null!;
    private IExecutionContext<JToken> executeOptions = null!;

    [SetUp]
    public void Setup()
    {
        executeOptions = JsonExecutionContext.CreateDefault();
        data = JToken.Parse("""
            {
              "person": {
                "name": "Ada",
                "tags": "vip",
                "roles": ["admin"],
                "note": null,
                "address": { "city": "Amsterdam" }
              },
              "items": [
                { "id": "1", "cat": "a" },
                { "id": "2", "cat": "b" }
              ]
            }
            """);
    }

    private static IFunctionSupportedValue<JToken> ToArrayValue() =>
        new FunctionSupportedValue<JToken>(new ToArray<JToken>());

    private static IFunctionSupportedValue<JToken> Names(params string[] names) =>
        new FixedValue<JToken>(new JArray(names));

    private static IFunctionSupportedValue<JToken> Find(string[] kinds, bool recursive) =>
        new FunctionSupportedValue<JToken>((IFunction<JToken>)new ScriptPath<JToken>().SetArguments(new Arguments<JToken>
        {
            new FixedValue<JToken>(new JValue("*")),
            new FixedValue<JToken>(new JArray(kinds)),
            new FixedValue<JToken>(new JValue(recursive))
        }));

    [Test]
    public void NamedProperties_AreWrapped_OthersUntouched()
    {
        var result = new SetProperties<JToken>("$.person", ToArrayValue(), Names("tags", "roles"))
            .Execute(data, executeOptions);

        Assert.That(result.Success, Is.True);
        Assert.That(JToken.DeepEquals(data.SelectToken("$.person.tags"), JArray.Parse("[\"vip\"]")), Is.True);
        Assert.That(JToken.DeepEquals(data.SelectToken("$.person.roles"), JArray.Parse("[\"admin\"]")), Is.True,
            "already an array — toArray passes it through unchanged, not nested");
        Assert.That(data.SelectToken("$.person.name")?.Value<string>(), Is.EqualTo("Ada"),
            "not named — left alone");
    }

    [Test]
    public void RelativePath_ReachesANestedSubItem()
    {
        var result = new SetProperties<JToken>("$.person", ToArrayValue(), Names("@.address.city"))
            .Execute(data, executeOptions);

        Assert.That(result.Success, Is.True);
        Assert.That(JToken.DeepEquals(data.SelectToken("$.person.address.city"), JArray.Parse("[\"Amsterdam\"]")), Is.True);
    }

    [Test]
    public void FindFunction_SelectsLiveNodesDirectly()
    {
        var result = new SetProperties<JToken>("$.person", ToArrayValue(), Find(new[] { "primitive" }, true))
            .Execute(data, executeOptions);

        Assert.That(result.Success, Is.True);
        Assert.That(JToken.DeepEquals(data.SelectToken("$.person.name"), JArray.Parse("[\"Ada\"]")), Is.True);
        Assert.That(JToken.DeepEquals(data.SelectToken("$.person.tags"), JArray.Parse("[\"vip\"]")), Is.True);
        Assert.That(JToken.DeepEquals(data.SelectToken("$.person.address.city"), JArray.Parse("[\"Amsterdam\"]")), Is.True,
            "recursive find reaches nested primitives too");
    }

    [Test]
    public void NullProperty_BecomesEmptyArray()
    {
        var result = new SetProperties<JToken>("$.person", ToArrayValue(), Names("note")).Execute(data, executeOptions);

        Assert.That(result.Success, Is.True);
        Assert.That(JToken.DeepEquals(data.SelectToken("$.person.note"), new JArray()), Is.True);
    }

    [Test]
    public void NoPropertiesGiven_AppliesToEveryDirectProperty()
    {
        var result = new SetProperties<JToken>("$.person", ToArrayValue()).Execute(data, executeOptions);

        Assert.That(result.Success, Is.True);
        Assert.That(JToken.DeepEquals(data.SelectToken("$.person.name"), JArray.Parse("[\"Ada\"]")), Is.True);
        Assert.That(JToken.DeepEquals(data.SelectToken("$.person.tags"), JArray.Parse("[\"vip\"]")), Is.True);
        Assert.That(JToken.DeepEquals(data.SelectToken("$.person.roles"), JArray.Parse("[\"admin\"]")), Is.True);
        Assert.That(JToken.DeepEquals(data.SelectToken("$.person.note"), new JArray()), Is.True);
        Assert.That(JToken.DeepEquals(data.SelectToken("$.person.address"), JArray.Parse("[{\"city\":\"Amsterdam\"}]")), Is.True,
            "address is a direct property too — wrapped whole, not recursed into");
    }

    [Test]
    public void RecursiveObjectKind_SingleCall_LosesTheNestedWrap()
    {
        // billing.contact is itself an object nested under billing, another match — wrapping
        // billing clones its still-unwrapped contact, orphaning contact's own later replace.
        var tree = JToken.Parse("""
            {
              "customer": {
                "id": "C-1",
                "billing": { "iban": "NL00BANK", "contact": { "email": "ada@example.com" } }
              }
            }
            """);

        var result = new SetProperties<JToken>("$.customer", ToArrayValue(), Find(new[] { "object" }, true))
            .Execute(tree, executeOptions);

        Assert.That(result.Success, Is.True);
        Assert.That(JToken.DeepEquals(
            tree.SelectToken("$.customer.billing"),
            JArray.Parse("""[{"iban":"NL00BANK","contact":{"email":"ada@example.com"}}]""")), Is.True,
            "billing wraps, but the clone freezes contact in its pre-wrap, unwrapped shape");
    }

    [Test]
    public void RecursiveObjectKind_DeepestFirst_WrapsEveryComplexObjectInTheTree()
    {
        var tree = JToken.Parse("""
            {
              "customer": {
                "id": "C-1",
                "billing": { "iban": "NL00BANK", "contact": { "email": "ada@example.com" } }
              }
            }
            """);

        new SetProperties<JToken>("$.customer.billing", ToArrayValue(), Find(new[] { "object" }, true))
            .Execute(tree, executeOptions);
        var result = new SetProperties<JToken>("$.customer", ToArrayValue(), Find(new[] { "object" }, true))
            .Execute(tree, executeOptions);

        Assert.That(result.Success, Is.True);
        Assert.That(JToken.DeepEquals(
            tree.SelectToken("$.customer.billing"),
            JArray.Parse("""[{"iban":"NL00BANK","contact":[{"email":"ada@example.com"}]}]""")), Is.True,
            "wrapping the innermost object first, then the level above, reaches every depth");
        Assert.That(tree.SelectToken("$.customer.id")?.Value<string>(), Is.EqualTo("C-1"), "primitive — untouched");
    }

    [Test]
    public void WildcardContainerPath_AppliesToEveryMatchedObject()
    {
        var result = new SetProperties<JToken>("$.items[*]", ToArrayValue(), Names("cat")).Execute(data, executeOptions);

        Assert.That(result.Success, Is.True);
        Assert.That(JToken.DeepEquals(data.SelectToken("$.items[0].cat"), JArray.Parse("[\"a\"]")), Is.True);
        Assert.That(JToken.DeepEquals(data.SelectToken("$.items[1].cat"), JArray.Parse("[\"b\"]")), Is.True);
        Assert.That(data.SelectToken("$.items[0].id")?.Value<string>(), Is.EqualTo("1"), "not named — left alone");
    }

    [Test]
    public void NotArraySpecific_ALiteralValueOverwritesEveryNamedProperty()
    {
        var result = new SetProperties<JToken>(
            "$.person", new FixedValue<JToken>(new JValue("redacted")), Names("name", "tags"))
            .Execute(data, executeOptions);

        Assert.That(result.Success, Is.True);
        Assert.That(data.SelectToken("$.person.name")?.Value<string>(), Is.EqualTo("redacted"));
        Assert.That(data.SelectToken("$.person.tags")?.Value<string>(), Is.EqualTo("redacted"));
    }

    [Test]
    public void MissingNamedProperty_WarnsAndSkipsButStillSucceeds()
    {
        var result = new SetProperties<JToken>("$.person", ToArrayValue(), Names("doesNotExist", "tags"))
            .Execute(data, executeOptions);

        Assert.That(result.Success, Is.True);
        Assert.That(JToken.DeepEquals(data.SelectToken("$.person.tags"), JArray.Parse("[\"vip\"]")), Is.True);
        Assert.That(executeOptions.GetLogEntries().Any(e =>
            e.Level == LogLevel.Warning && e.Message.Contains("doesNotExist") && e.Message.Contains("not found")), Is.True);
    }

    [Test]
    public void NonObjectMatch_WarnsAndSkips()
    {
        var before = data.SelectToken("$.person.roles")!.DeepClone();

        var result = new SetProperties<JToken>("$.person.roles", ToArrayValue(), Names("whatever"))
            .Execute(data, executeOptions);

        Assert.That(result.Success, Is.True);
        Assert.That(JToken.DeepEquals(data.SelectToken("$.person.roles"), before), Is.True);
        Assert.That(executeOptions.GetLogEntries().Any(e =>
            e.Level == LogLevel.Warning && e.Message.Contains("is not an object")), Is.True);
    }

    [Test]
    public void MissingPath_WarnsAndContinues()
    {
        var before = data.DeepClone();

        var result = new SetProperties<JToken>("$.does.not.exist", ToArrayValue(), Names("x")).Execute(data, executeOptions);

        Assert.That(result.Success, Is.True, "a missing path is a no-op, not a failure");
        Assert.That(JToken.DeepEquals(data, before), Is.True);
        Assert.That(executeOptions.GetLogEntries().Any(e =>
            e.Level == LogLevel.Warning && e.Message.Contains("no nodes matched")), Is.True);
    }

    [TestCase(null, "Path property for setProperties command is missing")]
    [TestCase("", "Path property for setProperties command is missing")]
    public void WithoutPath_Fails(string? path, string message)
    {
        var result = new SetProperties<JToken> { Path = path, Value = ToArrayValue() }.Execute(data, executeOptions);

        Assert.That(result.Success, Is.False);
        Assert.That(executeOptions.GetLogEntries().Any(l => l.Message == message), Is.True);
    }

    [Test]
    public void WithoutValue_Fails()
    {
        var result = new SetProperties<JToken> { Path = "$.person" }.Execute(data, executeOptions);

        Assert.That(result.Success, Is.False);
        Assert.That(executeOptions.GetLogEntries().Any(l =>
            l.Message == "Value property for setProperties command is missing"), Is.True);
    }

    [Test]
    public void CanUseScriptApi()
    {
        var script = new TLioScript<JToken>
        {
            new SetProperties<JToken>("$.person", ToArrayValue(), Names("tags"))
        };

        var result = script.Execute(data, executeOptions);

        Assert.That(result.Success, Is.True);
        Assert.That(JToken.DeepEquals(result.Data.SelectToken("$.person.tags"), JArray.Parse("[\"vip\"]")), Is.True);
    }
}
