using Newtonsoft.Json.Linq;
using NUnit.Framework;
using TLio.Core.Models;
using TLio.Extensions.Math;
using TLio.Json;

namespace TLio.UnitTests.FunctionsTests.MathTests;

/// <summary>Ported from JLio.UnitTests.Math.AvgTests.</summary>
[TestFixture]
public class AvgTests
{
    private JToken data = null!;
    private TLio.Core.Contracts.IExecutionContext<JToken> context = null!;

    [SetUp]
    public void Setup()
    {
        context = JsonExecutionContext.CreateDefault();
        data = JToken.Parse(@"{ ""nums"": [2, 4, 6], ""a"": 10, ""b"": 20 }");
    }

    [Test] public void Avg_Array()
    {
        var fn = new Avg<JToken>();
        fn.SetArguments(new Arguments<JToken> { new PathValue<JToken>("$.nums") });
        var result = fn.Execute(data, data, context);
        Assert.That(result.Success, Is.True);
        Assert.That(result.Data.First!.Value<double>(), Is.EqualTo(4));
    }

    [Test] public void Avg_TwoValues()
    {
        var fn = new Avg<JToken>();
        fn.SetArguments(new Arguments<JToken>
        {
            new PathValue<JToken>("$.a"),
            new PathValue<JToken>("$.b")
        });
        var result = fn.Execute(data, data, context);
        Assert.That(result.Success, Is.True);
        Assert.That(result.Data.First!.Value<double>(), Is.EqualTo(15));
    }

    [Test] public void Avg_PathNotFound_ReturnsFailed()
    {
        var fn = new Avg<JToken>();
        fn.SetArguments(new Arguments<JToken> { new PathValue<JToken>("$.missing") });
        var result = fn.Execute(data, data, context);
        Assert.That(result.Success, Is.False);
    }
}
