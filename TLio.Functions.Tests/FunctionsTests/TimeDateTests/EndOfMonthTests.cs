using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using NUnit.Framework;
using TLio.Core.Contracts;
using TLio.Core.Models;
using TLio.Extensions.TimeDate;
using TLio.Json;

namespace TLio.Functions.Tests.FunctionsTests.TimeDateTests;

/// <summary>
/// =endofmonth(date) — the last day of that date's month. February is the whole reason the
/// function exists, so both a leap and a common year are pinned.
/// </summary>
[TestFixture]
public class EndOfMonthTests
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
            ""feb2024"":  ""2024-02-10"",
            ""feb2023"":  ""2023-02-10"",
            ""feb1900"":  ""1900-02-10"",
            ""feb2000"":  ""2000-02-10"",
            ""april"":    ""2024-04-10"",
            ""december"": ""2024-12-05"",
            ""stamp"":    ""2024-04-10T22:15:00Z"",
            ""onTheEnd"": ""2024-04-30"",
            ""nullDate"": null
        }");
    }

    private string End(string datePath)
    {
        var fn = new EndOfMonth<JToken>();
        fn.SetArguments(new Arguments<JToken> { new PathValue<JToken>(datePath) });
        var result = fn.Execute(data, data, context);
        Assert.That(result.Success, Is.True);
        return result.Data[0].ToObject<string>()!;
    }

    [Test]
    public void EndOfMonth_FebruaryInALeapYear_IsTheTwentyNinth()
        => Assert.That(End("$.feb2024"), Is.EqualTo("2024-02-29"));

    [Test]
    public void EndOfMonth_FebruaryInACommonYear_IsTheTwentyEighth()
        => Assert.That(End("$.feb2023"), Is.EqualTo("2023-02-28"));

    /// <summary>1900 is divisible by 4 but not a leap year; 2000 is, because it is divisible by 400.</summary>
    [Test]
    public void EndOfMonth_CenturyYears_FollowTheGregorianRule()
    {
        Assert.That(End("$.feb1900"), Is.EqualTo("1900-02-28"));
        Assert.That(End("$.feb2000"), Is.EqualTo("2000-02-29"));
    }

    [Test]
    public void EndOfMonth_ThirtyDayMonth_IsTheThirtieth()
        => Assert.That(End("$.april"), Is.EqualTo("2024-04-30"));

    [Test]
    public void EndOfMonth_December_DoesNotRollIntoTheNextYear()
        => Assert.That(End("$.december"), Is.EqualTo("2024-12-31"));

    [Test]
    public void EndOfMonth_Timestamp_DropsTheTimeComponent()
        => Assert.That(End("$.stamp"), Is.EqualTo("2024-04-30"));

    [Test]
    public void EndOfMonth_AlreadyTheLastDay_IsIdempotent()
        => Assert.That(End("$.onTheEnd"), Is.EqualTo("2024-04-30"));

    // ── Contract ──────────────────────────────────────────────────────────────

    [Test]
    public void EndOfMonth_NoArgs_ReturnsFailed()
    {
        var fn = new EndOfMonth<JToken>();
        fn.SetArguments(new Arguments<JToken>());
        Assert.That(fn.Execute(data, data, context).Success, Is.False);
    }

    [Test]
    public void EndOfMonth_TooManyArgs_ReturnsFailed()
    {
        var fn = new EndOfMonth<JToken>();
        fn.SetArguments(new Arguments<JToken>
        {
            new PathValue<JToken>("$.april"), new PathValue<JToken>("$.december")
        });
        Assert.That(fn.Execute(data, data, context).Success, Is.False);
    }

    [Test]
    public void EndOfMonth_PathNotFound_ReturnsFailed()
    {
        var fn = new EndOfMonth<JToken>();
        fn.SetArguments(new Arguments<JToken> { new PathValue<JToken>("$.missing") });
        Assert.That(fn.Execute(data, data, context).Success, Is.False);
    }

    [Test]
    public void EndOfMonth_FoundButNull_ReturnsFailed()
    {
        var fn = new EndOfMonth<JToken>();
        fn.SetArguments(new Arguments<JToken> { new PathValue<JToken>("$.nullDate") });
        Assert.That(fn.Execute(data, data, context).Success, Is.False);
    }

    /// <summary>
    /// The =dateadd(...) route to a month end — go to the first, add a month, step back a day —
    /// gives the same answer, and is what this function saves a script author from writing.
    /// </summary>
    [Test]
    public void EndOfMonth_AgreesWithTheDateAddRoundTrip()
    {
        var start = new StartOfMonth<JToken>();
        start.SetArguments(new Arguments<JToken> { new PathValue<JToken>("$.feb2024") });
        var first = start.Execute(data, data, context).Data[0].ToObject<string>()!;

        var plusMonth = new DateAdd<JToken>();
        plusMonth.SetArguments(new Arguments<JToken>
        {
            new FixedValue<JToken>(new JValue(first)),
            new FixedValue<JToken>(new JValue(1L)),
            new FixedValue<JToken>(new JValue("months"))
        });
        var nextFirst = plusMonth.Execute(data, data, context).Data[0].ToObject<string>()!;

        var minusDay = new DateAdd<JToken>();
        minusDay.SetArguments(new Arguments<JToken>
        {
            new FixedValue<JToken>(new JValue(nextFirst)),
            new FixedValue<JToken>(new JValue(-1L))
        });
        var lastDay = minusDay.Execute(data, data, context).Data[0].ToObject<string>()!;

        Assert.That(lastDay, Is.EqualTo(End("$.feb2024")));
    }
}
