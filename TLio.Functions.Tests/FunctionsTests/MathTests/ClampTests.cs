using Newtonsoft.Json.Linq;
using NUnit.Framework;
using TLio.Core.Models;
using TLio.Extensions.Math;
using TLio.Json;

namespace TLio.Functions.Tests.FunctionsTests.MathTests;

[TestFixture]
public class ClampTests
{
    private JToken data = null!;
    private TLio.Core.Contracts.IExecutionContext<JToken> context = null!;

    [SetUp]
    public void Setup()
    {
        context = JsonExecutionContext.CreateDefault();
        data = JToken.Parse(@"{
            ""low"": 1, ""high"": 5,
            ""under"": 0.4, ""inside"": 3, ""over"": 9,
            ""negative"": -7,
            ""nul"": null,
            ""text"": ""abc""
        }");
    }

    private static Clamp<JToken> Fn(params string[] paths)
    {
        var args = new Arguments<JToken>();
        foreach (var p in paths) args.Add(new PathValue<JToken>(p));
        var fn = new Clamp<JToken>();
        fn.SetArguments(args);
        return fn;
    }

    [Test] public void Clamp_InsideRange_ReturnsValueUnchanged()
    {
        var result = Fn("$.inside", "$.low", "$.high").Execute(data, data, context);
        Assert.That(result.Success, Is.True);
        Assert.That(result.Data.First!.Value<double>(), Is.EqualTo(3));
    }

    [Test] public void Clamp_BelowLow_ReturnsLow()
    {
        var result = Fn("$.under", "$.low", "$.high").Execute(data, data, context);
        Assert.That(result.Success, Is.True);
        Assert.That(result.Data.First!.Value<double>(), Is.EqualTo(1));
    }

    [Test] public void Clamp_AboveHigh_ReturnsHigh()
    {
        var result = Fn("$.over", "$.low", "$.high").Execute(data, data, context);
        Assert.That(result.Success, Is.True);
        Assert.That(result.Data.First!.Value<double>(), Is.EqualTo(5));
    }

    [Test] public void Clamp_ExactlyOnBound_IsInclusive()
    {
        var result = Fn("$.high", "$.low", "$.high").Execute(data, data, context);
        Assert.That(result.Success, Is.True);
        Assert.That(result.Data.First!.Value<double>(), Is.EqualTo(5));
    }

    [Test] public void Clamp_NegativeValue_ClampsToLow()
    {
        var result = Fn("$.negative", "$.low", "$.high").Execute(data, data, context);
        Assert.That(result.Data.First!.Value<double>(), Is.EqualTo(1));
    }

    [Test] public void Clamp_WholeResult_IsInteger()
    {
        var result = Fn("$.over", "$.low", "$.high").Execute(data, data, context);
        Assert.That(result.Data.First!.Type, Is.EqualTo(JTokenType.Integer));
    }

    [Test] public void Clamp_TooFewArgs_ReturnsFailed()
    {
        Assert.That(Fn("$.inside", "$.low").Execute(data, data, context).Success, Is.False);
    }

    [Test] public void Clamp_PathNotFound_ReturnsFailed()
    {
        Assert.That(Fn("$.missing", "$.low", "$.high").Execute(data, data, context).Success, Is.False);
    }

    [Test] public void Clamp_NonNumeric_ReturnsFailed()
    {
        Assert.That(Fn("$.text", "$.low", "$.high").Execute(data, data, context).Success, Is.False);
    }

    [Test] public void Clamp_FoundNullValue_ClampsZeroIntoRange()
    {
        var result = Fn("$.nul", "$.low", "$.high").Execute(data, data, context);
        Assert.That(result.Success, Is.True);
        Assert.That(result.Data.First!.Value<double>(), Is.EqualTo(1)); // 0 is below low
    }

    // ── The edge a reader gets wrong ─────────────────────────────────────────

    [Test] public void Clamp_LowAboveHigh_ReturnsFailed()
    {
        Assert.That(Fn("$.inside", "$.high", "$.low").Execute(data, data, context).Success, Is.False);
    }
}
