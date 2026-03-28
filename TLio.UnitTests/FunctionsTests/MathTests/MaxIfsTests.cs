using Newtonsoft.Json.Linq;
using NUnit.Framework;
using TLio.Core.Models;
using TLio.Extensions.Math;
using TLio.Json;

namespace TLio.UnitTests.FunctionsTests.MathTests;

/// <summary>Ported from JLio.UnitTests.Math.MaxIfsTests.</summary>
[TestFixture]
public class MaxIfsTests
{
    private JToken data = null!;
    private TLio.Core.Contracts.IExecutionContext<JToken> context = null!;

    [SetUp]
    public void Setup()
    {
        context = JsonExecutionContext.CreateDefault();
        data = JToken.Parse(@"{
            ""nums"": [1, 2, 3, 4, 5],
            ""cat"": [""A"", ""B"", ""A"", ""C"", ""A""],
            ""crit_A"": ""A"",
            ""crit_Z"": ""Z""
        }");
    }

    [Test] public void MaxIfs_NoMatch_ReturnsFailed()
    {
        var fn = new MaxIfs<JToken>();
        fn.SetArguments(new Arguments<JToken>
        {
            new PathValue<JToken>("$.nums"),
            new PathValue<JToken>("$.cat"),
            new PathValue<JToken>("$.crit_Z")
        });
        var result = fn.Execute(data, data, context);
        Assert.That(result.Success, Is.False);
    }

    [Test] public void MaxIfs_TooFewArgs_ReturnsFailed()
    {
        var fn = new MaxIfs<JToken>();
        fn.SetArguments(new Arguments<JToken>
        {
            new PathValue<JToken>("$.nums"),
            new PathValue<JToken>("$.cat")
        });
        var result = fn.Execute(data, data, context);
        Assert.That(result.Success, Is.False);
    }

    [Test] public void MaxIfs_RangeNotFound_ReturnsFailed()
    {
        var fn = new MaxIfs<JToken>();
        fn.SetArguments(new Arguments<JToken>
        {
            new PathValue<JToken>("$.missing"),
            new PathValue<JToken>("$.cat"),
            new PathValue<JToken>("$.crit_A")
        });
        var result = fn.Execute(data, data, context);
        Assert.That(result.Success, Is.False);
    }
}
