using Newtonsoft.Json.Linq;
using NUnit.Framework;
using TLio.Core.Contracts;
using TLio.Core.Models;
using TLio.Functions;
using TLio.Json;

namespace TLio.Functions.Tests.FunctionsTests;

/// <summary>
/// =scriptpath(*, kinds, recursive) — the "find" shape: returns live nodes under the current
/// node whose kind is in <c>kinds</c>, walking the whole subtree when <c>recursive</c> is true.
/// </summary>
[TestFixture]
public class ScriptPathFindTests
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
                "age": 30,
                "note": null,
                "tags": ["vip", "admin"],
                "address": { "city": "Amsterdam", "zip": "1011" }
              }
            }
            """);
    }

    private static IFunction<JToken> Find(string name, string[] kinds, bool recursive) =>
        (IFunction<JToken>)new ScriptPath<JToken>().SetArguments(new Arguments<JToken>
        {
            new FixedValue<JToken>(new JValue(name)),
            new FixedValue<JToken>(new JArray(kinds)),
            new FixedValue<JToken>(new JValue(recursive))
        });

    [Test]
    public void NonRecursive_PrimitiveOnly_FindsOnlyDirectScalarChildren()
    {
        var person = data.SelectToken("$.person")!;
        var fn = Find("*", new[] { "primitive" }, false);

        var result = fn.Execute(person, data, executeOptions);

        Assert.That(result.Success, Is.True);
        var values = result.Data.Select(n => n.ToString()).OrderBy(v => v).ToList();
        // name, age — not note (null), not tags (array), not address (object)
        Assert.That(values, Is.EquivalentTo(new[] { "Ada", "30" }));
    }

    [Test]
    public void NonRecursive_NullOnly_FindsTheNullProperty()
    {
        var person = data.SelectToken("$.person")!;
        var fn = Find("*", new[] { "null" }, false);

        var result = fn.Execute(person, data, executeOptions);

        Assert.That(result.Success, Is.True);
        Assert.That(result.Data.Count, Is.EqualTo(1));
        Assert.That(result.Data[0].Type, Is.EqualTo(JTokenType.Null));
    }

    [Test]
    public void Recursive_ReachesNestedObjectProperties()
    {
        var person = data.SelectToken("$.person")!;
        var fn = Find("*", new[] { "object", "primitive" }, true);

        var result = fn.Execute(person, data, executeOptions);

        Assert.That(result.Success, Is.True);
        var texts = result.Data.Select(n => n.ToString()).ToList();
        // primitives: name, age, city, zip + tags elements (arrays are transparent, their
        // string elements are primitives too) + the address object itself (kind "object")
        Assert.That(texts, Does.Contain("Ada"));
        Assert.That(texts, Does.Contain("30"));
        Assert.That(texts, Does.Contain("Amsterdam"), "nested under address — reached only because recursive=true");
        Assert.That(texts, Does.Contain("1011"));
        Assert.That(texts, Does.Contain("vip"), "array elements are reached even though arrays themselves never match");
        Assert.That(texts, Does.Contain("admin"));
        Assert.That(result.Data.Any(n => n.Type == JTokenType.Object), Is.True,
            "the address object itself is included because 'object' is in kinds");
    }

    [Test]
    public void Recursive_ArraysAreNeverThemselvesAMatch()
    {
        var person = data.SelectToken("$.person")!;
        // Ask for arrays too — should never produce a match since arrays are transparent.
        var fn = Find("*", new[] { "array", "primitive" }, true);

        var result = fn.Execute(person, data, executeOptions);

        Assert.That(result.Success, Is.True);
        Assert.That(result.Data.Any(n => n.Type == JTokenType.Array), Is.False);
    }

    [Test]
    public void ReturnedNodesAreLiveReferences_ReplaceWritesBackIntoTheDocument()
    {
        var person = data.SelectToken("$.person")!;
        var fn = Find("*", new[] { "primitive" }, false);

        var result = fn.Execute(person, data, executeOptions);
        var nameNode = result.Data.First(n => n.ToString() == "Ada");

        executeOptions.NodeAdapter.Replace(nameNode, new JValue("Grace"));

        Assert.That(data.SelectToken("$.person.name")?.Value<string>(), Is.EqualTo("Grace"));
    }

    [Test]
    public void NonWildcardName_Fails()
    {
        var fn = Find("specificName", new[] { "primitive" }, false);
        var result = fn.Execute(data, data, executeOptions);

        Assert.That(result.Success, Is.False);
    }

    [Test]
    public void UnrecognisedKind_Fails()
    {
        var fn = (IFunction<JToken>)new ScriptPath<JToken>().SetArguments(new Arguments<JToken>
        {
            new FixedValue<JToken>(new JValue("*")),
            new FixedValue<JToken>(new JArray(new[] { "bogus" })),
            new FixedValue<JToken>(new JValue(false))
        });
        var result = fn.Execute(data, data, executeOptions);

        Assert.That(result.Success, Is.False);
    }
}
