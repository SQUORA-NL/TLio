using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using NUnit.Framework;
using TLio.Core.Contracts;
using TLio.Core.Models;
using TLio.Extensions.TimeDate;
using TLio.Json;

namespace TLio.Functions.Tests.FunctionsTests.TimeDateTests;

/// <summary>
/// Extended coverage for TimeDate extension functions.
/// Covers edge cases not exercised in the existing DateCompareTests,
/// IsDateBetweenTests, MinMaxDateTests, and AvgDateTests files.
/// </summary>
[TestFixture]
public class TimeDate_ExtendedTests
{
    private JToken data = null!;
    private IExecutionContext<JToken> context = null!;

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
            ""d1"":       ""2024-01-01"",
            ""d2"":       ""2024-06-15"",
            ""d3"":       ""2024-12-31"",
            ""same1"":    ""2024-03-15"",
            ""same2"":    ""2024-03-15"",
            ""leap"":     ""2024-02-29"",
            ""yearEnd"":  ""2024-12-31"",
            ""yearStart"":""2025-01-01"",
            ""ts1"":      ""2024-03-15T00:00:00Z"",
            ""ts2"":      ""2024-03-15T23:59:59Z"",
            ""invalid"":  ""not-a-date"",
            ""dates"":    [""2024-01-01"", ""2024-06-15"", ""2024-12-31""]
        }");
    }

    // ── datecompare ───────────────────────────────────────────────────────────

    [Test]
    public void DateCompare_EqualDates_ReturnsZero()
    {
        var fn = new DateCompare<JToken>();
        fn.SetArguments(new Arguments<JToken>
        {
            new PathValue<JToken>("$.same1"),
            new PathValue<JToken>("$.same2")
        });
        var result = fn.Execute(data, data, context);
        Assert.That(result.Success, Is.True);
        Assert.That(result.Data[0].ToObject<long>(), Is.EqualTo(0));
    }

    [Test]
    public void DateCompare_EarlierBeforeLater_ReturnsMinusOne()
    {
        var fn = new DateCompare<JToken>();
        fn.SetArguments(new Arguments<JToken>
        {
            new PathValue<JToken>("$.d1"),
            new PathValue<JToken>("$.d2")
        });
        var result = fn.Execute(data, data, context);
        Assert.That(result.Success, Is.True);
        Assert.That(result.Data[0].ToObject<long>(), Is.EqualTo(-1));
    }

    [Test]
    public void DateCompare_LaterBeforeEarlier_ReturnsPlusOne()
    {
        var fn = new DateCompare<JToken>();
        fn.SetArguments(new Arguments<JToken>
        {
            new PathValue<JToken>("$.d3"),
            new PathValue<JToken>("$.d1")
        });
        var result = fn.Execute(data, data, context);
        Assert.That(result.Success, Is.True);
        Assert.That(result.Data[0].ToObject<long>(), Is.EqualTo(1));
    }

    [Test]
    public void DateCompare_InvalidDateArg_ReturnsFailed()
    {
        var fn = new DateCompare<JToken>();
        fn.SetArguments(new Arguments<JToken>
        {
            new PathValue<JToken>("$.invalid"),
            new PathValue<JToken>("$.d1")
        });
        var result = fn.Execute(data, data, context);
        Assert.That(result.Success, Is.False);
    }

    [Test]
    public void DateCompare_TimestampsWithinSameDay_ComparesCorrectly()
    {
        var fn = new DateCompare<JToken>();
        fn.SetArguments(new Arguments<JToken>
        {
            new PathValue<JToken>("$.ts1"),
            new PathValue<JToken>("$.ts2")
        });
        var result = fn.Execute(data, data, context);
        Assert.That(result.Success, Is.True);
        Assert.That(result.Data[0].ToObject<long>(), Is.EqualTo(-1));
    }

    [Test]
    public void DateCompare_LeapYearDate_ParsesSuccessfully()
    {
        var fn = new DateCompare<JToken>();
        fn.SetArguments(new Arguments<JToken>
        {
            new PathValue<JToken>("$.leap"),
            new PathValue<JToken>("$.d1")
        });
        var result = fn.Execute(data, data, context);
        Assert.That(result.Success, Is.True);
        Assert.That(result.Data[0].ToObject<long>(), Is.EqualTo(1));
    }

    [Test]
    public void DateCompare_YearBoundary_Jan01_After_Dec31()
    {
        var fn = new DateCompare<JToken>();
        fn.SetArguments(new Arguments<JToken>
        {
            new PathValue<JToken>("$.yearStart"),
            new PathValue<JToken>("$.yearEnd")
        });
        var result = fn.Execute(data, data, context);
        Assert.That(result.Success, Is.True);
        Assert.That(result.Data[0].ToObject<long>(), Is.EqualTo(1));
    }

    // ── isdatebetween ─────────────────────────────────────────────────────────

    [Test]
    public void IsDateBetween_DateOnStartBoundary_ReturnsTrue()
    {
        var fn = new IsDateBetween<JToken>();
        fn.SetArguments(new Arguments<JToken>
        {
            new PathValue<JToken>("$.d1"),   // date  = 2024-01-01
            new PathValue<JToken>("$.d1"),   // start = 2024-01-01
            new PathValue<JToken>("$.d3")    // end   = 2024-12-31
        });
        var result = fn.Execute(data, data, context);
        Assert.That(result.Success, Is.True);
        Assert.That(result.Data[0].ToObject<bool>(), Is.True);
    }

    [Test]
    public void IsDateBetween_DateOnEndBoundary_ReturnsTrue()
    {
        var fn = new IsDateBetween<JToken>();
        fn.SetArguments(new Arguments<JToken>
        {
            new PathValue<JToken>("$.d3"),   // date  = 2024-12-31
            new PathValue<JToken>("$.d1"),   // start = 2024-01-01
            new PathValue<JToken>("$.d3")    // end   = 2024-12-31
        });
        var result = fn.Execute(data, data, context);
        Assert.That(result.Success, Is.True);
        Assert.That(result.Data[0].ToObject<bool>(), Is.True);
    }

    [Test]
    public void IsDateBetween_AllSameDates_ReturnsTrue()
    {
        var fn = new IsDateBetween<JToken>();
        fn.SetArguments(new Arguments<JToken>
        {
            new PathValue<JToken>("$.same1"),
            new PathValue<JToken>("$.same2"),
            new PathValue<JToken>("$.same1")
        });
        var result = fn.Execute(data, data, context);
        Assert.That(result.Success, Is.True);
        Assert.That(result.Data[0].ToObject<bool>(), Is.True);
    }

    [Test]
    public void IsDateBetween_DateOutsideRange_ReturnsFalse()
    {
        var fn = new IsDateBetween<JToken>();
        fn.SetArguments(new Arguments<JToken>
        {
            new PathValue<JToken>("$.yearStart"), // 2025-01-01 — outside 2024 range
            new PathValue<JToken>("$.d1"),
            new PathValue<JToken>("$.d3")
        });
        var result = fn.Execute(data, data, context);
        Assert.That(result.Success, Is.True);
        Assert.That(result.Data[0].ToObject<bool>(), Is.False);
    }

    // ── mindate / maxdate ─────────────────────────────────────────────────────

    [Test]
    public void MinDate_ArrayOfDates_ReturnsEarliest()
    {
        var fn = new MinDate<JToken>();
        fn.SetArguments(new Arguments<JToken> { new PathValue<JToken>("$.dates") });
        var result = fn.Execute(data, data, context);
        Assert.That(result.Success, Is.True);
        Assert.That(result.Data[0].ToObject<string>(), Does.StartWith("2024-01-01"));
    }

    [Test]
    public void MaxDate_ArrayOfDates_ReturnsLatest()
    {
        var fn = new MaxDate<JToken>();
        fn.SetArguments(new Arguments<JToken> { new PathValue<JToken>("$.dates") });
        var result = fn.Execute(data, data, context);
        Assert.That(result.Success, Is.True);
        Assert.That(result.Data[0].ToObject<string>(), Does.StartWith("2024-12-31"));
    }

    [Test]
    public void MinDate_TwoScalarArgs_ReturnsSmaller()
    {
        var fn = new MinDate<JToken>();
        fn.SetArguments(new Arguments<JToken>
        {
            new PathValue<JToken>("$.d3"),
            new PathValue<JToken>("$.d1")
        });
        var result = fn.Execute(data, data, context);
        Assert.That(result.Success, Is.True);
        Assert.That(result.Data[0].ToObject<string>(), Does.StartWith("2024-01-01"));
    }

    [Test]
    public void MaxDate_TwoScalarArgs_ReturnsLarger()
    {
        var fn = new MaxDate<JToken>();
        fn.SetArguments(new Arguments<JToken>
        {
            new PathValue<JToken>("$.d1"),
            new PathValue<JToken>("$.d3")
        });
        var result = fn.Execute(data, data, context);
        Assert.That(result.Success, Is.True);
        Assert.That(result.Data[0].ToObject<string>(), Does.StartWith("2024-12-31"));
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
    public void MaxDate_NoArgs_ReturnsFailed()
    {
        var fn = new MaxDate<JToken>();
        fn.SetArguments(new Arguments<JToken>());
        var result = fn.Execute(data, data, context);
        Assert.That(result.Success, Is.False);
    }

    [Test]
    public void MinDate_InvalidDate_ReturnsFailed()
    {
        var fn = new MinDate<JToken>();
        fn.SetArguments(new Arguments<JToken> { new PathValue<JToken>("$.invalid") });
        var result = fn.Execute(data, data, context);
        Assert.That(result.Success, Is.False);
    }

    // ── avgdate ───────────────────────────────────────────────────────────────

    [Test]
    public void AvgDate_SingleElement_ReturnsThatElement()
    {
        var fn = new AvgDate<JToken>();
        fn.SetArguments(new Arguments<JToken> { new PathValue<JToken>("$.d1") });
        var result = fn.Execute(data, data, context);
        Assert.That(result.Success, Is.True);
        Assert.That(result.Data[0].ToObject<string>(), Does.StartWith("2024-01-01"));
    }

    [Test]
    public void AvgDate_ArrayPath_ReturnsAverageDate()
    {
        var fn = new AvgDate<JToken>();
        fn.SetArguments(new Arguments<JToken> { new PathValue<JToken>("$.dates") });
        var result = fn.Execute(data, data, context);
        Assert.That(result.Success, Is.True);
        // Average of 2024-01-01, 2024-06-15, 2024-12-31 should be around 2024-07-15
        Assert.That(result.Data[0].ToObject<string>(), Does.StartWith("2024"));
    }

    [Test]
    public void AvgDate_TwoEqualDates_ReturnsThatDate()
    {
        var fn = new AvgDate<JToken>();
        fn.SetArguments(new Arguments<JToken>
        {
            new PathValue<JToken>("$.same1"),
            new PathValue<JToken>("$.same2")
        });
        var result = fn.Execute(data, data, context);
        Assert.That(result.Success, Is.True);
        Assert.That(result.Data[0].ToObject<string>(), Does.StartWith("2024-03-15"));
    }

    [Test]
    public void AvgDate_NoArgs_ReturnsFailed()
    {
        var fn = new AvgDate<JToken>();
        fn.SetArguments(new Arguments<JToken>());
        var result = fn.Execute(data, data, context);
        Assert.That(result.Success, Is.False);
    }
}
