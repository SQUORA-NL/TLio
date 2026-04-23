using Newtonsoft.Json.Linq;
using NUnit.Framework;
using TLio.Core.Models;
using TLio.Extensions.Math;
using TLio.Json;

namespace TLio.Functions.Tests.FunctionsTests.MathTests;

/// <summary>Edge-case tests: empty arrays, single elements, zero, and negative values.</summary>
[TestFixture]
public class MathEdgeCaseTests
{
    private TLio.Core.Contracts.IExecutionContext<JToken> context = null!;

    [SetUp]
    public void Setup() => context = JsonExecutionContext.CreateDefault();

    // ── Empty array ───────────────────────────────────────────────────────────

    [Test]
    public void Sum_EmptyArray_ReturnsZero()
    {
        var data = JToken.Parse(@"{ ""nums"": [] }");
        var fn = new Sum<JToken>();
        fn.SetArguments(new Arguments<JToken> { new PathValue<JToken>("$.nums") });
        var result = fn.Execute(data, data, context);
        Assert.That(result.Success, Is.True);
        Assert.That(result.Data.First!.Value<double>(), Is.EqualTo(0));
    }

    [Test]
    public void Avg_EmptyArray_ReturnsZero()
    {
        var data = JToken.Parse(@"{ ""nums"": [] }");
        var fn = new Avg<JToken>();
        fn.SetArguments(new Arguments<JToken> { new PathValue<JToken>("$.nums") });
        var result = fn.Execute(data, data, context);
        Assert.That(result.Success, Is.True);
        Assert.That(result.Data.First!.Value<double>(), Is.EqualTo(0));
    }

    [Test]
    public void Min_EmptyArray_ReturnsFailed()
    {
        var data = JToken.Parse(@"{ ""nums"": [] }");
        var fn = new Min<JToken>();
        fn.SetArguments(new Arguments<JToken> { new PathValue<JToken>("$.nums") });
        var result = fn.Execute(data, data, context);
        Assert.That(result.Success, Is.False);
    }

    [Test]
    public void Max_EmptyArray_ReturnsFailed()
    {
        var data = JToken.Parse(@"{ ""nums"": [] }");
        var fn = new Max<JToken>();
        fn.SetArguments(new Arguments<JToken> { new PathValue<JToken>("$.nums") });
        var result = fn.Execute(data, data, context);
        Assert.That(result.Success, Is.False);
    }

    [Test]
    public void Count_EmptyArrayElements_ReturnsZero()
    {
        var data = JToken.Parse(@"{ ""nums"": [] }");
        var fn = new Count<JToken>();
        fn.SetArguments(new Arguments<JToken> { new PathValue<JToken>("$.nums[*]") });
        var result = fn.Execute(data, data, context);
        Assert.That(result.Success, Is.True);
        Assert.That(result.Data.First!.Value<long>(), Is.EqualTo(0));
    }

    // ── Single element ────────────────────────────────────────────────────────

    [Test]
    public void Sum_SingleElement_ReturnsThatValue()
    {
        var data = JToken.Parse(@"{ ""nums"": [5] }");
        var fn = new Sum<JToken>();
        fn.SetArguments(new Arguments<JToken> { new PathValue<JToken>("$.nums") });
        var result = fn.Execute(data, data, context);
        Assert.That(result.Success, Is.True);
        Assert.That(result.Data.First!.Value<double>(), Is.EqualTo(5));
    }

    [Test]
    public void Min_SingleElement_ReturnsThatValue()
    {
        var data = JToken.Parse(@"{ ""nums"": [7] }");
        var fn = new Min<JToken>();
        fn.SetArguments(new Arguments<JToken> { new PathValue<JToken>("$.nums") });
        var result = fn.Execute(data, data, context);
        Assert.That(result.Success, Is.True);
        Assert.That(result.Data.First!.Value<double>(), Is.EqualTo(7));
    }

    [Test]
    public void Max_SingleElement_ReturnsThatValue()
    {
        var data = JToken.Parse(@"{ ""nums"": [7] }");
        var fn = new Max<JToken>();
        fn.SetArguments(new Arguments<JToken> { new PathValue<JToken>("$.nums") });
        var result = fn.Execute(data, data, context);
        Assert.That(result.Success, Is.True);
        Assert.That(result.Data.First!.Value<double>(), Is.EqualTo(7));
    }

    [Test]
    public void Avg_SingleElement_ReturnsThatValue()
    {
        var data = JToken.Parse(@"{ ""nums"": [8] }");
        var fn = new Avg<JToken>();
        fn.SetArguments(new Arguments<JToken> { new PathValue<JToken>("$.nums") });
        var result = fn.Execute(data, data, context);
        Assert.That(result.Success, Is.True);
        Assert.That(result.Data.First!.Value<double>(), Is.EqualTo(8));
    }

    // ── Negative values ───────────────────────────────────────────────────────

    [Test]
    public void Sum_AllNegatives_ReturnsCorrectSum()
    {
        var data = JToken.Parse(@"{ ""nums"": [-1, -2, -3] }");
        var fn = new Sum<JToken>();
        fn.SetArguments(new Arguments<JToken> { new PathValue<JToken>("$.nums") });
        var result = fn.Execute(data, data, context);
        Assert.That(result.Success, Is.True);
        Assert.That(result.Data.First!.Value<double>(), Is.EqualTo(-6));
    }

    [Test]
    public void Min_NegativeValues_ReturnsSmallestNegative()
    {
        var data = JToken.Parse(@"{ ""nums"": [-1, -5, -2] }");
        var fn = new Min<JToken>();
        fn.SetArguments(new Arguments<JToken> { new PathValue<JToken>("$.nums") });
        var result = fn.Execute(data, data, context);
        Assert.That(result.Success, Is.True);
        Assert.That(result.Data.First!.Value<double>(), Is.EqualTo(-5));
    }

    [Test]
    public void Max_NegativeValues_ReturnsLargestNegative()
    {
        var data = JToken.Parse(@"{ ""nums"": [-1, -5, -2] }");
        var fn = new Max<JToken>();
        fn.SetArguments(new Arguments<JToken> { new PathValue<JToken>("$.nums") });
        var result = fn.Execute(data, data, context);
        Assert.That(result.Success, Is.True);
        Assert.That(result.Data.First!.Value<double>(), Is.EqualTo(-1));
    }

    // ── Zero values ───────────────────────────────────────────────────────────

    [Test]
    public void Sum_AllZeros_ReturnsZero()
    {
        var data = JToken.Parse(@"{ ""nums"": [0, 0, 0] }");
        var fn = new Sum<JToken>();
        fn.SetArguments(new Arguments<JToken> { new PathValue<JToken>("$.nums") });
        var result = fn.Execute(data, data, context);
        Assert.That(result.Success, Is.True);
        Assert.That(result.Data.First!.Value<double>(), Is.EqualTo(0));
    }

    [Test]
    public void Min_IdenticalValues_ReturnsThatValue()
    {
        var data = JToken.Parse(@"{ ""nums"": [3, 3, 3] }");
        var fn = new Min<JToken>();
        fn.SetArguments(new Arguments<JToken> { new PathValue<JToken>("$.nums") });
        var result = fn.Execute(data, data, context);
        Assert.That(result.Success, Is.True);
        Assert.That(result.Data.First!.Value<double>(), Is.EqualTo(3));
    }
}
