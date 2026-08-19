using Microsoft.Extensions.Logging;
using NUnit.Framework;
using TLio.Client;
using TLio.Yaml;
using YamlDotNet.RepresentationModel;

namespace TLio.Yaml.Tests.Commands;

/// <summary>
/// A YAML node is named by the mapping key that holds it, so rename rewrites that key and
/// the document root — held by nothing — has no name to change. Key order survives.
/// </summary>
[TestFixture]
public class YamlRenameTests
{
    private ScriptEngine<YamlNode> _engine = null!;

    [SetUp]
    public void SetUp()
    {
        var options = ParseOptions<YamlNode>.CreateDefault();
        _engine = new ScriptEngine<YamlNode>(options.CommandsProvider, options.FunctionsProvider);
    }

    [Test]
    public void Rename_MappingKey_ChangesTheKeyAndKeepsTheValue()
    {
        var context = YamlExecutionContext.CreateDefault();
        var input = context.NodeAdapter.Parse("first: Ada\nlast: Lovelace");

        var result = _engine.Execute(
            """[{"command":"rename","path":"$.first","name":"given"}]""", input, context);

        Assert.That(result.Success, Is.True);
        Assert.That(context.NodeAdapter.TryGetString(
            context.ItemsFetcher.SelectNode("$.given", result.Data!)!), Is.EqualTo("Ada"));
        Assert.That(context.ItemsFetcher.SelectNode("$.first", result.Data!), Is.Null);
    }

    [Test]
    public void Rename_MappingKey_KeepsKeyOrder()
    {
        var context = YamlExecutionContext.CreateDefault();
        var input = context.NodeAdapter.Parse("town: Amsterdam\nzip: '1011'");

        var result = _engine.Execute(
            """[{"command":"rename","path":"$.town","name":"city"}]""", input, context);

        var keys = ((YamlMappingNode)result.Data!).Children.Keys
            .OfType<YamlScalarNode>().Select(k => k.Value).ToList();
        Assert.That(keys, Is.EqualTo(new[] { "city", "zip" }),
            "the mapping is rebuilt in place rather than having the new key appended");
    }

    [Test]
    public void Rename_NestedMapping_KeepsItsChildren()
    {
        var context = YamlExecutionContext.CreateDefault();
        var input = context.NodeAdapter.Parse("address:\n  town: Amsterdam");

        var result = _engine.Execute(
            """[{"command":"rename","path":"$.address","name":"location"}]""", input, context);

        Assert.That(result.Success, Is.True);
        Assert.That(context.NodeAdapter.TryGetString(
            context.ItemsFetcher.SelectNode("$.location.town", result.Data!)!), Is.EqualTo("Amsterdam"));
    }

    [Test]
    public void Rename_TheDocumentRoot_WarnsBecauseItHasNoNameOfItsOwn()
    {
        var context = YamlExecutionContext.CreateDefault();
        var input = context.NodeAdapter.Parse("first: Ada");

        var result = _engine.Execute(
            """[{"command":"rename","path":"$","name":"opdracht"}]""", input, context);

        Assert.That(result.Success, Is.True, "a root with no name is a no-op, not a failure");
        Assert.That(context.NodeAdapter.TryGetString(
            context.ItemsFetcher.SelectNode("$.first", result.Data!)!), Is.EqualTo("Ada"));
        Assert.That(context.GetLogEntries().Any(e =>
            e.Level == LogLevel.Warning && e.Message.Contains("no name to change")), Is.True);
    }

    [Test]
    public void Rename_MissingPath_WarnsAndContinues()
    {
        var context = YamlExecutionContext.CreateDefault();
        var input = context.NodeAdapter.Parse("first: Ada");

        var result = _engine.Execute(
            """[{"command":"rename","path":"$.missing","name":"given"}]""", input, context);

        Assert.That(result.Success, Is.True);
        Assert.That(context.GetLogEntries().Any(e =>
            e.Level == LogLevel.Warning && e.Message.Contains("no nodes matched")), Is.True);
    }
}
