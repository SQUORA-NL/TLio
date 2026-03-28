using Newtonsoft.Json.Linq;
using NUnit.Framework;
using TLio.Commands;
using TLio.Core.Contracts;
using TLio.Core.Models;
using TLio.Functions;
using TLio.Json;

namespace TLio.Functions.Tests.FunctionsTests;

/// <summary>
/// Ported from JLio.UnitTests.FunctionsTests.ScriptPathTests.
/// Adaptations:
///   - ExecutionContext.CreateDefault() → JsonExecutionContext.CreateDefault()
///   - NUnit classic API → Assert.That() constraint model (NUnit 4)
/// </summary>
[TestFixture]
public class ScriptPathTests
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
    public void CanGetCurrentNodePath()
    {
        // When used in Set at $.person.name, the current node is $.person.name
        // ScriptPath returns the path of the node it is computed at
        var scriptPathFn = new ScriptPath<JToken>();
        var script = new TLioScript<JToken>
        {
            new Set<JToken>("$.result", new FunctionSupportedValue<JToken>(scriptPathFn))
        };
        var result = script.Execute(data, executeOptions);

        Assert.That(result.Success, Is.True);
        var pathValue = result.Data.SelectToken("$.result")?.Value<string>();
        Assert.That(pathValue, Is.Not.Null);
        Assert.That(pathValue, Does.Contain("$"));
    }

    [Test]
    public void CanGetPathAsStringNode()
    {
        var scriptPathFn = new ScriptPath<JToken>();
        var result = scriptPathFn.Execute(data, data, executeOptions);

        Assert.That(result.Success, Is.True);
        Assert.That(result.Data.First, Is.Not.Null);
        Assert.That(result.Data.First!.Type, Is.EqualTo(JTokenType.String));
    }

    [Test]
    public void CanGetRootPath()
    {
        var scriptPathFn = new ScriptPath<JToken>();
        var result = scriptPathFn.Execute(data, data, executeOptions);

        Assert.That(result.Success, Is.True);
        Assert.That(result.Data.First?.Value<string>(), Is.EqualTo("$"));
    }
}
