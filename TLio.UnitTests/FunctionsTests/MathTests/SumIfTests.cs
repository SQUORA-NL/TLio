using Newtonsoft.Json.Linq;
using NUnit.Framework;
using TLio.Core.Models;
using TLio.Extensions.Math;
using TLio.Json;

namespace TLio.UnitTests.FunctionsTests.MathTests;

/// <summary>Ported from JLio.UnitTests.Math.SumIfTests.</summary>
[TestFixture]
public class SumIfTests
{
    private JToken data = null!;
    private TLio.Core.Contracts.IExecutionContext<JToken> context = null!;

    [SetUp]
    public void Setup()
    {
        context = JsonExecutionContext.CreateDefault();
        data = JToken.Parse(@"{
            ""nums"": [1, 2, 3, 4, 5],
            ""values"": [10, 20, 30, 40, 50],
            ""cat"": [""A"", ""B"", ""A"", ""C"", ""A""],
            ""crit_gt3"": "">3"",
            ""crit_A"": ""A""
        }");
    }

    [Test] public void SumIf_NoMatch_ReturnsZero()
    {
        var fn = new SumIf<JToken>();
        fn.SetArguments(new Arguments<JToken>
        {
            new PathValue<JToken>("$.nums"),
            new PathValue<JToken>("$.crit_A") // "A" won't match any number
        });
        var result = fn.Execute(data, data, context);
        Assert.That(result.Success, Is.True);
        Assert.That(result.Data.First!.Value<double>(), Is.EqualTo(0));
    }

    [Test] public void SumIf_RangeNotFound_ReturnsFailed()
    {
        var fn = new SumIf<JToken>();
        fn.SetArguments(new Arguments<JToken>
        {
            new PathValue<JToken>("$.missing"),
            new PathValue<JToken>("$.crit_gt3")
        });
        var result = fn.Execute(data, data, context);
        Assert.That(result.Success, Is.False);
    }
}
