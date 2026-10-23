using Newtonsoft.Json.Linq;
using NUnit.Framework;
using TLio.Client;
using TLio.Core.Contracts;
using TLio.Core.Models;
using TLio.Functions.Collections;
using TLio.Json;

namespace TLio.Functions.Tests.FunctionsTests.CollectionTests;

/// <summary>
/// =resolve(collectionPath, keyField, keyValue [, returnField]). The match / return-field /
/// duplicate / key-type cases are fixtures (Fixtures/Collections/resolve); this holds what a
/// fixture cannot: the failure contract — a failed lookup fails the command, leaves the
/// document alone and says why in the log.
/// </summary>
[TestFixture]
public class ResolveTests
{
    private const string Doc = """{"party":[{"refKey":"p1","firstName":"Ada"},{"refKey":"p2","firstName":"Grace"}]}""";

    private ScriptEngine<JToken> engine = null!;
    private IExecutionContext<JToken> context = null!;

    [SetUp]
    public void Setup()
    {
        var options = ParseOptions<JToken>.CreateDefault();
        engine = new ScriptEngine<JToken>(options.CommandsProvider, options.FunctionsProvider);
        context = JsonExecutionContext.CreateDefault();
    }

    private TLioExecutionResult<JToken> Run(string value, out JToken data)
    {
        data = JToken.Parse(Doc);
        var script = new JArray(new JObject
        {
            ["command"] = "add", ["path"] = "$.out", ["value"] = value
        }).ToString();
        return engine.Execute(script, data, context);
    }

    [Test]
    public void NoMatch_FailsTheCommand_LeavesDocumentUnchanged_AndNamesTheArguments()
    {
        var result = Run("=resolve($.party, 'refKey', 'zzz', 'firstName')", out var data);

        Assert.That(result.Success, Is.False);
        Assert.That(data["out"], Is.Null);
        Assert.That(context.GetLogEntries().Any(e => e.Group == "resolve"
            && e.Message.Contains("refKey") && e.Message.Contains("zzz")), Is.True);
    }

    [Test]
    public void ReturnFieldMatchingNothing_FailsAndNamesTheField()
    {
        var result = Run("=resolve($.party, 'refKey', 'p1', 'lastName')", out var data);

        Assert.That(result.Success, Is.False);
        Assert.That(data["out"], Is.Null);
        Assert.That(context.GetLogEntries().Any(e => e.Group == "resolve" && e.Message.Contains("lastName")), Is.True);
    }

    [TestCase("=resolve($.party, 'refKey')")]
    [TestCase("=resolve($.party, 'refKey', 'p1', 'firstName', 'extra')")]
    public void WrongArgumentCount_FailsWithoutThrowing(string value)
    {
        var result = Run(value, out var data);

        Assert.That(result.Success, Is.False);
        Assert.That(data["out"], Is.Null);
    }

    [Test]
    public void CollectionPathMatchingNothing_IsNoMatch()
    {
        var result = Run("=resolve($.nowhere, 'refKey', 'p1')", out _);

        Assert.That(result.Success, Is.False);
    }

    [Test]
    public void RelativeCollectionPath_IsAnchoredOnTheCurrentNode()
    {
        // '@.party' is relative to the node the function is called from.
        var data = JToken.Parse("""{"deal":{"party":[{"refKey":"p1","firstName":"Ada"}]}}""");
        var fn = new ResolveFunction<JToken>();
        var args = new Arguments<JToken>();
        foreach (var a in new[] { "@.party", "refKey", "p1", "firstName" })
            args.Add(new FixedValue<JToken>(new JValue(a)));
        fn.SetArguments(args);

        var result = fn.Execute(data["deal"]!, data, context);

        Assert.That(result.Success, Is.True);
        Assert.That(result.Data.First!.Value<string>(), Is.EqualTo("Ada"));
    }
}
