using Newtonsoft.Json.Linq;
using NUnit.Framework;
using TLio.Core.Models;
using TLio.Extensions.Math;
using TLio.Json;

namespace TLio.Functions.Tests.FunctionsTests.MathTests;

/// <summary>Mirror of SumTests — multiply is the variadic twin of sum.</summary>
[TestFixture]
public class MultiplyTests
{
    private JToken data = null!;
    private TLio.Core.Contracts.IExecutionContext<JToken> context = null!;

    [SetUp]
    public void Setup()
    {
        context = JsonExecutionContext.CreateDefault();
        data = JToken.Parse(@"{
            ""a"": 6, ""b"": 7, ""half"": 0.5,
            ""factors"": [2, 3, 4],
            ""withNull"": [2, null, 4],
            ""nul"": null,
            ""text"": ""abc"",
            ""numeric"": ""2.5""
        }");
    }

    private static Multiply<JToken> Fn(params string[] paths)
    {
        var args = new Arguments<JToken>();
        foreach (var p in paths) args.Add(new PathValue<JToken>(p));
        var fn = new Multiply<JToken>();
        fn.SetArguments(args);
        return fn;
    }

    [Test] public void Multiply_TwoScalars_ReturnsProduct()
    {
        var result = Fn("$.a", "$.b").Execute(data, data, context);
        Assert.That(result.Success, Is.True);
        Assert.That(result.Data.First!.Value<double>(), Is.EqualTo(42));
    }

    [Test] public void Multiply_SingleArgument_ReturnsThatValue()
    {
        var result = Fn("$.a").Execute(data, data, context);
        Assert.That(result.Success, Is.True);
        Assert.That(result.Data.First!.Value<double>(), Is.EqualTo(6));
    }

    [Test] public void Multiply_Array_MultipliesEveryElement()
    {
        var result = Fn("$.factors").Execute(data, data, context);
        Assert.That(result.Success, Is.True);
        Assert.That(result.Data.First!.Value<double>(), Is.EqualTo(24)); // 2 * 3 * 4
    }

    [Test] public void Multiply_WholeResult_IsInteger()
    {
        var result = Fn("$.a", "$.b").Execute(data, data, context);
        Assert.That(result.Data.First!.Type, Is.EqualTo(JTokenType.Integer));
    }

    [Test] public void Multiply_FractionalResult_IsFloat()
    {
        var result = Fn("$.a", "$.half").Execute(data, data, context);
        Assert.That(result.Success, Is.True);
        Assert.That(result.Data.First!.Value<double>(), Is.EqualTo(3));
    }

    [Test] public void Multiply_NumericString_IsParsed()
    {
        var result = Fn("$.a", "$.numeric").Execute(data, data, context);
        Assert.That(result.Success, Is.True);
        Assert.That(result.Data.First!.Value<double>(), Is.EqualTo(15));
    }

    [Test] public void Multiply_NoArguments_ReturnsFailed()
    {
        var fn = new Multiply<JToken>();
        fn.SetArguments(new Arguments<JToken>());
        Assert.That(fn.Execute(data, data, context).Success, Is.False);
    }

    [Test] public void Multiply_PathNotFound_ReturnsFailed()
    {
        Assert.That(Fn("$.missing", "$.a").Execute(data, data, context).Success, Is.False);
    }

    [Test] public void Multiply_NonNumeric_ReturnsFailed()
    {
        Assert.That(Fn("$.text").Execute(data, data, context).Success, Is.False);
    }

    // ── The edge a reader gets wrong ─────────────────────────────────────────

    [Test] public void Multiply_FoundNull_MultipliesAsZero()
    {
        var result = Fn("$.a", "$.nul").Execute(data, data, context);
        Assert.That(result.Success, Is.True);
        Assert.That(result.Data.First!.Value<double>(), Is.EqualTo(0));
    }

    [Test] public void Multiply_ArrayWithNullElement_CollapsesToZero()
    {
        var result = Fn("$.withNull").Execute(data, data, context);
        Assert.That(result.Success, Is.True);
        Assert.That(result.Data.First!.Value<double>(), Is.EqualTo(0)); // not 8 — null is 0, not 1
    }
}
