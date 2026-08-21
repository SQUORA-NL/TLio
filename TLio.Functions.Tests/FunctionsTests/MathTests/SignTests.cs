using Newtonsoft.Json.Linq;
using NUnit.Framework;
using TLio.Core.Models;
using TLio.Extensions.Math;
using TLio.Json;

namespace TLio.Functions.Tests.FunctionsTests.MathTests;

[TestFixture]
public class SignTests
{
    private JToken data = null!;
    private TLio.Core.Contracts.IExecutionContext<JToken> context = null!;

    [SetUp]
    public void Setup()
    {
        context = JsonExecutionContext.CreateDefault();
        data = JToken.Parse(@"{
            ""pos"": 42, ""neg"": -0.001, ""zero"": 0,
            ""nul"": null,
            ""numeric"": ""-3"",
            ""text"": ""abc""
        }");
    }

    private static Sign<JToken> Fn(params string[] paths)
    {
        var args = new Arguments<JToken>();
        foreach (var p in paths) args.Add(new PathValue<JToken>(p));
        var fn = new Sign<JToken>();
        fn.SetArguments(args);
        return fn;
    }

    [Test] public void Sign_Positive_ReturnsOne()
    {
        var result = Fn("$.pos").Execute(data, data, context);
        Assert.That(result.Success, Is.True);
        Assert.That(result.Data.First!.Value<long>(), Is.EqualTo(1));
    }

    [Test] public void Sign_Negative_ReturnsMinusOne()
    {
        var result = Fn("$.neg").Execute(data, data, context);
        Assert.That(result.Success, Is.True);
        Assert.That(result.Data.First!.Value<long>(), Is.EqualTo(-1));
    }

    [Test] public void Sign_Zero_ReturnsZero()
    {
        var result = Fn("$.zero").Execute(data, data, context);
        Assert.That(result.Success, Is.True);
        Assert.That(result.Data.First!.Value<long>(), Is.EqualTo(0));
    }

    [Test] public void Sign_Result_IsInteger()
    {
        var result = Fn("$.neg").Execute(data, data, context);
        Assert.That(result.Data.First!.Type, Is.EqualTo(JTokenType.Integer));
    }

    [Test] public void Sign_NumericString_IsParsed()
    {
        var result = Fn("$.numeric").Execute(data, data, context);
        Assert.That(result.Success, Is.True);
        Assert.That(result.Data.First!.Value<long>(), Is.EqualTo(-1));
    }

    [Test] public void Sign_NoArguments_ReturnsFailed()
    {
        var fn = new Sign<JToken>();
        fn.SetArguments(new Arguments<JToken>());
        Assert.That(fn.Execute(data, data, context).Success, Is.False);
    }

    [Test] public void Sign_PathNotFound_ReturnsFailed()
    {
        Assert.That(Fn("$.missing").Execute(data, data, context).Success, Is.False);
    }

    [Test] public void Sign_NonNumeric_ReturnsFailed()
    {
        Assert.That(Fn("$.text").Execute(data, data, context).Success, Is.False);
    }

    // ── The edge a reader gets wrong ─────────────────────────────────────────

    [Test] public void Sign_FoundNull_ReturnsZero()
    {
        // Null is 0 in this pack, and sign(0) is 0 — not a failure and not -1.
        var result = Fn("$.nul").Execute(data, data, context);
        Assert.That(result.Success, Is.True);
        Assert.That(result.Data.First!.Value<long>(), Is.EqualTo(0));
    }

    [Test] public void Sign_TinyNegative_IsMinusOneNotZero()
    {
        var result = Fn("$.neg").Execute(data, data, context);
        Assert.That(result.Data.First!.Value<long>(), Is.EqualTo(-1));
    }
}
