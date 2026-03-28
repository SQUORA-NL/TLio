using Newtonsoft.Json.Linq;
using NUnit.Framework;
using TLio.Commands;
using TLio.Core.Contracts;
using TLio.Core.Models;
using TLio.Functions;
using TLio.Json;

namespace TLio.Functions.Tests.FunctionsTests;

/// <summary>
/// Ported from JLio.UnitTests.FunctionsTests.PromoteTests.
/// Adaptations:
///   - ExecutionContext.CreateDefault() → JsonExecutionContext.CreateDefault()
///   - NUnit classic API → Assert.That() constraint model (NUnit 4)
/// </summary>
[TestFixture]
public class PromoteTests
{
    private JToken data = null!;
    private IExecutionContext<JToken> executeOptions = null!;

    [SetUp]
    public void Setup()
    {
        executeOptions = JsonExecutionContext.CreateDefault();
        data = JToken.Parse(
            "{ \"person\": { \"name\": \"Alice\", \"age\": 30 }, \"result\": null }");
    }

    [Test]
    public void CanPromoteNestedObject()
    {
        // promote($.person) wraps person in {"person": {...}}
        var promoteFn = (IFunction<JToken>)new Promote<JToken>()
            .SetArguments(new Arguments<JToken> { new FixedValue<JToken>(new JValue("$.person")) });
        var script = new TLioScript<JToken>
        {
            new Set<JToken>("$.result", new FunctionSupportedValue<JToken>(promoteFn))
        };
        var result = script.Execute(data, executeOptions);

        Assert.That(result.Success, Is.True);
        // result should be {"person": {"name":"Alice","age":30}}
        Assert.That(result.Data.SelectToken("$.result.person"), Is.Not.Null);
        Assert.That(result.Data.SelectToken("$.result.person.name")?.Value<string>(), Is.EqualTo("Alice"));
    }

    [Test]
    public void PromoteWrapsWithParentPropertyName()
    {
        var promoteFn = (IFunction<JToken>)new Promote<JToken>()
            .SetArguments(new Arguments<JToken> { new FixedValue<JToken>(new JValue("$.person")) });
        var result = promoteFn.Execute(data, data, executeOptions);

        Assert.That(result.Success, Is.True);
        Assert.That(result.Data.First, Is.Not.Null);
        Assert.That(result.Data.First!.Type, Is.EqualTo(JTokenType.Object));
        var promoted = (JObject)result.Data.First!;
        Assert.That(promoted.ContainsKey("person"), Is.True);
    }

    [Test]
    public void ReturnsFalseWithNoArguments()
    {
        var promoteFn = new Promote<JToken>();
        var result = promoteFn.Execute(data, data, executeOptions);

        Assert.That(result.Success, Is.False);
    }

    [Test]
    public void ReturnsFalseForNonExistentPath()
    {
        var promoteFn = (IFunction<JToken>)new Promote<JToken>()
            .SetArguments(new Arguments<JToken> { new FixedValue<JToken>(new JValue("$.nonExistent")) });
        var result = promoteFn.Execute(data, data, executeOptions);

        Assert.That(result.Success, Is.False);
    }
}
