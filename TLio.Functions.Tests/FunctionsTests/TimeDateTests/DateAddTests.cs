using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using NUnit.Framework;
using TLio.Core.Contracts;
using TLio.Core.Models;
using TLio.Extensions.TimeDate;
using TLio.Json;

namespace TLio.Functions.Tests.FunctionsTests.TimeDateTests;

/// <summary>
/// =dateadd(date, amount, unit?). The cases worth holding are month-end clamping, negative
/// amounts, and a date-only input staying date-only.
/// </summary>
[TestFixture]
public class DateAddTests
{
    private JToken data = null!;
    private IExecutionContext<JToken> context = null!;

    private static JToken ParseDates(string json)
    {
        using var reader = new JsonTextReader(new System.IO.StringReader(json))
            { DateParseHandling = DateParseHandling.None };
        return JToken.Load(reader);
    }

    private static IFunctionSupportedValue<JToken> Literal(string text) =>
        new FixedValue<JToken>(new JValue(text));

    private static IFunctionSupportedValue<JToken> Number(long value) =>
        new FixedValue<JToken>(new JValue(value));

    [SetUp]
    public void Setup()
    {
        context = JsonExecutionContext.CreateDefault();
        data = ParseDates(@"{
            ""janEnd"":     ""2024-01-31"",
            ""janEnd23"":   ""2023-01-31"",
            ""leapDay"":    ""2024-02-29"",
            ""mid"":        ""2024-03-15"",
            ""stamp"":      ""2024-03-15T10:30:00Z"",
            ""startDate"":  ""2026-09-01"",
            ""amount"":     3,
            ""nullDate"":   null,
            ""nullAmount"": null
        }");
    }

    private string Add(string datePath, long amount, string? unit = null)
    {
        var fn = new DateAdd<JToken>();
        var args = new Arguments<JToken> { new PathValue<JToken>(datePath), Number(amount) };
        if (unit != null) args.Add(Literal(unit));
        fn.SetArguments(args);
        var result = fn.Execute(data, data, context);
        Assert.That(result.Success, Is.True);
        return result.Data[0].ToObject<string>()!;
    }

    // ── Month-end clamping ────────────────────────────────────────────────────

    [Test]
    public void DateAdd_OneMonthFromJanuary31_ClampsToFebruary29InALeapYear()
        => Assert.That(Add("$.janEnd", 1, "months"), Is.EqualTo("2024-02-29"));

    [Test]
    public void DateAdd_OneMonthFromJanuary31_ClampsToFebruary28InACommonYear()
        => Assert.That(Add("$.janEnd23", 1, "months"), Is.EqualTo("2023-02-28"));

    [Test]
    public void DateAdd_OneYearFromFebruary29_ClampsToFebruary28()
        => Assert.That(Add("$.leapDay", 1, "years"), Is.EqualTo("2025-02-28"));

    [Test]
    public void DateAdd_FourYearsFromFebruary29_LandsOnFebruary29Again()
        => Assert.That(Add("$.leapDay", 4, "years"), Is.EqualTo("2028-02-29"));

    // ── Units ─────────────────────────────────────────────────────────────────

    [Test]
    public void DateAdd_NoUnit_DefaultsToDays()
        => Assert.That(Add("$.mid", 10), Is.EqualTo("2024-03-25"));

    [Test]
    public void DateAdd_Weeks_AddsSevenDaysEach()
        => Assert.That(Add("$.mid", 2, "weeks"), Is.EqualTo("2024-03-29"));

    [Test]
    public void DateAdd_Days_CrossesTheMonthBoundary()
        => Assert.That(Add("$.mid", 20, "days"), Is.EqualTo("2024-04-04"));

    [Test]
    public void DateAdd_Hours_TurnsADateOnlyValueIntoATimestamp()
        => Assert.That(Add("$.mid", 6, "hours"), Is.EqualTo("2024-03-15T06:00:00Z"));

    [Test]
    public void DateAdd_Minutes_KeepsTheTimeComponent()
        => Assert.That(Add("$.stamp", 45, "minutes"), Is.EqualTo("2024-03-15T11:15:00Z"));

    [Test]
    public void DateAdd_Seconds_KeepsTheTimeComponent()
        => Assert.That(Add("$.stamp", 30, "seconds"), Is.EqualTo("2024-03-15T10:30:30Z"));

    /// <summary>A whole number of hours off midnight lands back on midnight, and is a date again.</summary>
    [Test]
    public void DateAdd_TwentyFourHours_ReturnsToADateOnlyString()
        => Assert.That(Add("$.mid", 24, "hours"), Is.EqualTo("2024-03-16"));

