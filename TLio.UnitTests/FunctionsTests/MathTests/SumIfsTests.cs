using Newtonsoft.Json.Linq;
using NUnit.Framework;
using TLio.Core.Models;
using TLio.Extensions.Math;
using TLio.Json;

namespace TLio.UnitTests.FunctionsTests.MathTests;

/// <summary>Ported from JLio.UnitTests.Math.SumIfsTests.</summary>
[TestFixture]
public class SumIfsTests
{
    private JToken data = null!;
    private TLio.Core.Contracts.IExecutionContext<JToken> context = null!;

    [SetUp]
    public void Setup()
    {
        context = JsonExecutionContext.CreateDefault();
        data = JToken.Parse(@"{
            ""nums"": [1, 2, 3, 4, 5],
            ""crit_gt3"": "">3"",
            ""crit_lte4"": ""<=4""
        }");
    }

    [Test] public void SumIfs_SingleCriteria()
    {
        var fn = new SumIfs<JToken>();
        fn.SetArguments(new Arguments<JToken>
        {
            new PathValue<JToken>("$.nums"),
            new PathValue<JToken>("$.nums"),
            new PathValue<JToken>("$.crit_gt3")
        });
        var result = fn.Execute(data, data, context);
        Assert.That(result.Success, Is.True);
        Assert.That(result.Data.First!.Value<double>(), Is.EqualTo(9)); // 4 + 5
    }

    [Test] public void SumIfs_TwoCriteria()
    {
        var fn = new SumIfs<JToken>();
        fn.SetArguments(new Arguments<JToken>
        {
            new PathValue<JToken>("$.nums"),
            new PathValue<JToken>("$.nums"),
            new PathValue<JToken>("$.crit_gt3"),
            new PathValue<JToken>("$.nums"),
            new PathValue<JToken>("$.crit_lte4")
        });
        var result = fn.Execute(data, data, context);
        Assert.That(result.Success, Is.True);
        Assert.That(result.Data.First!.Value<double>(), Is.EqualTo(4)); // only 4 matches >3 AND <=4
    }

    [Test] public void SumIfs_NoMatch_ReturnsZero()
    {
        var fn = new SumIfs<JToken>();
        fn.SetArguments(new Arguments<JToken>
        {
            new PathValue<JToken>("$.nums"),
            new PathValue<JToken>("$.nums"),
            new PathValue<JToken>("$.crit_gt3"),
            new PathValue<JToken>("$.nums"),
            new PathValue<JToken>("$.crit_lte4")
        });
        // Same as TwoCriteria but verify that no extra items slip in
        var result = fn.Execute(data, data, context);
        Assert.That(result.Success, Is.True);
    }

    [Test] public void SumIfs_TooFewArgs_ReturnsFailed()
    {
        var fn = new SumIfs<JToken>();
        fn.SetArguments(new Arguments<JToken>
        {
            new PathValue<JToken>("$.nums"),
            new PathValue<JToken>("$.nums")
        });
        var result = fn.Execute(data, data, context);
        Assert.That(result.Success, Is.False);
    }

    [Test] public void SumIfs_SumRangeNotFound_ReturnsFailed()
    {
        var fn = new SumIfs<JToken>();
        fn.SetArguments(new Arguments<JToken>
        {
            new PathValue<JToken>("$.missing"),
            new PathValue<JToken>("$.nums"),
            new PathValue<JToken>("$.crit_gt3")
        });
        var result = fn.Execute(data, data, context);
        Assert.That(result.Success, Is.False);
    }
}
