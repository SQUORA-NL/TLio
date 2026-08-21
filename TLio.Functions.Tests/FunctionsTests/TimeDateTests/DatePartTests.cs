using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using NUnit.Framework;
using TLio.Core.Contracts;
using TLio.Core.Models;
using TLio.Extensions.TimeDate;
using TLio.Json;

namespace TLio.Functions.Tests.FunctionsTests.TimeDateTests;

/// <summary>
/// =datepart(date, part). Two parts are not their .NET namesakes — dayofweek is ISO
/// (1 = Monday) and weekofyear is the ISO 8601 week — so both get a boundary test.
/// </summary>
[TestFixture]
public class DatePartTests
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
            ""stamp"":       ""2026-08-21T14:35:09Z"",
            ""monday"":      ""2024-03-11"",
            ""sunday"":      ""2024-03-17"",
            ""isoEdge"":     ""2021-01-01"",
            ""isoEdgeBack"": ""2019-12-30"",
            ""marchFirst"":  ""2024-03-01"",
            ""feb2024"":     ""2024-02-10"",
            ""feb2023"":     ""2023-02-10"",
            ""startDate"":   ""2026-09-01"",
            ""nullDate"":    null
        }");
    }

    private long Part(string datePath, string part)
    {
        var fn = new DatePart<JToken>();
        fn.SetArguments(new Arguments<JToken> { new PathValue<JToken>(datePath), Literal(part) });
        var result = fn.Execute(data, data, context);
        Assert.That(result.Success, Is.True);
        return result.Data[0].ToObject<long>();
    }

    // ── The plain components ──────────────────────────────────────────────────

    [TestCase("year", 2026)]
    [TestCase("month", 8)]
    [TestCase("day", 21)]
    [TestCase("hour", 14)]
    [TestCase("minute", 35)]
    [TestCase("second", 9)]
    [TestCase("quarter", 3)]
    public void DatePart_Component_IsExtracted(string part, int expected)
        => Assert.That(Part("$.stamp", part), Is.EqualTo(expected));

    [Test]
    public void DatePart_PartName_IsCaseInsensitive()
        => Assert.That(Part("$.stamp", "DayOfWeek"), Is.EqualTo(Part("$.stamp", "dayofweek")));

    // ── ISO day of week ───────────────────────────────────────────────────────

    [Test]
    public void DatePart_DayOfWeek_MondayIsOne()
        => Assert.That(Part("$.monday", "dayofweek"), Is.EqualTo(1));

    /// <summary>.NET's DayOfWeek would answer 0 here. ISO says 7, and that is what a rule means.</summary>
    [Test]
    public void DatePart_DayOfWeek_SundayIsSevenNotZero()
        => Assert.That(Part("$.sunday", "dayofweek"), Is.EqualTo(7));

    // ── ISO week of year ──────────────────────────────────────────────────────

    /// <summary>1 January 2021 is a Friday, so ISO 8601 puts it in week 53 of 2020.</summary>
    [Test]
    public void DatePart_WeekOfYear_JanuaryFirstCanBelongToThePreviousYearsLastWeek()
        => Assert.That(Part("$.isoEdge", "weekofyear"), Is.EqualTo(53));

    /// <summary>30 December 2019 is a Monday, and ISO 8601 puts it in week 1 of 2020.</summary>
    [Test]
    public void DatePart_WeekOfYear_LateDecemberCanBelongToTheNextYearsFirstWeek()
        => Assert.That(Part("$.isoEdgeBack", "weekofyear"), Is.EqualTo(1));

    // ── Derived counts ────────────────────────────────────────────────────────

    [Test]
    public void DatePart_DayOfYear_CountsTheLeapDay()
        => Assert.That(Part("$.marchFirst", "dayofyear"), Is.EqualTo(61));

    [Test]
    public void DatePart_DaysInMonth_IsLeapYearCorrect()
    {
        Assert.That(Part("$.feb2024", "daysinmonth"), Is.EqualTo(29));
        Assert.That(Part("$.feb2023", "daysinmonth"), Is.EqualTo(28));
    }

    [Test]
    public void DatePart_DateOnlyValue_HasAZeroTime()
    {
        Assert.That(Part("$.monday", "hour"), Is.EqualTo(0));
        Assert.That(Part("$.monday", "minute"), Is.EqualTo(0));
        Assert.That(Part("$.monday", "second"), Is.EqualTo(0));
    }

    // ── Contract ──────────────────────────────────────────────────────────────

    [Test]
    public void DatePart_UnknownPart_FailsAndListsTheAcceptedParts()
    {
        var fn = new DatePart<JToken>();
        fn.SetArguments(new Arguments<JToken>
        {
            new PathValue<JToken>("$.stamp"), Literal("fortnight")
        });
        var result = fn.Execute(data, data, context);

        Assert.That(result.Success, Is.False);
        Assert.That(context.GetLogEntries().Any(e => e.Message.Contains("weekofyear")), Is.True);
    }

    [Test]
    public void DatePart_TooFewArgs_ReturnsFailed()
    {
        var fn = new DatePart<JToken>();
        fn.SetArguments(new Arguments<JToken> { new PathValue<JToken>("$.stamp") });
        Assert.That(fn.Execute(data, data, context).Success, Is.False);
    }

    [Test]
    public void DatePart_TooManyArgs_ReturnsFailed()
    {
        var fn = new DatePart<JToken>();
        fn.SetArguments(new Arguments<JToken>
        {
            new PathValue<JToken>("$.stamp"), Literal("year"), Literal("extra")
        });
        Assert.That(fn.Execute(data, data, context).Success, Is.False);
    }

    [Test]
    public void DatePart_PathNotFound_ReturnsFailed()
    {
        var fn = new DatePart<JToken>();
        fn.SetArguments(new Arguments<JToken> { new PathValue<JToken>("$.missing"), Literal("year") });
        Assert.That(fn.Execute(data, data, context).Success, Is.False);
    }

    [Test]
    public void DatePart_FoundButNull_ReturnsFailed()
    {
        var fn = new DatePart<JToken>();
        fn.SetArguments(new Arguments<JToken> { new PathValue<JToken>("$.nullDate"), Literal("year") });
        Assert.That(fn.Execute(data, data, context).Success, Is.False);
    }

    // ── The idiom it replaces ─────────────────────────────────────────────────

    /// <summary>
    /// The samples read the year out of a date with =substring($.date,0,4), which only works
    /// while the string happens to be ISO and four digits wide.
    /// </summary>
    [Test]
    public void DatePart_Year_MatchesTheSubstringIdiomFromTheSamples()
        => Assert.That(Part("$.startDate", "year"), Is.EqualTo(2026));
}
