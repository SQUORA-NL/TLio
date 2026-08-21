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
/// Tests for =sortby($.array, '$.key', 'asc'|'desc').
///
/// The engine tests register the function locally: <c>sortby</c> is not part of
/// <c>ParseOptions.CreateDefault()</c> in this branch yet.
/// </summary>
[TestFixture]
public class SortByTests
{
    private JToken data = null!;
    private IExecutionContext<JToken> context = null!;
    private ScriptEngine<JToken> engine = null!;

    [SetUp]
    public void Setup()
    {
        context = JsonExecutionContext.CreateDefault();
        data = JToken.Parse(@"{
            ""coverages"": [
                { ""code"": ""casco"",          ""premium"": 240 },
                { ""code"": ""wa"",             ""premium"": 120 },
                { ""code"": ""rechtsbijstand"", ""premium"": 60 }
            ],
            ""withGaps"": [
                { ""code"": ""b"", ""premium"": 20 },
                { ""code"": ""x"" },
                { ""code"": ""a"", ""premium"": 10 }
            ],
            ""nested"": [
                { ""code"": ""b"", ""rating"": { ""factor"": 2 } },
                { ""code"": ""a"", ""rating"": { ""factor"": 1 } }
            ],
            ""empty"": []
        }");

        var options = ParseOptions<JToken>.CreateDefault();
        options.FunctionsProvider.Register("sortby", () => new SortBy<JToken>());
        engine = new ScriptEngine<JToken>(options.CommandsProvider, options.FunctionsProvider);
    }

    private static SortBy<JToken> Fn(string arrayPath, string keyPath, string? direction = null)
    {
        var arguments = new Arguments<JToken>
        {
            new PathValue<JToken>(arrayPath),
            new FixedValue<JToken>(new JValue(keyPath))
        };
        if (direction != null) arguments.Add(new FixedValue<JToken>(new JValue(direction)));
        var fn = new SortBy<JToken>();
        fn.SetArguments(arguments);
        return fn;
    }

    private static string Codes(JToken? array) =>
        string.Join(",", array!.Select(e => e["code"]!.Value<string>()));

    [Test]
    public void SortBy_AscendingIsTheDefault()
    {
        var result = Fn("$.coverages", "$.premium").Execute(data, data, context);

        Assert.That(result.Success, Is.True);
        Assert.That(Codes(result.Data.First), Is.EqualTo("rechtsbijstand,wa,casco"));
    }

    [Test]
    public void SortBy_Descending()
    {
        var result = Fn("$.coverages", "$.premium", "desc").Execute(data, data, context);

        Assert.That(result.Success, Is.True);
        Assert.That(Codes(result.Data.First), Is.EqualTo("casco,wa,rechtsbijstand"));
    }

    /// <summary>The key path is relative to the element, so the bare property name works too.</summary>
    [Test]
    public void SortBy_AcceptsABareKeyName()
    {
        var result = Fn("$.coverages", "premium").Execute(data, data, context);

        Assert.That(result.Success, Is.True);
        Assert.That(Codes(result.Data.First), Is.EqualTo("rechtsbijstand,wa,casco"));
    }

    [Test]
    public void SortBy_WalksANestedKeyPath()
    {
        var result = Fn("$.nested", "$.rating.factor").Execute(data, data, context);

        Assert.That(result.Success, Is.True);
        Assert.That(Codes(result.Data.First), Is.EqualTo("a,b"));
    }

    [Test]
    public void SortBy_SortsStringKeysOrdinally()
    {
        var result = Fn("$.coverages", "$.code").Execute(data, data, context);

        Assert.That(result.Success, Is.True);
        Assert.That(Codes(result.Data.First), Is.EqualTo("casco,rechtsbijstand,wa"));
    }

    /// <summary>The decision a reader will not expect: a missing key sorts last BOTH ways.</summary>
    [Test]
    public void SortBy_MissingKeySortsLastInBothDirections()
    {
        var ascending = Fn("$.withGaps", "$.premium").Execute(data, data, context);
        var descending = Fn("$.withGaps", "$.premium", "desc").Execute(data, data, context);

        Assert.That(Codes(ascending.Data.First), Is.EqualTo("a,b,x"));
        Assert.That(Codes(descending.Data.First), Is.EqualTo("b,a,x"));
    }

