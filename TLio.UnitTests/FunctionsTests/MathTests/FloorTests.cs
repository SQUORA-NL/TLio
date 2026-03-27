using Newtonsoft.Json.Linq;
using NUnit.Framework;
using TLio.Core.Models;
using TLio.Extensions.Math;
using TLio.Json;

namespace TLio.UnitTests.FunctionsTests.MathTests;

/// <summary>Ported from JLio.UnitTests.Math.FloorTests.</summary>
[TestFixture]
public class FloorTests
{
    private JToken data = null!;
    private TLio.Core.Contracts.IExecutionContext<JToken> context = null!;

    [SetUp]
    public void Setup()
    {
        context = JsonExecutionContext.CreateDefault();
        data = JToken.Parse(@"{ ""whole"": 4, ""down"": 4.9, ""neg"": -4.1 }");
    }

    [Test] public void Floor_WholeNumber()
    {
        var fn = new Floor<JToken>();
        fn.SetArguments(new Arguments<JToken> { new PathValue<JToken>("$.whole") });
        var result = fn.Execute(data, data, context);
        Assert.That(result.Success, Is.True);
        Assert.That(result.Data.First!.Value<double>(), Is.EqualTo(4));
    }

    [Test] public void Floor_Fractional()
    {
        var fn = new Floor<JToken>();
        fn.SetArguments(new Arguments<JToken> { new PathValue<JToken>("$.down") });
        var result = fn.Execute(data, data, context);
        Assert.That(result.Success, Is.True);
        Assert.That(result.Data.First!.Value<double>(), Is.EqualTo(4));
    }

    [Test] public void Floor_Negative()
    {
        var fn = new Floor<JToken>();
        fn.SetArguments(new Arguments<JToken> { new PathValue<JToken>("$.neg") });
        var result = fn.Execute(data, data, context);
        Assert.That(result.Success, Is.True);
        Assert.That(result.Data.First!.Value<double>(), Is.EqualTo(-5));
    }

    [Test] public void Floor_PathNotFound_ReturnsFailed()
    {
        var fn = new Floor<JToken>();
        fn.SetArguments(new Arguments<JToken> { new PathValue<JToken>("$.missing") });
        var result = fn.Execute(data, data, context);
        Assert.That(result.Success, Is.False);
    }
}
