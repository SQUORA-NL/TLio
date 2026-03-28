using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using NUnit.Framework;
using TLio.Core.Models;
using TLio.Extensions.TimeDate;
using TLio.Json;

namespace TLio.Functions.Tests.FunctionsTests.TimeDateTests;

[TestFixture]
public class MinMaxDateTests
{
    private JToken data = null!;
    private TLio.Core.Contracts.IExecutionContext<JToken> context = null!;

    private static JToken ParseDates(string json)
    {
        using var reader = new JsonTextReader(new System.IO.StringReader(json))
            { DateParseHandling = DateParseHandling.None };
        return JToken.Load(reader);
    }

    [SetUp]
    public void Setup()
    {
        context = JsonExecutionContext.CreateDefault();
        data = ParseDates(@"{
            ""d1"":    ""2024-01-01"",
            ""d2"":    ""2024-06-15"",
            ""d3"":    ""2023-11-30"",
            ""dates"": [""2024-03-01"", ""2022-07-04"", ""2025-01-01""],
            ""ts1"":   ""2024-03-15T08:00:00Z"",
            ""ts2"":   ""2024-03-15T20:00:00Z""
        }");
    }

    [Test]
    public void MinDate_NoArgs_ReturnsFailed()
    {
        var fn = new MinDate<JToken>();
        fn.SetArguments(new Arguments<JToken>());
        var result = fn.Execute(data, data, context);
        Assert.That(result.Success, Is.False);
    }

    [Test]
    public void MinDate_PathNotFound_ReturnsFailed()
    {
        var fn = new MinDate<JToken>();
        fn.SetArguments(new Arguments<JToken> { new PathValue<JToken>("$.missing") });
        var result = fn.Execute(data, data, context);
        Assert.That(result.Success, Is.False);
    }

    [Test]
    public void MaxDate_NoArgs_ReturnsFailed()
    {
        var fn = new MaxDate<JToken>();
        fn.SetArguments(new Arguments<JToken>());
        var result = fn.Execute(data, data, context);
        Assert.That(result.Success, Is.False);
    }

    [Test]
    public void MaxDate_PathNotFound_ReturnsFailed()
    {
        var fn = new MaxDate<JToken>();
        fn.SetArguments(new Arguments<JToken> { new PathValue<JToken>("$.missing") });
        var result = fn.Execute(data, data, context);
        Assert.That(result.Success, Is.False);
    }
}
