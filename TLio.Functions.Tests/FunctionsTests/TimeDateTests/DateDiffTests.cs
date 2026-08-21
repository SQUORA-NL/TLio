using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using NUnit.Framework;
using TLio.Core.Contracts;
using TLio.Core.Models;
using TLio.Extensions.TimeDate;
using TLio.Json;

namespace TLio.Functions.Tests.FunctionsTests.TimeDateTests;

/// <summary>
/// =datediff(from, to, unit?). The interesting cases are all boundaries: the day before and the
/// day of an anniversary, 29 February, and the sign of a backwards interval.
/// </summary>
[TestFixture]
public class DateDiffTests
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

    [SetUp]
    public void Setup()
    {
        context = JsonExecutionContext.CreateDefault();
        data = ParseDates(@"{
            ""birth"":        ""1985-03-12"",
            ""dayBefore"":    ""2026-03-11"",
            ""dayOf"":        ""2026-03-12"",
            ""leapBirth"":    ""2000-02-29"",
            ""leapNextYear"": ""2001-02-28"",
            ""start"":        ""2024-01-01"",
            ""end"":          ""2024-03-15"",
            ""t1"":           ""2024-03-15T00:00:00Z"",
            ""t2"":           ""2024-03-16T06:30:45Z"",
            ""quotedOn"":     ""2026-08-21"",
            ""driverBirth"":  ""1991-11-04"",
            ""nullDate"":     null
        }");
    }

    private long Diff(string fromPath, string toPath, string? unit = null)
    {
        var fn = new DateDiff<JToken>();
        var args = new Arguments<JToken>
        {
            new PathValue<JToken>(fromPath),
            new PathValue<JToken>(toPath)
        };
        if (unit != null) args.Add(Literal(unit));
        fn.SetArguments(args);
        var result = fn.Execute(data, data, context);
        Assert.That(result.Success, Is.True);
        return result.Data[0].ToObject<long>();
    }

    // ── Calendar-aware years and months ───────────────────────────────────────

    [Test]
    public void DateDiff_Years_DayBeforeTheAnniversary_HasNotCountedIt()
        => Assert.That(Diff("$.birth", "$.dayBefore", "years"), Is.EqualTo(40));

    [Test]
    public void DateDiff_Years_DayOfTheAnniversary_CountsIt()
        => Assert.That(Diff("$.birth", "$.dayOf", "years"), Is.EqualTo(41));

    [Test]
    public void DateDiff_Months_DayBeforeTheAnniversary_IsOneShort()
        => Assert.That(Diff("$.birth", "$.dayBefore", "months"), Is.EqualTo(491));

    [Test]
    public void DateDiff_Months_DayOfTheAnniversary_IsWhole()
        => Assert.That(Diff("$.birth", "$.dayOf", "months"), Is.EqualTo(492));

    /// <summary>
    /// A 29 February birth date has no anniversary in a common year. Month-end clamping gives it
    /// 28 February, the same day =dateadd(...,1,'years') would produce — the two agree.
    /// </summary>
    [Test]
    public void DateDiff_Years_LeapDayBirth_TurnsOnFebruary28InACommonYear()
        => Assert.That(Diff("$.leapBirth", "$.leapNextYear", "years"), Is.EqualTo(1));

    [Test]
    public void DateDiff_Years_IsNotDaysOver365Point25()
    {
        // 40 years of 365.25 days would be 14610 days; the real interval here is 14974.
        Assert.That(Diff("$.birth", "$.dayBefore", "days"), Is.EqualTo(14974));
        Assert.That(Diff("$.birth", "$.dayBefore", "years"), Is.EqualTo(40));
    }

    // ── Interval units ────────────────────────────────────────────────────────

    [Test]
    public void DateDiff_NoUnit_DefaultsToDays()
        => Assert.That(Diff("$.start", "$.end"), Is.EqualTo(74));

    [Test]
    public void DateDiff_Days_CountsTheElapsedInterval()
        => Assert.That(Diff("$.start", "$.end", "days"), Is.EqualTo(74));

    [Test]
    public void DateDiff_Weeks_TruncatesTheDayCount()
        => Assert.That(Diff("$.start", "$.end", "weeks"), Is.EqualTo(10));

    [Test]
    public void DateDiff_Hours_TruncatesTowardZero()
        => Assert.That(Diff("$.t1", "$.t2", "hours"), Is.EqualTo(30));

    [Test]
    public void DateDiff_Minutes_TruncatesTowardZero()
        => Assert.That(Diff("$.t1", "$.t2", "minutes"), Is.EqualTo(1830));

    [Test]
    public void DateDiff_Seconds_AreExact()
        => Assert.That(Diff("$.t1", "$.t2", "seconds"), Is.EqualTo(109845));

    // ── Sign ──────────────────────────────────────────────────────────────────

    [Test]
    public void DateDiff_BackwardsInterval_IsNegative()
        => Assert.That(Diff("$.dayOf", "$.birth", "years"), Is.EqualTo(-41));

    [Test]
    public void DateDiff_BackwardsInterval_TruncatesTowardZeroNotDown()
        => Assert.That(Diff("$.dayBefore", "$.birth", "years"), Is.EqualTo(-40));

    [Test]
    public void DateDiff_BackwardsDays_IsNegative()
        => Assert.That(Diff("$.end", "$.start", "days"), Is.EqualTo(-74));

    [Test]
    public void DateDiff_BackwardsWeeks_TruncateTowardZero()
        => Assert.That(Diff("$.end", "$.start", "weeks"), Is.EqualTo(-10));

    // ── Unit spellings ────────────────────────────────────────────────────────

    [TestCase("year")]
    [TestCase("years")]
    [TestCase("YEARS")]
    [TestCase("Year")]
    public void DateDiff_UnitSpellings_AreAllAccepted(string unit)
        => Assert.That(Diff("$.birth", "$.dayOf", unit), Is.EqualTo(41));

    [Test]
    public void DateDiff_UnknownUnit_FailsAndListsTheAcceptedUnits()
    {
        var fn = new DateDiff<JToken>();
        fn.SetArguments(new Arguments<JToken>
        {
            new PathValue<JToken>("$.birth"),
            new PathValue<JToken>("$.dayOf"),
            Literal("fortnights")
        });
        var result = fn.Execute(data, data, context);

        Assert.That(result.Success, Is.False);
        Assert.That(context.GetLogEntries().Any(e => e.Message.Contains("year(s), month(s)")), Is.True);
    }

    // ── Contract ──────────────────────────────────────────────────────────────

    [Test]
    public void DateDiff_TooFewArgs_ReturnsFailed()
    {
        var fn = new DateDiff<JToken>();
        fn.SetArguments(new Arguments<JToken> { new PathValue<JToken>("$.birth") });
        Assert.That(fn.Execute(data, data, context).Success, Is.False);
    }

    [Test]
    public void DateDiff_TooManyArgs_ReturnsFailed()
    {
        var fn = new DateDiff<JToken>();
        fn.SetArguments(new Arguments<JToken>
        {
            new PathValue<JToken>("$.birth"),
            new PathValue<JToken>("$.dayOf"),
            Literal("years"),
            Literal("extra")
        });
        Assert.That(fn.Execute(data, data, context).Success, Is.False);
    }

    [Test]
    public void DateDiff_PathNotFound_ReturnsFailed()
    {
        var fn = new DateDiff<JToken>();
        fn.SetArguments(new Arguments<JToken>
        {
            new PathValue<JToken>("$.missing"),
            new PathValue<JToken>("$.dayOf")
        });
        Assert.That(fn.Execute(data, data, context).Success, Is.False);
    }

    [Test]
    public void DateDiff_FoundButNull_ReturnsFailed()
    {
        var fn = new DateDiff<JToken>();
        fn.SetArguments(new Arguments<JToken>
        {
            new PathValue<JToken>("$.nullDate"),
            new PathValue<JToken>("$.dayOf")
        });
        Assert.That(fn.Execute(data, data, context).Success, Is.False);
    }

    // ── The idiom it replaces ─────────────────────────────────────────────────

    /// <summary>
    /// The shipped rating samples compute driver age as
    /// floor(calculate(concat('(', 20260821, '-', 19911104, ')/10000'))) = 34.
    /// The replacement must agree exactly.
    /// </summary>
    [Test]
    public void DateDiff_Years_MatchesTheYyyymmddSubtractionIdiomFromTheSamples()
        => Assert.That(Diff("$.driverBirth", "$.quotedOn", "years"), Is.EqualTo(34));
}
