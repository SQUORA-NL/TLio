using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using NUnit.Framework;
using TLio.Core.Models;
using TLio.Extensions.TimeDate;
using TLio.Json;

namespace TLio.UnitTests.FunctionsTests.TimeDateTests;

[TestFixture]
public class AvgDateTests
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
            ""d2"":    ""2024-12-31"",
            ""dates"": [""2024-01-01"", ""2024-07-01"", ""2024-12-31""],
            ""ts1"":   ""2024-03-15T00:00:00Z"",
            ""ts2"":   ""2024-03-15T12:00:00Z""
        }");
    }

    [Test]
    public void AvgDate_TwoScalars_ReturnsMidpoint()
    {
        var fn = new AvgDate<JToken>();
        fn.SetArguments(new Arguments<JToken>
        {
            new PathValue<JToken>("$.d1"),
            new PathValue<JToken>("$.d2")
        });
        var result = fn.Execute(data, data, context);
        Assert.That(result.Success, Is.True);
        // 2024-01-01 + 2024-12-31 midpoint ≈ 2024-07-01 (or 2024-07-02 depending on rounding)
        var dateStr = result.Data.First!.Value<string>()!;
        Assert.That(dateStr, Does.StartWith("2024-07-0"));
    }

    [Test]
    public void AvgDate_Array_ReturnsMidpoint()
    {
        var fn = new AvgDate<JToken>();
        fn.SetArguments(new Arguments<JToken> { new PathValue<JToken>("$.dates") });
        var result = fn.Execute(data, data, context);
        Assert.That(result.Success, Is.True);
        // [2024-01-01, 2024-07-01, 2024-12-31] average ≈ 2024-07-01
        var dateStr = result.Data.First!.Value<string>()!;
        Assert.That(dateStr, Does.StartWith("2024-07-0"));
    }

    [Test]
    public void AvgDate_SingleDate_ReturnsSameDate()
    {
        var fn = new AvgDate<JToken>();
        fn.SetArguments(new Arguments<JToken> { new PathValue<JToken>("$.d1") });
        var result = fn.Execute(data, data, context);
        Assert.That(result.Success, Is.True);
        Assert.That(result.Data.First!.Value<string>(), Is.EqualTo("2024-01-01"));
    }

    [Test]
    public void AvgDate_WithTimestamps_ReturnsAveragedTimestamp()
    {
        var fn = new AvgDate<JToken>();
        fn.SetArguments(new Arguments<JToken>
        {
            new PathValue<JToken>("$.ts1"),
            new PathValue<JToken>("$.ts2")
        });
        var result = fn.Execute(data, data, context);
        Assert.That(result.Success, Is.True);
        // Average of 00:00 and 12:00 = 06:00
        Assert.That(result.Data.First!.Value<string>(), Is.EqualTo("2024-03-15T06:00:00Z"));
    }

    [Test]
    public void AvgDate_NoArgs_ReturnsFailed()
    {
        var fn = new AvgDate<JToken>();
        fn.SetArguments(new Arguments<JToken>());
        var result = fn.Execute(data, data, context);
        Assert.That(result.Success, Is.False);
    }

    [Test]
    public void AvgDate_PathNotFound_ReturnsFailed()
    {
        var fn = new AvgDate<JToken>();
        fn.SetArguments(new Arguments<JToken> { new PathValue<JToken>("$.missing") });
        var result = fn.Execute(data, data, context);
        Assert.That(result.Success, Is.False);
    }
}
