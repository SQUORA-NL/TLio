using Microsoft.Extensions.Logging;
using Newtonsoft.Json.Linq;
using NUnit.Framework;
using TLio.Client;
using TLio.Core.Contracts;
using TLio.Core.Models;
using TLio.Functions.Collections;
using TLio.Json;

namespace TLio.Functions.Tests.FunctionsTests.CollectionTests;

/// <summary>
/// Tests for =distinct($.array).
///
/// The engine tests register the function locally: <c>distinct</c> is not part of
/// <c>ParseOptions.CreateDefault()</c> in this branch yet.
/// </summary>
[TestFixture]
public class DistinctTests
{
    private JToken data = null!;
    private IExecutionContext<JToken> context = null!;
    private ScriptEngine<JToken> engine = null!;

    [SetUp]
    public void Setup()
    {
        context = JsonExecutionContext.CreateDefault();
        data = JToken.Parse(@"{
            ""codes"":   [""wa"", ""casco"", ""wa"", ""rechtsbijstand"", ""casco""],
            ""numbers"": [3, 1, 3, 2, 1],
            ""objects"": [{ ""code"": ""wa"" }, { ""code"": ""casco"" }, { ""code"": ""wa"" }],
            ""empty"":   [],
            ""scalar"":  ""wa"",
            ""nullish"": null
        }");

        var options = ParseOptions<JToken>.CreateDefault();
        options.FunctionsProvider.Register("distinct", () => new Distinct<JToken>());
        engine = new ScriptEngine<JToken>(options.CommandsProvider, options.FunctionsProvider);
    }

    private static Distinct<JToken> Fn(params string[] args)
    {
        var fn = new Distinct<JToken>();
        var arguments = new Arguments<JToken>();
        foreach (var a in args) arguments.Add(new PathValue<JToken>(a));
        fn.SetArguments(arguments);
        return fn;
    }

    [Test]
    public void Distinct_KeepsFirstOccurrenceInOrder()
    {
        var result = Fn("$.codes").Execute(data, data, context);

        Assert.That(result.Success, Is.True);
        Assert.That(result.Data.First!.ToString(Newtonsoft.Json.Formatting.None),
            Is.EqualTo(@"[""wa"",""casco"",""rechtsbijstand""]"));
    }

    [Test]
    public void Distinct_WorksOnNumbers()
    {
        var result = Fn("$.numbers").Execute(data, data, context);

        Assert.That(result.Success, Is.True);
        Assert.That(result.Data.First!.ToString(Newtonsoft.Json.Formatting.None), Is.EqualTo("[3,1,2]"));
    }

    /// <summary>Objects de-duplicate structurally — DeepEquals, not reference equality.</summary>
    [Test]
    public void Distinct_ComparesObjectsStructurally()
    {
        var result = Fn("$.objects").Execute(data, data, context);

        Assert.That(result.Success, Is.True);
        Assert.That(result.Data.First!.Children().Count(), Is.EqualTo(2));
    }

    [Test]
    public void Distinct_ScalarBecomesOneElementArray()
    {
        var result = Fn("$.scalar").Execute(data, data, context);

        Assert.That(result.Success, Is.True);
        Assert.That(result.Data.First!.ToString(Newtonsoft.Json.Formatting.None), Is.EqualTo(@"[""wa""]"));
    }

    /// <summary>An empty array is an answer, not a failure.</summary>
    [Test]
    public void Distinct_EmptyArrayReturnsEmptyArray()
    {
        var result = Fn("$.empty").Execute(data, data, context);

        Assert.That(result.Success, Is.True);
        Assert.That(result.Data.First!.Type, Is.EqualTo(JTokenType.Array));
        Assert.That(result.Data.First!.Children().Count(), Is.EqualTo(0));
    }

    /// <summary>Found-but-null is a single non-array node, so it flattens to a one-element array.</summary>
    [Test]
    public void Distinct_FoundButNullReturnsSingleNullArray()
    {
        var result = Fn("$.nullish").Execute(data, data, context);

        Assert.That(result.Success, Is.True);
        Assert.That(result.Data.First!.ToString(Newtonsoft.Json.Formatting.None), Is.EqualTo("[null]"));
    }

    [Test]
    public void Distinct_PathNotFound_ReturnsFailed()
    {
        var result = Fn("$.missing").Execute(data, data, context);

        Assert.That(result.Success, Is.False);
        Assert.That(context.GetLogEntries().Any(e => e.Level == LogLevel.Error), Is.True);
    }

    [Test]
    public void Distinct_WrongArity_ReturnsFailed()
    {
        Assert.That(new Distinct<JToken>().Execute(data, data, context).Success, Is.False);
        Assert.That(Fn("$.codes", "$.numbers").Execute(data, data, context).Success, Is.False);
    }

    /// <summary>The source array must be left exactly as it was.</summary>
    [Test]
    public void Distinct_DoesNotMutateInput()
    {
        Fn("$.codes").Execute(data, data, context);

        Assert.That(data.SelectToken("$.codes")!.Children().Count(), Is.EqualTo(5));
    }

    [Test]
    public void Distinct_ThroughEngine_WritesArray()
    {
        const string script = @"[{ ""command"": ""put"", ""path"": ""$.unique"", ""value"": ""=distinct($.codes)"" }]";
        var result = engine.Execute(script, data, context);

        Assert.That(result.Success, Is.True);
        Assert.That(result.Data.SelectToken("$.unique")!.ToString(Newtonsoft.Json.Formatting.None),
            Is.EqualTo(@"[""wa"",""casco"",""rechtsbijstand""]"));
    }
}
