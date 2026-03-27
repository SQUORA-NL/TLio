using Newtonsoft.Json.Linq;
using NUnit.Framework;
using TLio.Core.Models;
using TLio.Extensions.Math;
using TLio.Json;

namespace TLio.UnitTests.FunctionsTests.MathTests;

/// <summary>Ported from JLio.UnitTests.Math.AbsTests.</summary>
[TestFixture]
public class AbsTests
{
    private JToken data = null!;
    private TLio.Core.Contracts.IExecutionContext<JToken> context = null!;

    [SetUp]
    public void Setup()
    {
        context = JsonExecutionContext.CreateDefault();
        data = JToken.Parse(@"{ ""pos"": 3, ""neg"": -5, ""frac"": -2.7 }");
    }

    [Test] public void Abs_Positive()
    {
        var fn = new Abs<JToken>();
        fn.SetArguments(new Arguments<JToken> { new PathValue<JToken>("$.pos") });
        var result = fn.Execute(data, data, context);
        Assert.That(result.Success, Is.True);
        Assert.That(result.Data.First!.Value<double>(), Is.EqualTo(3));
    }

    [Test] public void Abs_Negative()
    {
        var fn = new Abs<JToken>();
        fn.SetArguments(new Arguments<JToken> { new PathValue<JToken>("$.neg") });
        var result = fn.Execute(data, data, context);
        Assert.That(result.Success, Is.True);
        Assert.That(result.Data.First!.Value<double>(), Is.EqualTo(5));
    }

    [Test] public void Abs_NegativeFractional()
    {
        var fn = new Abs<JToken>();
        fn.SetArguments(new Arguments<JToken> { new PathValue<JToken>("$.frac") });
        var result = fn.Execute(data, data, context);
        Assert.That(result.Success, Is.True);
        Assert.That(result.Data.First!.Value<double>(), Is.EqualTo(2.7));
    }

    [Test] public void Abs_PathNotFound_ReturnsFailed()
    {
        var fn = new Abs<JToken>();
        fn.SetArguments(new Arguments<JToken> { new PathValue<JToken>("$.missing") });
        var result = fn.Execute(data, data, context);
        Assert.That(result.Success, Is.False);
    }

    [Test] public void Abs_NoArgs_ReturnsFailed()
    {
        var fn = new Abs<JToken>();
        fn.SetArguments(new Arguments<JToken>());
        var result = fn.Execute(data, data, context);
        Assert.That(result.Success, Is.False);
    }
}
