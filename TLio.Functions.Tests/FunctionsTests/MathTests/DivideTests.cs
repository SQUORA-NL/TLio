using Newtonsoft.Json.Linq;
using NUnit.Framework;
using TLio.Core.Models;
using TLio.Extensions.Math;
using TLio.Json;

namespace TLio.Functions.Tests.FunctionsTests.MathTests;

/// <summary>Mirror of SubtractTests — divide is the binary twin of subtract.</summary>
[TestFixture]
public class DivideTests
{
    private JToken data = null!;
    private TLio.Core.Contracts.IExecutionContext<JToken> context = null!;

    [SetUp]
    public void Setup()
    {
        context = JsonExecutionContext.CreateDefault();
        data = JToken.Parse(@"{
            ""a"": 10, ""b"": 4, ""two"": 2, ""zero"": 0,
            ""nums"": [1, 2, 3],
            ""nul"": null,
            ""text"": ""abc""
        }");
    }

    private static Divide<JToken> Fn(params string[] paths)
    {
        var args = new Arguments<JToken>();
        foreach (var p in paths) args.Add(new PathValue<JToken>(p));
        var fn = new Divide<JToken>();
        fn.SetArguments(args);
        return fn;
    }

    [Test] public void Divide_TwoScalars_ReturnsQuotient()
    {
        var result = Fn("$.a", "$.two").Execute(data, data, context);
        Assert.That(result.Success, Is.True);
        Assert.That(result.Data.First!.Value<double>(), Is.EqualTo(5));
    }

    [Test] public void Divide_WholeResult_IsInteger()
    {
        var result = Fn("$.a", "$.two").Execute(data, data, context);
        Assert.That(result.Data.First!.Type, Is.EqualTo(JTokenType.Integer));
    }

    [Test] public void Divide_FractionalResult_KeepsFraction()
    {
        var result = Fn("$.a", "$.b").Execute(data, data, context);
        Assert.That(result.Success, Is.True);
        Assert.That(result.Data.First!.Value<double>(), Is.EqualTo(2.5));
    }

    [Test] public void Divide_ArrayDividend_IsSummedFirst()
    {
        var result = Fn("$.nums", "$.two").Execute(data, data, context);
        Assert.That(result.Success, Is.True);
        Assert.That(result.Data.First!.Value<double>(), Is.EqualTo(3)); // (1+2+3) / 2
    }

    [Test] public void Divide_TooFewArgs_ReturnsFailed()
    {
        Assert.That(Fn("$.a").Execute(data, data, context).Success, Is.False);
    }

    [Test] public void Divide_PathNotFound_ReturnsFailed()
    {
        Assert.That(Fn("$.missing", "$.two").Execute(data, data, context).Success, Is.False);
    }

    [Test] public void Divide_NonNumeric_ReturnsFailed()
    {
        Assert.That(Fn("$.a", "$.text").Execute(data, data, context).Success, Is.False);
    }

    [Test] public void Divide_FoundNullDividend_IsZero()
    {
        var result = Fn("$.nul", "$.two").Execute(data, data, context);
        Assert.That(result.Success, Is.True);
        Assert.That(result.Data.First!.Value<double>(), Is.EqualTo(0));
    }

    // ── The edge a reader gets wrong ─────────────────────────────────────────

    [Test] public void Divide_ZeroDivisor_ReturnsFailed_NotInfinity()
    {
        Assert.That(Fn("$.a", "$.zero").Execute(data, data, context).Success, Is.False);
    }

    [Test] public void Divide_FoundNullDivisor_IsZeroAndFails()
    {
        // Found-null is 0 everywhere in this pack, so a null divisor is a zero divisor.
        Assert.That(Fn("$.a", "$.nul").Execute(data, data, context).Success, Is.False);
    }
}
