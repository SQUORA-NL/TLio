using Microsoft.Extensions.Logging;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using NUnit.Framework;
using TLio.Client;
using TLio.Core.Contracts;
using TLio.Core.Models;
using TLio.Functions.Collections;
using TLio.Json;

namespace TLio.Functions.Tests.FunctionsTests.CollectionTests;

/// <summary>
/// Tests for =last($.path[*]) — the sibling of =partial($.path, n) that counts from the end.
///
/// The engine tests register the function locally: <c>last</c> is not part of
/// <c>ParseOptions.CreateDefault()</c> in this branch yet.
/// </summary>
[TestFixture]
public class LastTests
{
    private JToken data = null!;
    private IExecutionContext<JToken> context = null!;
    private ScriptEngine<JToken> engine = null!;

    [SetUp]
    public void Setup()
    {
        context = JsonExecutionContext.CreateDefault();
        data = JToken.Parse(@"{
            ""items"":  [""first"", ""second"", ""third""],
            ""events"": [ { ""on"": ""2024-01-01"" }, { ""on"": ""2024-06-01"" } ],
            ""single"": [""only""],
            ""empty"":  []
        }");

        var options = ParseOptions<JToken>.CreateDefault();
        options.FunctionsProvider.Register("last", () => new Last<JToken>());
        engine = new ScriptEngine<JToken>(options.CommandsProvider, options.FunctionsProvider);
    }

    private static Last<JToken> Fn(params string[] paths)
    {
        var arguments = new Arguments<JToken>();
        foreach (var p in paths) arguments.Add(new FixedValue<JToken>(new JValue(p)));
        var fn = new Last<JToken>();
        fn.SetArguments(arguments);
        return fn;
    }

    [Test]
    public void Last_ReturnsTheLastMatchedNode()
    {
        var result = Fn("$.items[*]").Execute(data, data, context);

        Assert.That(result.Success, Is.True);
        Assert.That(result.Data.First!.Value<string>(), Is.EqualTo("third"));
    }

    [Test]
    public void Last_ReturnsTheLastObject()
    {
        var result = Fn("$.events[*]").Execute(data, data, context);

        Assert.That(result.Success, Is.True);
        Assert.That(result.Data.First!["on"]!.Value<string>(), Is.EqualTo("2024-06-01"));
    }

    [Test]
    public void Last_SingleMatchReturnsThatMatch()
    {
        var result = Fn("$.single[*]").Execute(data, data, context);

        Assert.That(result.Success, Is.True);
        Assert.That(result.Data.First!.Value<string>(), Is.EqualTo("only"));
    }

    /// <summary>
    /// The trap: last works on the matched NODE SET, exactly like partial. A path naming the
    /// array matches one node — the array — so that array is what comes back.
    /// </summary>
    [Test]
    public void Last_OnAnArrayPathReturnsTheArrayItself()
    {
        var result = Fn("$.items").Execute(data, data, context);

        Assert.That(result.Success, Is.True);
        Assert.That(result.Data.First!.Type, Is.EqualTo(JTokenType.Array));
    }

    [Test]
    public void Last_EmptyArrayMatchesNothing_ReturnsFailed()
    {
        var result = Fn("$.empty[*]").Execute(data, data, context);

        Assert.That(result.Success, Is.False);
        Assert.That(context.GetLogEntries().Any(e => e.Level == LogLevel.Error), Is.True);
    }

    [Test]
    public void Last_PathNotFound_ReturnsFailed()
    {
        var result = Fn("$.missing[*]").Execute(data, data, context);

        Assert.That(result.Success, Is.False);
    }

    [Test]
    public void Last_WrongArity_ReturnsFailed()
    {
        Assert.That(new Last<JToken>().Execute(data, data, context).Success, Is.False);
        Assert.That(Fn("$.items[*]", "$.items[*]").Execute(data, data, context).Success, Is.False);
    }

    [Test]
    public void Last_ThroughEngine_WritesTheLastElement()
    {
        const string script =
            @"[{ ""command"": ""put"", ""path"": ""$.latest"", ""value"": ""=last($.items[*])"" }]";
        var result = engine.Execute(script, data, context);

        Assert.That(result.Success, Is.True);
        Assert.That(result.Data.SelectToken("$.latest")!.Value<string>(), Is.EqualTo("third"));
    }

    /// <summary>=last is what partial cannot do: reach the end without knowing the length.</summary>
    [Test]
    public void Last_MatchesPartialAtTheFinalIndex()
    {
        var partial = new Partial<JToken>();
        partial.SetArguments(new Arguments<JToken>
        {
            new FixedValue<JToken>(new JValue("$.items[*]")),
            new FixedValue<JToken>(new JValue(2))
        });

        var viaPartial = partial.Execute(data, data, context);
        var viaLast = Fn("$.items[*]").Execute(data, data, context);

        Assert.That(viaLast.Data.First!.ToString(Formatting.None),
            Is.EqualTo(viaPartial.Data.First!.ToString(Formatting.None)));
    }
}
