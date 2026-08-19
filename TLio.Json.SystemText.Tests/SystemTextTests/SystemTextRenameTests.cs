using System.Text.Json.Nodes;
using Microsoft.Extensions.Logging;
using NUnit.Framework;
using TLio.Client;
using TLio.Json.SystemText;

namespace TLio.Json.SystemText.Tests.SystemTextTests;

/// <summary>
/// Rename over System.Text.Json. JsonObject has no rename primitive and re-adding a key
/// appends it, so the adapter rebuilds the object in order; nodes are re-attached rather
/// than cloned. The root has no key and therefore no name.
/// </summary>
[TestFixture]
public class SystemTextRenameTests
{
    private ScriptEngine<JsonNode> _engine = null!;

    [SetUp]
    public void SetUp()
    {
        var options = ParseOptions<JsonNode>.CreateDefault();
        _engine = new ScriptEngine<JsonNode>(options.CommandsProvider, options.FunctionsProvider);
    }

    [Test]
    public void Rename_Property_ChangesTheKeyAndKeepsTheValue()
    {
        var context = SystemTextJsonExecutionContext.CreateDefault();
        var input = context.NodeAdapter.Parse("""{"first":"Ada","last":"Lovelace"}""");

        var result = _engine.Execute(
            """[{"command":"rename","path":"$.first","name":"given"}]""", input, context);

        Assert.That(result.Success, Is.True);
        Assert.That(context.NodeAdapter.Serialize(result.Data!),
            Is.EqualTo("""{"given":"Ada","last":"Lovelace"}"""),
            "the renamed key keeps its original position");
    }

    [Test]
    public void Rename_NestedObject_KeepsItsChildren()
    {
        var context = SystemTextJsonExecutionContext.CreateDefault();
        var input = context.NodeAdapter.Parse("""{"address":{"town":"Amsterdam"}}""");

        var result = _engine.Execute(
            """[{"command":"rename","path":"$.address","name":"location"}]""", input, context);

        Assert.That(result.Success, Is.True);
        Assert.That(context.NodeAdapter.Serialize(result.Data!),
            Is.EqualTo("""{"location":{"town":"Amsterdam"}}"""));
    }

    [Test]
    public void Rename_TheRoot_WarnsBecauseItHasNoNameOfItsOwn()
    {
        var context = SystemTextJsonExecutionContext.CreateDefault();
        var input = context.NodeAdapter.Parse("""{"first":"Ada"}""");

        var result = _engine.Execute(
            """[{"command":"rename","path":"$","name":"opdracht"}]""", input, context);

        Assert.That(result.Success, Is.True);
        Assert.That(context.NodeAdapter.Serialize(result.Data!), Is.EqualTo("""{"first":"Ada"}"""));
        Assert.That(context.GetLogEntries().Any(e =>
            e.Level == LogLevel.Warning && e.Message.Contains("no name to change")), Is.True);
    }

    [Test]
    public void Rename_MissingPath_WarnsAndContinues()
    {
        var context = SystemTextJsonExecutionContext.CreateDefault();
        var input = context.NodeAdapter.Parse("""{"first":"Ada"}""");

        var result = _engine.Execute(
            """[{"command":"rename","path":"$.missing","name":"given"}]""", input, context);

        Assert.That(result.Success, Is.True);
        Assert.That(context.GetLogEntries().Any(e =>
            e.Level == LogLevel.Warning && e.Message.Contains("no nodes matched")), Is.True);
    }
}
