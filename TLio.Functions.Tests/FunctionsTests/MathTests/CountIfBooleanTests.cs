using Newtonsoft.Json.Linq;
using NUnit.Framework;
using TLio.Client;
using TLio.Core.Models;
using TLio.Extensions.Math;
using TLio.Json;

namespace TLio.Functions.Tests.FunctionsTests.MathTests;

/// <summary>
/// countif / countifs / sumifs over boolean ranges. A boolean node is neither a number nor text, so the
/// range value used to read as null and the count was always 0.
/// </summary>
[TestFixture]
public class CountIfBooleanTests
{
    private const string Document = """{ "flags": [true, false, true, true], "n": [1, 2, 3, 4] }""";

    private static JToken Run(string value)
    {
        var options = ParseOptions<JToken>.CreateDefault().WithMath();
        var engine = new ScriptEngine<JToken>(options.CommandsProvider, options.FunctionsProvider);
        var script = $$"""[{ "command": "add", "path": "$.out", "value": "{{value}}" }]""";
        var result = engine.Execute(script, JToken.Parse(Document), JsonExecutionContext.CreateDefault());
        Assert.That(result.Success, Is.True);
        return result.Data["out"]!;
    }

    [TestCase("=countif($.flags, true)", 3)]
    [TestCase("=countif($.flags, 'true')", 3)]
    [TestCase("=countif($.flags, false)", 1)]
    [TestCase("=countif($.flags, '<>true')", 1)]
    [TestCase("=countifs($.flags, true, $.n, '>1')", 2)]
    [TestCase("=countif($.n, 3)", 1)]
    public void CountIf_CountsBooleans(string expression, int expected) =>
        Assert.That(Run(expression).Value<int>(), Is.EqualTo(expected));
}
