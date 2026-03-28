using Newtonsoft.Json.Linq;
using NUnit.Framework;
using TLio.Core.Models;
using TLio.Extensions.Math;
using TLio.Json;

namespace TLio.Functions.Tests.FunctionsTests.MathTests;

/// <summary>Ported from JLio.UnitTests.Math.CountTests.</summary>
[TestFixture]
public class CountTests
{
    private JToken data = null!;
    private TLio.Core.Contracts.IExecutionContext<JToken> context = null!;

    [SetUp]
    public void Setup()
    {
        context = JsonExecutionContext.CreateDefault();
        data = JToken.Parse(@"{ ""nums"": [1, 2, 3, 4], ""a"": 10, ""b"": 20 }");
    }

    [Test] public void Count_PathNotFound_ReturnsZero()
    {
        var fn = new Count<JToken>();
        fn.SetArguments(new Arguments<JToken> { new PathValue<JToken>("$.missing") });
        var result = fn.Execute(data, data, context);
        // Count skips not-found paths — result is 0, not a failure
        Assert.That(result.Success, Is.True);
        Assert.That(result.Data.First!.Value<long>(), Is.EqualTo(0));
    }

    [Test] public void Count_NoArgs_ReturnsZero()
    {
        var fn = new Count<JToken>();
        fn.SetArguments(new Arguments<JToken>());
        var result = fn.Execute(data, data, context);
        Assert.That(result.Success, Is.True);
        Assert.That(result.Data.First!.Value<long>(), Is.EqualTo(0));
    }
}
