using Newtonsoft.Json.Linq;
using NUnit.Framework;
using TLio.Client;
using TLio.Core.Contracts;
using TLio.Extensions.Looping;
using TLio.Json;

namespace TLio.UnitTests.CommandsTests.LoopingTests;

/// <summary>
/// <c>with</c> — evaluate a path once, run a nested script with <c>@</c> on the node it selected.
/// Driven through the engine (script text in, document out) because that is how a script author
/// meets it, and because the nested commands are parsed by the same binder as forEach's.
/// </summary>
[TestFixture]
public class WithTests
{
    private ScriptEngine<JToken> engine = null!;
    private IExecutionContext<JToken> context = null!;

    [SetUp]
    public void Setup()
    {
        var options = ParseOptions<JToken>.CreateDefault();
        options.CommandsProvider.RegisterLooping<JToken>();
        engine = new ScriptEngine<JToken>(options.CommandsProvider, options.FunctionsProvider);
        context = JsonExecutionContext.CreateDefault();
    }

    private TLioExecutionResultOf Run(string script, string document)
    {
        var data = JToken.Parse(document);
        var result = engine.Execute(script, data, context);
        return new TLioExecutionResultOf(result.Success, result.Data);
    }

    private sealed record TLioExecutionResultOf(bool Success, JToken Data);

    private bool Warned(string fragment) =>
        context.GetLogEntries().Any(e => e.Level == Microsoft.Extensions.Logging.LogLevel.Warning && e.Message.Contains(fragment));

    [Test]
    public void OneMatch_NestedCommandsAddressTheNodeWithAt()
    {
        var r = Run("""
            [ { "command": "with", "path": "$.party[1]", "commands": [
                { "command": "add", "path": "$.out.name", "value": "=fetch(@.name)" },
                { "command": "add", "path": "$.out.copy", "value": "@.name" },
                { "command": "add", "path": "@.seen", "value": true }
            ] } ]
            """, """{"party":[{"name":"Ada"},{"name":"Grace"}]}""");

        Assert.That(r.Success, Is.True);
        Assert.That(r.Data.SelectToken("$.out.name")!.Value<string>(), Is.EqualTo("Grace"));
        Assert.That(r.Data.SelectToken("$.out.copy")!.Value<string>(), Is.EqualTo("Grace"));
        Assert.That(r.Data.SelectToken("$.party[1].seen")!.Value<bool>(), Is.True);
        Assert.That(r.Data.SelectToken("$.party[0].seen"), Is.Null);
    }

    [Test]
    public void ZeroMatches_WarnsSkipsTheBlockAndTheScriptContinues()
    {
        var r = Run("""
            [ { "command": "with", "path": "$.nobody", "commands": [
                  { "command": "add", "path": "$.inside", "value": 1 } ] },
              { "command": "add", "path": "$.after", "value": 2 } ]
            """, """{"x":1}""");

        Assert.That(r.Success, Is.True);
        Assert.That(r.Data.SelectToken("$.inside"), Is.Null);
        Assert.That(r.Data.SelectToken("$.after")!.Value<int>(), Is.EqualTo(2));
        Assert.That(Warned("matched nothing"), Is.True);
    }

    [Test]
    public void SeveralMatches_UsesTheFirstAndWarns()
    {
        var r = Run("""
            [ { "command": "with", "path": "$.party[*]", "commands": [
                  { "command": "add", "path": "$.picked", "value": "@.name" } ] } ]
            """, """{"party":[{"name":"Ada"},{"name":"Grace"}]}""");

        Assert.That(r.Success, Is.True);
        Assert.That(r.Data.SelectToken("$.picked")!.Value<string>(), Is.EqualTo("Ada"));
        Assert.That(Warned("matched 2 nodes"), Is.True);
    }

    [Test]
    public void CurrentNode_IsRestoredAfterTheBlock()
    {
        Run("""
            [ { "command": "with", "path": "$.a", "commands": [
                  { "command": "add", "path": "@.x", "value": 1 } ] } ]
            """, """{"a":{}}""");

        Assert.That(context.CurrentNode, Is.Null);
    }

    [Test]
    public void CurrentNode_IsRestoredWhenANestedCommandFails()
    {
        var r = Run("""
            [ { "command": "with", "path": "$.a", "commands": [
                  { "command": "add", "path": "$.out", "value": "=fetch($.missing)" } ] },
              { "command": "add", "path": "$.after", "value": 1 } ]
            """, """{"a":{}}""");

        Assert.That(r.Success, Is.False);
        Assert.That(context.CurrentNode, Is.Null);
        Assert.That(r.Data.SelectToken("$.after"), Is.Null, "the script stops at the failing block");
    }

