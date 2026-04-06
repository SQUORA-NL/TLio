using NUnit.Framework;
using TLio.Client;
using TLio.Extensions.Text;
using TLio.Yaml;
using YamlDotNet.RepresentationModel;

namespace TLio.Yaml.Tests.Yaml;

/// <summary>
/// Verifies that the TLio.Extensions.Text pack works with the YAML adapter (SC-003).
/// These are inline tests — no fixture files required.
/// </summary>
[TestFixture]
public class TextFunctionTests
{
    private ScriptEngine<YamlNode> _engine = null!;

    [SetUp]
    public void SetUp()
    {
        var options = ParseOptions<YamlNode>.CreateDefault();
        options.FunctionsProvider.RegisterText<YamlNode>();
        _engine = new ScriptEngine<YamlNode>(options.CommandsProvider, options.FunctionsProvider);
    }

    [Test]
    public void ToLower_OnYamlInput_ReturnsLowercaseString()
    {
        const string script = "[{\"command\":\"add\",\"path\":\"$.nameLower\",\"value\":\"=toLower($.name)\"}]";
        const string yamlInput = "name: Alice";

        var context = YamlExecutionContext.CreateDefault();
        var input   = context.NodeAdapter.Parse(yamlInput);
        var result  = _engine.Execute(script, input, context);

        Assert.That(result.Success, Is.True);
        var nameLower = context.ItemsFetcher.SelectNode("$.nameLower", result.Data!);
        Assert.That(context.NodeAdapter.TryGetString(nameLower!), Is.EqualTo("alice"));
    }

    [Test]
    public void Concat_OnYamlInput_ReturnsConcatenatedString()
    {
        const string script = "[{\"command\":\"add\",\"path\":\"$.full\",\"value\":\"=concat($.first,' ',$.last)\"}]";
        const string yamlInput = "first: Alice\nlast: Smith";

        var context = YamlExecutionContext.CreateDefault();
        var input   = context.NodeAdapter.Parse(yamlInput);
        var result  = _engine.Execute(script, input, context);

        Assert.That(result.Success, Is.True);
        var full = context.ItemsFetcher.SelectNode("$.full", result.Data!);
        Assert.That(context.NodeAdapter.TryGetString(full!), Is.EqualTo("Alice Smith"));
    }
}
