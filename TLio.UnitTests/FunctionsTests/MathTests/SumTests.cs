using Newtonsoft.Json.Linq;
using NUnit.Framework;
using TLio.Core.Models;
using TLio.Extensions.Math;
using TLio.Json;

namespace TLio.UnitTests.FunctionsTests.MathTests;

/// <summary>Ported from JLio.UnitTests.Math.SumTests.</summary>
[TestFixture]
public class SumTests
{
    private JToken data = null!;
    private TLio.Core.Contracts.IExecutionContext<JToken> context = null!;

    [SetUp]
    public void Setup()
    {
        context = JsonExecutionContext.CreateDefault();
        data = JToken.Parse(@"{ ""a"": 5, ""b"": 3, ""c"": null, ""nums"": [1, 2, 3, 4], ""str"": ""2.5"" }");
    }

    private static JToken Run(string path, JToken data, TLio.Core.Contracts.IExecutionContext<JToken> ctx)
    {
        var fn = new Sum<JToken>();
        fn.SetArguments(new Arguments<JToken> { new PathValue<JToken>(path) });
        var result = fn.Execute(data, data, ctx);
        Assert.That(result.Success, Is.True);
        return result.Data.First!;
    }

    [Test] public void Sum_SingleValue() =>
        Assert.That(Run("$.a", data, context).Value<double>(), Is.EqualTo(5));

    [Test] public void Sum_TwoValues()
    {
        var fn = new Sum<JToken>();
        fn.SetArguments(new Arguments<JToken>
        {
            new PathValue<JToken>("$.a"),
            new PathValue<JToken>("$.b")
        });
        var result = fn.Execute(data, data, context);
        Assert.That(result.Success, Is.True);
        Assert.That(result.Data.First!.Value<double>(), Is.EqualTo(8));
    }

    [Test] public void Sum_Array() =>
        Assert.That(Run("$.nums", data, context).Value<double>(), Is.EqualTo(10));

    [Test] public void Sum_NumericString() =>
        Assert.That(Run("$.str", data, context).Value<double>(), Is.EqualTo(2.5));

    [Test] public void Sum_NullFound_TreatedAsZero() =>
        Assert.That(Run("$.c", data, context).Value<double>(), Is.EqualTo(0));

    [Test] public void Sum_PathNotFound_ReturnsFailed()
    {
        var fn = new Sum<JToken>();
        fn.SetArguments(new Arguments<JToken> { new PathValue<JToken>("$.missing") });
        var result = fn.Execute(data, data, context);
        Assert.That(result.Success, Is.False);
    }

    [Test] public void Sum_NoArgs_ReturnsFailed()
    {
        var fn = new Sum<JToken>();
        fn.SetArguments(new Arguments<JToken>());
        var result = fn.Execute(data, data, context);
        Assert.That(result.Success, Is.False);
    }
}
