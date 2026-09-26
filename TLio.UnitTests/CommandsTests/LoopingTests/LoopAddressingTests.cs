using Newtonsoft.Json.Linq;
using NUnit.Framework;
using TLio.Client;
using TLio.Core.Models;
using TLio.Extensions.Looping;
using TLio.Json;

namespace TLio.UnitTests.CommandsTests.LoopingTests;

/// <summary>
/// End-to-end, through real script text: proves a nested command reaches the loop's current
/// element via the ordinary <c>@</c> relative-path token — the same one
/// <c>decisionTable</c>/<c>resolve</c>/<c>setProperties</c> already use — parsed by the real
/// <see cref="ScriptEngine{TNode}"/> pipeline rather than hand-built command objects.
/// </summary>
[TestFixture]
public class LoopAddressingTests
{
    private ScriptEngine<JToken> engine = null!;

    [SetUp]
    public void Setup()
    {
        var options = ParseOptions<JToken>.CreateDefault();
        options.CommandsProvider.RegisterLooping<JToken>();
        engine = new ScriptEngine<JToken>(options.CommandsProvider, options.FunctionsProvider);
    }

    private (string Document, List<string> Log) Run(string scriptJson, string input)
    {
        var context = JsonExecutionContext.CreateDefault();
        var script = engine.Parse(scriptJson, ScriptFormat.Json, context.NodeAdapter);
        var result = engine.Execute(script, JToken.Parse(input), context);
        return (result.Data.ToString(Newtonsoft.Json.Formatting.None),
                context.GetLogEntries().Select(e => $"{e.Level}|{e.Message}").ToList());
    }

    [Test]
    public void BareAt_ReplacesTheWholeElementWithATemplateReadingOneOfItsFields()
    {
        // "=fetch(@.v)" reads a field off the element as it was *before* this command runs;
        // "@" as the whole Path then replaces that same element with the template's result.
        // (Bare "@" with no field — reading the *whole* element as a value via e.g. "=fetch(@)"
        // — is deliberately not supported: see IItemsFetcher.IsPathExpression's remarks on why
        // a lone "@"/"$"/"." can't be told apart from ordinary literal text such as XML's "."
        // used as a decimal point or padding character.)
        var (document, _) = Run("""
            [ { "command": "forEach", "path": "$.items", "commands": [
                  { "command": "set", "path": "@",
                    "value": { "original": "=fetch(@.v)", "tag": "visited" } }
              ] } ]
            """, """{ "items": [{"v":"a"},{"v":"b"},{"v":"c"}] }""");

        Assert.That(document, Is.EqualTo(
            """{"items":[{"original":"a","tag":"visited"},{"original":"b","tag":"visited"},{"original":"c","tag":"visited"}]}"""));
    }

    [Test]
    public void AtDotField_AddsOnlyThatFieldToEachElement()
    {
        var (document, _) = Run("""
            [ { "command": "forEach", "path": "$.items", "commands": [
                  { "command": "add", "path": "@.tag", "value": "visited" }
              ] } ]
            """, """{ "items": [{"id":1},{"id":2}] }""");

        Assert.That(document, Is.EqualTo("""{"items":[{"id":1,"tag":"visited"},{"id":2,"tag":"visited"}]}"""));
    }

    [Test]
    public void AtIsReadableWhileWritingToAnUnrelatedAbsolutePath()
    {
        // "put path=$.lastSeen" has nothing "@"-relative about it at all — only the *value*
        // references "@.v". Proves context.CurrentNode, not the command's own resolved target,
        // is what "@" anchors to inside a loop.
        var (document, _) = Run("""
            [ { "command": "forEach", "path": "$.items", "commands": [
                  { "command": "put", "path": "$.lastSeen", "value": "=fetch(@.v)" }
              ] } ]
            """, """{ "items": [{"v":"a"},{"v":"b"},{"v":"c"}] }""");

        Assert.That(document, Does.Contain("\"lastSeen\":\"c\""));
    }

    [Test]
    public void NoLoopScaffoldingLeaksIntoTheFinalDocument()
    {
        var (document, _) = Run("""
            [ { "command": "forEach", "path": "$.items", "commands": [
                  { "command": "add", "path": "@.tag", "value": "x" } ] } ]
            """, """{ "items": [{},{}] }""");

        Assert.That(document, Does.Not.Contain("__loop"));
        Assert.That(document, Does.Not.Contain("appendTo"));
    }
}
