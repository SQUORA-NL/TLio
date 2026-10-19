using System.Text.Json.Nodes;
using Newtonsoft.Json.Linq;
using NUnit.Framework;
using TLio.Client;
using TLio.Json;
using TLio.Json.SystemText;

namespace TLio.Json.AdapterParity.Tests;

/// <summary>
/// Writing commands aimed at arrays and at the less common JSONPath spellings (recursive
/// descent, bracket-quoted names, unions, the bare wildcard), checked against the same expected
/// document through BOTH JSON adapters. These are the cases a JLio-to-TLio differential run found
/// diverging: add skipped an array it should have appended to, <c>$..t</c> overwrote the arrays
/// it matched, <c>$.rows[*].t[1]</c> threw, and <c>$['f']['tags']</c> created a property called
/// <c>f']['tags</c>.
/// </summary>
[TestFixture]
public class ArrayTargetParityTests
{
    private const string Doc =
        """{"f":{"tags":[1,2,3],"n":1,"o":{"x":1}},"rows":[{"id":1,"t":[1]},{"id":2,"t":[2]}]}""";

    private static string RunNewtonsoft(string script, string doc)
    {
        var options = ParseOptions<JToken>.CreateDefault();
        var engine = new ScriptEngine<JToken>(options.CommandsProvider, options.FunctionsProvider);
        return engine.Execute(script, JToken.Parse(doc), JsonExecutionContext.CreateDefault())
            .Data.ToString(Newtonsoft.Json.Formatting.None);
    }

    private static string RunSystemText(string script, string doc)
    {
        var options = ParseOptions<JsonNode>.CreateDefault();
        var engine = new ScriptEngine<JsonNode>(options.CommandsProvider, options.FunctionsProvider);
        return engine.Execute(script, JsonNode.Parse(doc)!, SystemTextJsonExecutionContext.CreateDefault())
            .Data.ToJsonString();
    }

    private static string Canonical(string json) => JToken.Parse(json).ToString(Newtonsoft.Json.Formatting.None);

    [TestCase("""[{"command":"add","path":"$.f.tags","value":4}]""",
        """{"f":{"tags":[1,2,3,4],"n":1,"o":{"x":1}},"rows":[{"id":1,"t":[1]},{"id":2,"t":[2]}]}""",
        TestName = "add: path names an existing array")]
    [TestCase("""[{"command":"add","path":"$.f","property":"tags","value":4}]""",
        """{"f":{"tags":[1,2,3,4],"n":1,"o":{"x":1}},"rows":[{"id":1,"t":[1]},{"id":2,"t":[2]}]}""",
        TestName = "add: property names an existing array")]
    [TestCase("""[{"command":"add","path":"$.rows[*]","property":"t","value":9}]""",
        """{"f":{"tags":[1,2,3],"n":1,"o":{"x":1}},"rows":[{"id":1,"t":[1,9]},{"id":2,"t":[2,9]}]}""",
        TestName = "add: property on every row appends to each array")]
    [TestCase("""[{"command":"add","path":"$..t","value":7}]""",
        """{"f":{"tags":[1,2,3],"n":1,"o":{"x":1}},"rows":[{"id":1,"t":[1,7]},{"id":2,"t":[2,7]}]}""",
        TestName = "add: recursive descent appends instead of replacing the arrays")]
    [TestCase("""[{"command":"add","path":"$..n","value":9}]""",
        """{"f":{"tags":[1,2,3],"n":1,"o":{"x":1}},"rows":[{"id":1,"t":[1]},{"id":2,"t":[2]}]}""",
        TestName = "add: recursive descent never overwrites a scalar")]
    [TestCase("""[{"command":"add","path":"$.f.n","value":5}]""",
        """{"f":{"tags":[1,2,3],"n":1,"o":{"x":1}},"rows":[{"id":1,"t":[1]},{"id":2,"t":[2]}]}""",
        TestName = "add: an existing scalar is left alone")]
    [TestCase("""[{"command":"add","path":"$.rows[*].t[1]","value":9}]""",
        """{"f":{"tags":[1,2,3],"n":1,"o":{"x":1}},"rows":[{"id":1,"t":[1,9]},{"id":2,"t":[2,9]}]}""",
        TestName = "add: next index through a wildcard")]
    [TestCase("""[{"command":"put","path":"$.rows[*].t[1]","value":9}]""",
        """{"f":{"tags":[1,2,3],"n":1,"o":{"x":1}},"rows":[{"id":1,"t":[1,9]},{"id":2,"t":[2,9]}]}""",
        TestName = "put: next index through a wildcard")]
    [TestCase("""[{"command":"set","path":"$['f']['n']","value":5}]""",
        """{"f":{"tags":[1,2,3],"n":5,"o":{"x":1}},"rows":[{"id":1,"t":[1]},{"id":2,"t":[2]}]}""",
        TestName = "set: chained bracket-quoted names")]
    [TestCase("""[{"command":"put","path":"$['f']['n']","value":5}]""",
        """{"f":{"tags":[1,2,3],"n":5,"o":{"x":1}},"rows":[{"id":1,"t":[1]},{"id":2,"t":[2]}]}""",
        TestName = "put: chained bracket-quoted names")]
    [TestCase("""[{"command":"add","path":"$['f']['tags']","value":4}]""",
        """{"f":{"tags":[1,2,3,4],"n":1,"o":{"x":1}},"rows":[{"id":1,"t":[1]},{"id":2,"t":[2]}]}""",
        TestName = "add: chained bracket-quoted names onto an array")]
    [TestCase("""[{"command":"set","path":"$['f','rows']","value":0}]""",
        """{"f":0,"rows":0}""",
        TestName = "set: a union of names sets each")]
    [TestCase("""[{"command":"set","path":"$.f.*","value":0}]""",
        """{"f":{"tags":0,"n":0,"o":0},"rows":[{"id":1,"t":[1]},{"id":2,"t":[2]}]}""",
        TestName = "set: the bare wildcard sets each child")]
    public void BothAdaptersAgreeAndMatchTheExpectedDocument(string script, string expected)
    {
        var newtonsoft = RunNewtonsoft(script, Doc);
        var systemText = RunSystemText(script, Doc);

        Assert.That(Canonical(newtonsoft), Is.EqualTo(Canonical(expected)), "Newtonsoft adapter");
        Assert.That(Canonical(systemText), Is.EqualTo(Canonical(expected)), "System.Text.Json adapter");
    }
}
