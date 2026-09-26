using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using NUnit.Framework;
using TLio.Core.Contracts;
using TLio.Core.Models;
using TLio.Extensions.TimeDate;
using TLio.Json;

namespace TLio.Functions.Tests.FunctionsTests.TimeDateTests;

/// <summary>
/// =daycountfraction(from, to, convention). A360/A365 are pure elapsed-day arithmetic; 30E360's
/// interesting cases are all at month boundaries, where the 30-day-month fiction diverges from
/// the actual calendar.
/// </summary>
[TestFixture]
public class DayCountFractionTests
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
            ""start"":       ""2025-01-01"",
            ""oneYearOn"":   ""2026-01-01"",
            ""halfYearOn"":  ""2025-07-01"",
            ""jan31"":       ""2025-01-31"",
            ""feb28"":       ""2025-02-28"",
            ""mar31"":       ""2025-03-31"",
            ""nullDate"":    null
        }");
    }

    private double Fraction(string fromPath, string toPath, string convention)
    {
        var fn = new DayCountFraction<JToken>();
        fn.SetArguments(new Arguments<JToken>
        {
            new PathValue<JToken>(fromPath),
            new PathValue<JToken>(toPath),
            Literal(convention)
        });
        var result = fn.Execute(data, data, context);
        Assert.That(result.Success, Is.True);
        return result.Data[0].ToObject<double>();
    }

    // ── A360 / A365 ───────────────────────────────────────────────────────────

    [Test]
    public void A360_OneCalendarYear_Is365Over360()
        => Assert.That(Fraction("$.start", "$.oneYearOn", "A360"), Is.EqualTo(365d / 360d).Within(1e-9));

    [Test]
    public void A365_OneCalendarYear_IsExactlyOne()
        => Assert.That(Fraction("$.start", "$.oneYearOn", "A365"), Is.EqualTo(1d).Within(1e-9));

    [Test]
    public void A365_HalfACommonYear_IsActualDaysOver365()
        => Assert.That(Fraction("$.start", "$.halfYearOn", "A365"), Is.EqualTo(181d / 365d).Within(1e-9));

    [Test]
    public void A360_ConventionNameIsCaseInsensitive()
        => Assert.That(Fraction("$.start", "$.oneYearOn", "a360"), Is.EqualTo(365d / 360d).Within(1e-9));

    // ── 30E/360 ───────────────────────────────────────────────────────────────

    [Test]
    public void ThirtyE360_OneCalendarYear_IsExactlyOne()
        => Assert.That(Fraction("$.start", "$.oneYearOn", "30E360"), Is.EqualTo(1d).Within(1e-9));

    /// <summary>Both endpoints cap at day 30, so the actual 31 days in January count as 30.</summary>
    [Test]
    public void ThirtyE360_JanuaryThirtyFirst_CapsAtDayThirty()
        => Assert.That(Fraction("$.start", "$.jan31", "30E360"), Is.EqualTo(29d / 360d).Within(1e-9));

    /// <summary>28 February is a genuine short month under 30/360 (not capped, since 28 &lt; 30).</summary>
    [Test]
    public void ThirtyE360_FebruaryTwentyEighth_IsNotCapped()
        => Assert.That(Fraction("$.start", "$.feb28", "30E360"), Is.EqualTo(57d / 360d).Within(1e-9));

    [Test]
    public void ThirtyE360_MarchThirtyFirst_CapsAtDayThirty()
        => Assert.That(Fraction("$.start", "$.mar31", "30E360"), Is.EqualTo(89d / 360d).Within(1e-9));

    // ── Convention validation ─────────────────────────────────────────────────

    [Test]
    public void UnknownConvention_FailsAndListsTheAcceptedConventions()
    {
        var fn = new DayCountFraction<JToken>();
        fn.SetArguments(new Arguments<JToken>
        {
            new PathValue<JToken>("$.start"),
            new PathValue<JToken>("$.oneYearOn"),
            Literal("actual/actual")
        });
        var result = fn.Execute(data, data, context);

        Assert.That(result.Success, Is.False);
        Assert.That(context.GetLogEntries().Any(e => e.Message.Contains("A360, A365, 30E360")), Is.True);
    }

    // ── Contract ──────────────────────────────────────────────────────────────

    [Test]
    public void TooFewArgs_ReturnsFailed()
    {
        var fn = new DayCountFraction<JToken>();
        fn.SetArguments(new Arguments<JToken>
        {
            new PathValue<JToken>("$.start"),
            new PathValue<JToken>("$.oneYearOn")
        });
        Assert.That(fn.Execute(data, data, context).Success, Is.False);
    }

    [Test]
    public void TooManyArgs_ReturnsFailed()
    {
        var fn = new DayCountFraction<JToken>();
        fn.SetArguments(new Arguments<JToken>
        {
            new PathValue<JToken>("$.start"),
            new PathValue<JToken>("$.oneYearOn"),
            Literal("A360"),
            Literal("extra")
        });
        Assert.That(fn.Execute(data, data, context).Success, Is.False);
    }

    [Test]
    public void PathNotFound_ReturnsFailed()
    {
        var fn = new DayCountFraction<JToken>();
        fn.SetArguments(new Arguments<JToken>
        {
            new PathValue<JToken>("$.missing"),
            new PathValue<JToken>("$.oneYearOn"),
            Literal("A360")
        });
        Assert.That(fn.Execute(data, data, context).Success, Is.False);
    }

    [Test]
    public void FoundButNull_ReturnsFailed()
    {
        var fn = new DayCountFraction<JToken>();
        fn.SetArguments(new Arguments<JToken>
        {
            new PathValue<JToken>("$.nullDate"),
            new PathValue<JToken>("$.oneYearOn"),
            Literal("A360")
        });
        Assert.That(fn.Execute(data, data, context).Success, Is.False);
    }
}
