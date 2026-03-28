using Newtonsoft.Json.Linq;
using NUnit.Framework;
using TLio.Core.Models;
using TLio.Extensions.Math;
using TLio.Json;

namespace TLio.Functions.Tests.FunctionsTests.MathTests;

/// <summary>Ported from JLio.UnitTests.Math.SubtractTests.</summary>
[TestFixture]
public class SubtractTests
{
    private JToken data = null!;
    private TLio.Core.Contracts.IExecutionContext<JToken> context = null!;

    [SetUp]
    public void Setup()
    {
        context = JsonExecutionContext.CreateDefault();
        data = JToken.Parse(@"{ ""a"": 10, ""b"": 3, ""nums"": [1, 2, 3] }");
    }

    [Test] public void Subtract_TooFewArgs_ReturnsFailed()
    {
        var fn = new Subtract<JToken>();
        fn.SetArguments(new Arguments<JToken> { new PathValue<JToken>("$.a") });
        var result = fn.Execute(data, data, context);
        Assert.That(result.Success, Is.False);
    }

    [Test] public void Subtract_PathNotFound_ReturnsFailed()
    {
        var fn = new Subtract<JToken>();
        fn.SetArguments(new Arguments<JToken>
        {
            new PathValue<JToken>("$.missing"),
            new PathValue<JToken>("$.b")
        });
        var result = fn.Execute(data, data, context);
        Assert.That(result.Success, Is.False);
    }
}