    // ── Negative amounts ──────────────────────────────────────────────────────

    [Test]
    public void DateAdd_NegativeDays_Subtracts()
        => Assert.That(Add("$.mid", -10, "days"), Is.EqualTo("2024-03-05"));

    [Test]
    public void DateAdd_NegativeMonths_ClampsTheSameWay()
        => Assert.That(Add("$.leapDay", -12, "months"), Is.EqualTo("2023-02-28"));

    [Test]
    public void DateAdd_ZeroAmount_ReturnsTheSameDate()
        => Assert.That(Add("$.mid", 0, "years"), Is.EqualTo("2024-03-15"));

    // ── Amount as a path ──────────────────────────────────────────────────────

    [Test]
    public void DateAdd_AmountFromAPath_IsResolved()
    {
        var fn = new DateAdd<JToken>();
        fn.SetArguments(new Arguments<JToken>
        {
            new PathValue<JToken>("$.mid"),
            new PathValue<JToken>("$.amount"),
            Literal("days")
        });
        var result = fn.Execute(data, data, context);
        Assert.That(result.Success, Is.True);
        Assert.That(result.Data[0].ToObject<string>(), Is.EqualTo("2024-03-18"));
    }

    // ── Contract ──────────────────────────────────────────────────────────────

    [Test]
    public void DateAdd_UnknownUnit_FailsAndListsTheAcceptedUnits()
    {
        var fn = new DateAdd<JToken>();
        fn.SetArguments(new Arguments<JToken>
        {
            new PathValue<JToken>("$.mid"), Number(1), Literal("decades")
        });
        var result = fn.Execute(data, data, context);

        Assert.That(result.Success, Is.False);
        Assert.That(context.GetLogEntries().Any(e => e.Message.Contains("year(s), month(s)")), Is.True);
    }

    [Test]
    public void DateAdd_TooFewArgs_ReturnsFailed()
    {
        var fn = new DateAdd<JToken>();
        fn.SetArguments(new Arguments<JToken> { new PathValue<JToken>("$.mid") });
        Assert.That(fn.Execute(data, data, context).Success, Is.False);
    }

    [Test]
    public void DateAdd_TooManyArgs_ReturnsFailed()
    {
        var fn = new DateAdd<JToken>();
        fn.SetArguments(new Arguments<JToken>
        {
            new PathValue<JToken>("$.mid"), Number(1), Literal("days"), Literal("extra")
        });
        Assert.That(fn.Execute(data, data, context).Success, Is.False);
    }

    [Test]
    public void DateAdd_PathNotFound_ReturnsFailed()
    {
        var fn = new DateAdd<JToken>();
        fn.SetArguments(new Arguments<JToken> { new PathValue<JToken>("$.missing"), Number(1) });
        Assert.That(fn.Execute(data, data, context).Success, Is.False);
    }

    [Test]
    public void DateAdd_FoundButNullDate_ReturnsFailed()
    {
        var fn = new DateAdd<JToken>();
        fn.SetArguments(new Arguments<JToken> { new PathValue<JToken>("$.nullDate"), Number(1) });
        Assert.That(fn.Execute(data, data, context).Success, Is.False);
    }

    [Test]
    public void DateAdd_FoundButNullAmount_ReturnsFailed()
    {
        var fn = new DateAdd<JToken>();
        fn.SetArguments(new Arguments<JToken>
        {
            new PathValue<JToken>("$.mid"), new PathValue<JToken>("$.nullAmount")
        });
        Assert.That(fn.Execute(data, data, context).Success, Is.False);
    }

    [Test]
    public void DateAdd_OutOfRange_FailsRatherThanThrowing()
    {
        var fn = new DateAdd<JToken>();
        fn.SetArguments(new Arguments<JToken>
        {
            new PathValue<JToken>("$.mid"), Number(100000), Literal("years")
        });
        Assert.That(fn.Execute(data, data, context).Success, Is.False);
    }

    // ── The idiom it replaces ─────────────────────────────────────────────────

    /// <summary>
    /// The samples build the renewal date as
    /// concat(sum(substring(startDate,0,4),1), substring(startDate,4,6)) — "2027" + "-09-01".
    /// </summary>
    [Test]
    public void DateAdd_Years_MatchesTheStringSlicingRenewalDateFromTheSamples()
        => Assert.That(Add("$.startDate", 1, "years"), Is.EqualTo("2027-09-01"));
}
