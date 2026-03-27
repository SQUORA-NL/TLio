using Newtonsoft.Json.Linq;
using NUnit.Framework;
using TLio.Core.Models;
using TLio.Extensions.Math;
using TLio.Json;

namespace TLio.UnitTests.FunctionsTests.MathTests;

/// <summary>Ported from JLio.UnitTests.Math.PowTests.</summary>
[TestFixture]
public class PowTests
{
    private JToken data = null!;
    private TLio.Core.Contracts.IExecutionContext<JToken> context = null!;

    [SetUp]
    public void Setup()
    {
        context = JsonExecutionContext.CreateDefault();
        data = JToken.Parse(@"{ ""base"": 2, ""exp"": 3, ""half"": 0.5, ""four"": 4 }");
    }

    [Test] public void Pow_IntegerResult()
    {
        var fn = new Pow<JToken>();
        fn.SetArguments(new Arguments<JToken>
        {
            new PathValue<JToken>("$.base"),
            new PathValue<JToken>("$.exp")
        });
        var result = fn.Execute(data, data, context);
        Assert.That(result.Success, Is.True);
        Assert.That(result.Data.First!.Value<double>(), Is.EqualTo(8));
    }

    [Test] public void Pow_FractionalExponent()
    {
        var fn = new Pow<JToken>();
        fn.SetArguments(new Arguments<JToken>
        {
            new PathValue<JToken>("$.four"),
            new PathValue<JToken>("$.half")
        });
        var result = fn.Execute(data, data, context);
        Assert.That(result.Success, Is.True);
        Assert.That(result.Data.First!.Value<double>(), Is.EqualTo(2));
    }

    [Test] public void Pow_TooFewArgs_ReturnsFailed()
    {
        var fn = new Pow<JToken>();
        fn.SetArguments(new Arguments<JToken> { new PathValue<JToken>("$.base") });
        var result = fn.Execute(data, data, context);
        Assert.That(result.Success, Is.False);
    }

    [Test] public void Pow_PathNotFound_ReturnsFailed()
    {
        var fn = new Pow<JToken>();
        fn.SetArguments(new Arguments<JToken>
        {
            new PathValue<JToken>("$.missing"),
            new PathValue<JToken>("$.exp")
        });
        var result = fn.Execute(data, data, context);
        Assert.That(result.Success, Is.False);
    }
}
