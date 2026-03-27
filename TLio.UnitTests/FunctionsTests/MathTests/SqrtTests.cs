using Newtonsoft.Json.Linq;
using NUnit.Framework;
using TLio.Core.Models;
using TLio.Extensions.Math;
using TLio.Json;

namespace TLio.UnitTests.FunctionsTests.MathTests;

/// <summary>Ported from JLio.UnitTests.Math.SqrtTests.</summary>
[TestFixture]
public class SqrtTests
{
    private JToken data = null!;
    private TLio.Core.Contracts.IExecutionContext<JToken> context = null!;

    [SetUp]
    public void Setup()
    {
        context = JsonExecutionContext.CreateDefault();
        data = JToken.Parse(@"{ ""perfect"": 9, ""two"": 2, ""neg"": -1 }");
    }

    [Test] public void Sqrt_PerfectSquare()
    {
        var fn = new Sqrt<JToken>();
        fn.SetArguments(new Arguments<JToken> { new PathValue<JToken>("$.perfect") });
        var result = fn.Execute(data, data, context);
        Assert.That(result.Success, Is.True);
        Assert.That(result.Data.First!.Value<double>(), Is.EqualTo(3));
    }

    [Test] public void Sqrt_NonPerfectSquare()
    {
        var fn = new Sqrt<JToken>();
        fn.SetArguments(new Arguments<JToken> { new PathValue<JToken>("$.two") });
        var result = fn.Execute(data, data, context);
        Assert.That(result.Success, Is.True);
        Assert.That(result.Data.First!.Value<double>(), Is.EqualTo(System.Math.Sqrt(2)).Within(1e-10));
    }

    [Test] public void Sqrt_Negative_ReturnsFailed()
    {
        var fn = new Sqrt<JToken>();
        fn.SetArguments(new Arguments<JToken> { new PathValue<JToken>("$.neg") });
        var result = fn.Execute(data, data, context);
        Assert.That(result.Success, Is.False);
    }

    [Test] public void Sqrt_PathNotFound_ReturnsFailed()
    {
        var fn = new Sqrt<JToken>();
        fn.SetArguments(new Arguments<JToken> { new PathValue<JToken>("$.missing") });
        var result = fn.Execute(data, data, context);
        Assert.That(result.Success, Is.False);
    }
}
