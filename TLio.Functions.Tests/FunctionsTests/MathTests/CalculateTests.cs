using Newtonsoft.Json.Linq;
using NUnit.Framework;
using TLio.Core.Models;
using TLio.Extensions.Math;
using TLio.Json;

namespace TLio.Functions.Tests.FunctionsTests.MathTests;

/// <summary>Ported from JLio.UnitTests.Math.CalculateTests.</summary>
[TestFixture]
public class CalculateTests
{
    private JToken data = null!;
    private TLio.Core.Contracts.IExecutionContext<JToken> context = null!;

    [SetUp]
    public void Setup()
    {
        context = JsonExecutionContext.CreateDefault();
        data = JToken.Parse(@"{ ""add"": ""2 + 3"", ""complex"": ""10 * (4 - 1)"", ""modulo"": ""7 % 3"" }");
    }

    [Test] public void Calculate_PathNotFound_ReturnsFailed()
    {
        var fn = new Calculate<JToken>();
        fn.SetArguments(new Arguments<JToken> { new PathValue<JToken>("$.missing") });
        var result = fn.Execute(data, data, context);
        Assert.That(result.Success, Is.False);
    }

    [Test] public void Calculate_NoArgs_ReturnsFailed()
    {
        var fn = new Calculate<JToken>();
        fn.SetArguments(new Arguments<JToken>());
        var result = fn.Execute(data, data, context);
        Assert.That(result.Success, Is.False);
    }
}
