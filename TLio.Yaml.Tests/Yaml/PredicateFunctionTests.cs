using NUnit.Framework;
using TLio.Client;
using TLio.Yaml;
using YamlDotNet.RepresentationModel;

namespace TLio.Yaml.Tests.Yaml;

/// <summary>
/// The predicates are format-agnostic: they run through the adapter, not through JSON.
/// YAML has no type system for scalars, so the type checks report the apparent type —
/// this fixture pins that behaviour down.
/// </summary>
[TestFixture]
public class PredicateFunctionTests
{
    private const string Document = """
    name: Sanne
    age: 37
    active: true
    tags:
      - a
      - b
    address:
      city: Utrecht
    """;

    private ScriptEngine<YamlNode> _engine = null!;

    [SetUp]
    public void Setup()
    {
        var options = ParseOptions<YamlNode>.CreateDefault();
        _engine = new ScriptEngine<YamlNode>(options.CommandsProvider, options.FunctionsProvider);
    }

    private string Eval(string expression)
    {
        var context = YamlExecutionContext.CreateDefault();
        var script = $@"[{{ ""command"": ""add"", ""path"": ""$.out"", ""value"": ""{expression}"" }}]";
        var data = context.NodeAdapter.Parse(Document);
        var result = _engine.Execute(script, data, context);
        var node = ((YamlMappingNode)result.Data)[new YamlScalarNode("out")];
        return ((YamlScalarNode)node).Value!;
    }

    [TestCase("=equals($.name, 'Sanne')", "true")]
    [TestCase("=equals($.age, 37)", "true")]
    [TestCase("=greaterThan($.age, 30)", "true")]
    [TestCase("=lessThan($.age, 30)", "false")]
    [TestCase("=and(equals($.name, 'Sanne'), greaterThan($.age, 30))", "true")]
    [TestCase("=not(equals($.name, 'Other'))", "true")]
    [TestCase("=exists($.address.city)", "true")]
    [TestCase("=exists($.address.zip)", "false")]
    [TestCase("=isNull($.missing)", "true")]
    [TestCase("=in($.name, 'Sanne', 'Jan')", "true")]
    [TestCase("=matches($.name, '^S')", "true")]
    [TestCase("=isArray($.tags)", "true")]
    [TestCase("=isObject($.address)", "true")]
    public void Predicates_WorkOnYaml(string expression, string expected)
        => Assert.That(Eval(expression), Is.EqualTo(expected));

    [Test]
    public void TypeChecks_OnUntypedScalars_ReportApparentType()
    {
        // YAML scalars carry no type, so a numeric-looking scalar reads as a number.
        Assert.That(Eval("=isNumber($.age)"), Is.EqualTo("true"));
        Assert.That(Eval("=isString($.name)"), Is.EqualTo("true"));
        Assert.That(Eval("=isBoolean($.active)"), Is.EqualTo("true"));
    }
}
