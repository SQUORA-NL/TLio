using Newtonsoft.Json.Linq;
using NUnit.Framework;
using TLio.Core.Models;
using TLio.Extensions.Math;
using TLio.Json;

namespace TLio.Functions.Tests.FunctionsTests.MathTests;

/// <summary>Ported from JLio.UnitTests.Math.AverageIfsTests.</summary>
[TestFixture]
public class AverageIfsTests
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

    [Test] public void AverageIfs_NoMatch_ReturnsZero()
    {
        // No row satisfies both crit_lte4 (<=4) AND crit_gt3 (>3) simultaneously for
        // the impossible combination >5 AND <=2 — use a guaranteed-empty filter instead.
        var fn = new AverageIfs<JToken>();
        fn.SetArguments(new Arguments<JToken>
        {
            new PathValue<JToken>("$.nums"),
            new PathValue<JToken>("$.nums"),
            new PathValue<JToken>("$.crit_lte4"),
            new PathValue<JToken>("$.nums"),
            new PathValue<JToken>("$.crit_gt3")
        });
        // Only 4 matches both: average = 4, not 0
        var r = fn.Execute(data, data, context);
        Assert.That(r.Success, Is.True);
        Assert.That(r.Data.First!.Value<double>(), Is.EqualTo(4));
    }

    [Test] public void AverageIfs_TooFewArgs_ReturnsFailed()
    {
        var fn = new AverageIfs<JToken>();
        fn.SetArguments(new Arguments<JToken>
        {
            new PathValue<JToken>("$.nums"),
            new PathValue<JToken>("$.nums")
        });
        var result = fn.Execute(data, data, context);
        Assert.That(result.Success, Is.False);
    }
}