    [Test]
    public void With_InsideWith_InnerAtIsTheInnerNode_OuterAtIsBackAfterwards()
    {
        var r = Run("""
            [ { "command": "with", "path": "$.person", "commands": [
                  { "command": "with", "path": "@.address", "commands": [
                      { "command": "add", "path": "$.out.city", "value": "@.city" } ] },
                  { "command": "add", "path": "$.out.name", "value": "@.name" } ] } ]
            """, """{"person":{"name":"Ada","address":{"city":"Delft"}}}""");

        Assert.That(r.Success, Is.True);
        Assert.That(r.Data.SelectToken("$.out.city")!.Value<string>(), Is.EqualTo("Delft"));
        Assert.That(r.Data.SelectToken("$.out.name")!.Value<string>(), Is.EqualTo("Ada"));
    }

    [Test]
    public void With_InsideForEach_EachIterationKeepsItsOwnElementAfterTheWith()
    {
        var r = Run("""
            [ { "command": "forEach", "path": "$.people", "commands": [
                  { "command": "with", "path": "@.address", "commands": [
                      { "command": "add", "path": "@.checked", "value": true } ] },
                  { "command": "add", "path": "@.done", "value": true } ] } ]
            """, """{"people":[{"address":{"city":"A"}},{"address":{"city":"B"}}]}""");

        Assert.That(r.Success, Is.True);
        foreach (var i in new[] { 0, 1 })
        {
            Assert.That(r.Data.SelectToken($"$.people[{i}].address.checked")!.Value<bool>(), Is.True);
            Assert.That(r.Data.SelectToken($"$.people[{i}].done")!.Value<bool>(), Is.True, $"person {i}");
            Assert.That(r.Data.SelectToken($"$.people[{i}].address.done"), Is.Null);
        }
    }

    [Test]
    public void ForEach_InsideWith_IteratesTheSelectedNodesArray()
    {
        var r = Run("""
            [ { "command": "with", "path": "$.team", "commands": [
                  { "command": "forEach", "path": "@.members", "commands": [
                      { "command": "add", "path": "@.seen", "value": true } ] },
                  { "command": "add", "path": "@.visited", "value": true } ] } ]
            """, """{"team":{"members":[{"n":1},{"n":2}]}}""");

        Assert.That(r.Success, Is.True);
        Assert.That(r.Data.SelectToken("$.team.members[0].seen")!.Value<bool>(), Is.True);
        Assert.That(r.Data.SelectToken("$.team.members[1].seen")!.Value<bool>(), Is.True);
        Assert.That(r.Data.SelectToken("$.team.visited")!.Value<bool>(), Is.True);
    }

    [Test]
    public void ForEach_InsideWith_CanReachTheWithNodeThroughParentNavigation()
    {
        // The parent step is semantic: an array element's parent is the object owning the
        // array, so one "<--" from a member is the team.
        var r = Run("""
            [ { "command": "with", "path": "$.team", "commands": [
                  { "command": "forEach", "path": "@.members", "commands": [
                      { "command": "add", "path": "@.owner", "value": "=fetch(@.<--.name)" } ] } ] } ]
            """, """{"team":{"name":"Core","members":[{"n":1},{"n":2}]}}""");

        Assert.That(r.Success, Is.True);
        Assert.That(r.Data.SelectToken("$.team.members[0].owner")!.Value<string>(), Is.EqualTo("Core"));
        Assert.That(r.Data.SelectToken("$.team.members[1].owner")!.Value<string>(), Is.EqualTo("Core"));
    }

    [Test]
    public void IndirectPath_IsResolvedBeforeSelecting()
    {
        var r = Run("""
            [ { "command": "with", "path": "=indirect($.pointer)", "commands": [
                  { "command": "add", "path": "$.out", "value": "@.name" } ] } ]
            """, """{"pointer":"$.party[1]","party":[{"name":"Ada"},{"name":"Grace"}]}""");

        Assert.That(r.Success, Is.True);
        Assert.That(r.Data.SelectToken("$.out")!.Value<string>(), Is.EqualTo("Grace"));
    }

    [Test]
    public void MissingPath_FailsValidation()
    {
        var r = Run("""[ { "command": "with", "commands": [ { "command": "add", "path": "$.a", "value": 1 } ] } ]""",
            "{}");

        Assert.That(r.Success, Is.False);
    }

    [Test]
    public void EmptyBlock_IsANoOp()
    {
        var r = Run("""[ { "command": "with", "path": "$.a", "commands": [] } ]""", """{"a":{}}""");

        Assert.That(r.Success, Is.True);
    }

    [Test]
    public void TitleAndDescription_AreAccepted()
    {
        var r = Run("""
            [ { "command": "with", "path": "$.a", "title": "t", "description": "d", "commands": [
                  { "command": "add", "path": "@.x", "value": 1 } ] } ]
            """, """{"a":{}}""");

        Assert.That(r.Success, Is.True);
        Assert.That(r.Data.SelectToken("$.a.x")!.Value<int>(), Is.EqualTo(1));
    }
}