    [Test]
    public void SortBy_EmptyArrayReturnsEmptyArray()
    {
        var result = Fn("$.empty", "$.premium").Execute(data, data, context);

        Assert.That(result.Success, Is.True);
        Assert.That(result.Data.First!.ToString(Formatting.None), Is.EqualTo("[]"));
    }

    [Test]
    public void SortBy_UnknownDirection_LogsErrorAndFails()
    {
        var result = Fn("$.coverages", "$.premium", "up").Execute(data, data, context);

        Assert.That(result.Success, Is.False);
        Assert.That(context.GetLogEntries().Any(e =>
            e.Level == LogLevel.Error && e.Message.Contains("'desc'")), Is.True);
    }

    /// <summary>
    /// A key path with a subscript, wildcard or predicate is rejected up front: it would be
    /// resolved against a single element, and the fetchers do not agree on what that means.
    /// </summary>
    [Test]
    public void SortBy_NonPlainKeyPath_LogsErrorAndFails()
    {
        var result = Fn("$.coverages", "$.premium[0]").Execute(data, data, context);

        Assert.That(result.Success, Is.False);
        Assert.That(context.GetLogEntries().Any(e =>
            e.Level == LogLevel.Error && e.Message.Contains("plain property chain")), Is.True);
    }

    [Test]
    public void SortBy_PathNotFound_ReturnsFailed()
    {
        var result = Fn("$.missing", "$.premium").Execute(data, data, context);

        Assert.That(result.Success, Is.False);
        Assert.That(context.GetLogEntries().Any(e => e.Level == LogLevel.Error), Is.True);
    }

    [Test]
    public void SortBy_WrongArity_ReturnsFailed()
    {
        var oneArg = new SortBy<JToken>();
        oneArg.SetArguments(new Arguments<JToken> { new PathValue<JToken>("$.coverages") });
        Assert.That(oneArg.Execute(data, data, context).Success, Is.False);

        var fourArgs = new SortBy<JToken>();
        fourArgs.SetArguments(new Arguments<JToken>
        {
            new PathValue<JToken>("$.coverages"),
            new FixedValue<JToken>(new JValue("$.premium")),
            new FixedValue<JToken>(new JValue("asc")),
            new FixedValue<JToken>(new JValue("asc"))
        });
        Assert.That(fourArgs.Execute(data, data, context).Success, Is.False);
    }

    /// <summary>Equal keys keep their document order — the sort is stable.</summary>
    [Test]
    public void SortBy_IsStableForEqualKeys()
    {
        var ties = JToken.Parse(@"{ ""v"": [
            { ""code"": ""first"",  ""p"": 1 },
            { ""code"": ""second"", ""p"": 1 },
            { ""code"": ""third"",  ""p"": 0 }
        ] }");
        var fn = Fn("$.v", "$.p");

        var result = fn.Execute(ties, ties, context);

        Assert.That(Codes(result.Data.First), Is.EqualTo("third,first,second"));
    }

    [Test]
    public void SortBy_DoesNotMutateInput()
    {
        Fn("$.coverages", "$.premium").Execute(data, data, context);

        Assert.That(Codes(data.SelectToken("$.coverages")), Is.EqualTo("casco,wa,rechtsbijstand"));
    }

    [Test]
    public void SortBy_ThroughEngine_WritesOrderedArray()
    {
        const string script =
            @"[{ ""command"": ""put"", ""path"": ""$.ranked"", ""value"": ""=sortby($.coverages, '$.premium', 'desc')"" }]";
        var result = engine.Execute(script, data, context);

        Assert.That(result.Success, Is.True);
        Assert.That(Codes(result.Data.SelectToken("$.ranked")), Is.EqualTo("casco,wa,rechtsbijstand"));
    }
}
