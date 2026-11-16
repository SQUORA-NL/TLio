using Newtonsoft.Json.Linq;
using NUnit.Framework;
using TLio.Client;
using TLio.Core.Models;
using TLio.Extensions.Looping;
using TLio.Json;

namespace TLio.UnitTests.CommandsTests.LoopingTests;

/// <summary>
/// A bare <c>@</c> as a function argument is the current node — <c>=filterBySchema($.schema, @)</c> inside
/// forEach. It used to be handed to the function as the text "@" while the put reported success.
/// </summary>
[TestFixture]
public class CurrentNodeAsFunctionArgumentTests
{
    private const string Document = """
        { "schema": { "properties": { "id": {"type":"integer"} } },
          "items": [ { "id": 1, "extra": "x" }, { "id": 2, "extra": "y" } ] }
        """;

    [TestCase("@")]
    [TestCase("@.")]
    public void FilterBySchema_WithCurrentNode_FiltersEachItem(string current)
    {
        var options = ParseOptions<JToken>.CreateDefault().WithLooping();
        var engine = new ScriptEngine<JToken>(options.CommandsProvider, options.FunctionsProvider);
        var script = $$"""
            [{ "command": "forEach", "path": "$.items", "commands": [
                 { "command": "put", "path": "@.filtered", "value": "=filterBySchema($.schema, {{current}})" } ] }]
            """;

        var result = engine.Execute(script, JToken.Parse(Document), JsonExecutionContext.CreateDefault());

        Assert.That(result.Success, Is.True);
        Assert.That(result.Data["items"]![0]!["filtered"]!.ToString(Newtonsoft.Json.Formatting.None), Is.EqualTo("""{"id":1}"""));
        Assert.That(result.Data["items"]![1]!["filtered"]!.ToString(Newtonsoft.Json.Formatting.None), Is.EqualTo("""{"id":2}"""));
    }
}
