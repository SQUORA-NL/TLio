using Newtonsoft.Json.Linq;
using NUnit.Framework;
using TLio.Core.Models;
using TLio.Extensions.Math;
using TLio.Json;

namespace TLio.UnitTests.FunctionsTests.MathTests;

/// <summary>Ported from JLio.UnitTests.Math.ModuloTests.</summary>
[TestFixture]
public class ModuloTests
{
    private JToken data = null!;
    private TLio.Core.Contracts.IExecutionContext<JToken> context = null!;

    [SetUp]
    public void Setup()
    {
        context = JsonExecutionContext.CreateDefault();
        data = JToken.Parse(@"{ ""dividend"": 10, ""divisor"": 3, ""zero"": 0 }");
    }

    [Test] public void Modulo_DivideByZero_ReturnsFailed()
    {
        var fn = new Modulo<JToken>();
        fn.SetArguments(new Arguments<JToken>
        {
            new PathValue<JToken>("$.dividend"),
            new PathValue<JToken>("$.zero")
        });
        var result = fn.Execute(data, data, context);
        Assert.That(result.Success, Is.False);
    }

    [Test] public void Modulo_TooFewArgs_ReturnsFailed()
    {
        var fn = new Modulo<JToken>();
        fn.SetArguments(new Arguments<JToken> { new PathValue<JToken>("$.dividend") });
        var result = fn.Execute(data, data, context);
        Assert.That(result.Success, Is.False);
    }

    [Test] public void Modulo_PathNotFound_ReturnsFailed()
    {
        var fn = new Modulo<JToken>();
        fn.SetArguments(new Arguments<JToken>
        {
            new PathValue<JToken>("$.missing"),
            new PathValue<JToken>("$.divisor")
        });
        var result = fn.Execute(data, data, context);
        Assert.That(result.Success, Is.False);
    }
}
