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
