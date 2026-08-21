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
/// Tests for =sort($.array, 'asc'|'desc').
///
/// The engine tests register the function locally: <c>sort</c> is not part of
/// <c>ParseOptions.CreateDefault()</c> in this branch yet.
/// </summary>
[TestFixture]
public class SortTests
{
    private JToken data = null!;
    private IExecutionContext<JToken> context = null!;
    private ScriptEngine<JToken> engine = null!;

    [SetUp]
    public void Setup()
    {
        context = JsonExecutionContext.CreateDefault();
        data = JToken.Parse(@"{
            ""numbers"": [10, 2, 33, 4],
            ""words"":   [""pear"", ""Apple"", ""banana""],
            ""mixed"":   [10, ""two"", 9],
            ""empty"":   [],
            ""dir"":     ""desc""
        }");

        var options = ParseOptions<JToken>.CreateDefault();
        options.FunctionsProvider.Register("sort", () => new Sort<JToken>());
        engine = new ScriptEngine<JToken>(options.CommandsProvider, options.FunctionsProvider);
    }

    private static Sort<JToken> Fn(string arrayPath, string? direction = null)
    {
        var arguments = new Arguments<JToken> { new PathValue<JToken>(arrayPath) };
        if (direction != null) arguments.Add(new FixedValue<JToken>(new JValue(direction)));
        var fn = new Sort<JToken>();
        fn.SetArguments(arguments);
        return fn;
    }

    private static string Json(JToken? token) => token!.ToString(Formatting.None);

    [Test]
    public void Sort_DefaultsToAscendingNumeric()
    {
        var result = Fn("$.numbers").Execute(data, data, context);

        Assert.That(result.Success, Is.True);
        Assert.That(Json(result.Data.First), Is.EqualTo("[2,4,10,33]"));
    }

    [Test]
    public void Sort_Descending()
    {
        var result = Fn("$.numbers", "desc").Execute(data, data, context);

        Assert.That(result.Success, Is.True);
        Assert.That(Json(result.Data.First), Is.EqualTo("[33,10,4,2]"));
    }

    [Test]
    public void Sort_DirectionIsCaseInsensitive()
    {
        var result = Fn("$.numbers", "DESC").Execute(data, data, context);

        Assert.That(result.Success, Is.True);
        Assert.That(Json(result.Data.First), Is.EqualTo("[33,10,4,2]"));
    }

    /// <summary>Ordinal, not culture-aware: uppercase letters sort before lowercase.</summary>
    [Test]
    public void Sort_StringsUseOrdinalComparison()
    {
        var result = Fn("$.words").Execute(data, data, context);

        Assert.That(result.Success, Is.True);
        Assert.That(Json(result.Data.First), Is.EqualTo(@"[""Apple"",""banana"",""pear""]"));
    }

    /// <summary>
    /// The edge case a reader will get wrong: one non-numeric element moves the WHOLE array
    /// to ordinal text ordering, so 10 sorts before 9.
    /// </summary>
    [Test]
    public void Sort_MixedArrayFallsBackToOrdinalStringOrder()
    {
        var result = Fn("$.mixed").Execute(data, data, context);

        Assert.That(result.Success, Is.True);
        Assert.That(Json(result.Data.First), Is.EqualTo(@"[10,9,""two""]"));
    }

    [Test]
    public void Sort_EmptyArrayReturnsEmptyArray()
    {
        var result = Fn("$.empty").Execute(data, data, context);

        Assert.That(result.Success, Is.True);
        Assert.That(Json(result.Data.First), Is.EqualTo("[]"));
    }

    [Test]
    public void Sort_UnknownDirection_LogsErrorAndFails()
    {
        var result = Fn("$.numbers", "ascending").Execute(data, data, context);

        Assert.That(result.Success, Is.False);
        Assert.That(context.GetLogEntries().Any(e =>
            e.Level == LogLevel.Error && e.Message.Contains("'asc'") && e.Message.Contains("'desc'")), Is.True);
    }

    [Test]
    public void Sort_DirectionCanComeFromAPath()
    {
        var arguments = new Arguments<JToken>
        {
            new PathValue<JToken>("$.numbers"),
            new FixedValue<JToken>(new JValue("$.dir"))
        };
        var fn = new Sort<JToken>();
        fn.SetArguments(arguments);

        var result = fn.Execute(data, data, context);

        Assert.That(result.Success, Is.True);
        Assert.That(Json(result.Data.First), Is.EqualTo("[33,10,4,2]"));
    }

    [Test]
    public void Sort_PathNotFound_ReturnsFailed()
    {
        var result = Fn("$.missing").Execute(data, data, context);

        Assert.That(result.Success, Is.False);
        Assert.That(context.GetLogEntries().Any(e => e.Level == LogLevel.Error), Is.True);
    }

    [Test]
    public void Sort_WrongArity_ReturnsFailed()
    {
        Assert.That(new Sort<JToken>().Execute(data, data, context).Success, Is.False);

        var tooMany = new Sort<JToken>();
        tooMany.SetArguments(new Arguments<JToken>
        {
            new PathValue<JToken>("$.numbers"),
            new FixedValue<JToken>(new JValue("asc")),
            new FixedValue<JToken>(new JValue("asc"))
        });
        Assert.That(tooMany.Execute(data, data, context).Success, Is.False);
    }

    /// <summary>
    /// Equal keys keep their document order — OrderBy is stable, List.Sort is not.
    /// 1.0 and 1 compare equal numerically but are distinguishable in the output, so the
    /// float staying in front proves the order was not reshuffled.
    /// </summary>
    [Test]
    public void Sort_IsStableForEqualKeys()
    {
        var ties = JToken.Parse(@"{ ""v"": [2, 1.0, 1] }");
        var fn = new Sort<JToken>();
        fn.SetArguments(new Arguments<JToken> { new PathValue<JToken>("$.v") });

        Assert.That(Json(fn.Execute(ties, ties, context).Data.First), Is.EqualTo("[1.0,1,2]"));
    }

    [Test]
    public void Sort_DoesNotMutateInput()
    {
        Fn("$.numbers").Execute(data, data, context);

        Assert.That(Json(data.SelectToken("$.numbers")), Is.EqualTo("[10,2,33,4]"));
    }

    [Test]
    public void Sort_ThroughEngine_WritesSortedArray()
    {
        const string script =
            @"[{ ""command"": ""put"", ""path"": ""$.sorted"", ""value"": ""=sort($.numbers, 'desc')"" }]";
        var result = engine.Execute(script, data, context);

        Assert.That(result.Success, Is.True);
        Assert.That(Json(result.Data.SelectToken("$.sorted")), Is.EqualTo("[33,10,4,2]"));
    }
}
