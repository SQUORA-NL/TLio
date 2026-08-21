using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using NUnit.Framework;
using TLio.Core.Contracts;
using TLio.Core.Models;
using TLio.Extensions.TimeDate;
using TLio.Json;

namespace TLio.Functions.Tests.FunctionsTests.TimeDateTests;

/// <summary>
/// =formatdate(date, format). The class is FormatDateFunction; the registered name is
/// "formatdate", which the first test pins.
/// </summary>
[TestFixture]
public class FormatDateTests
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
            ""quotedOn"": ""2026-08-21"",
            ""stamp"":    ""2024-03-15T14:05:09Z"",
            ""offset"":   ""2024-03-15T23:30:00+05:00"",
            ""fmt"":      ""dd-MM-yyyy"",
            ""nullDate"": null
        }");
    }

    private string Format(string datePath, string format)
    {
        var fn = new FormatDateFunction<JToken>();
        fn.SetArguments(new Arguments<JToken> { new PathValue<JToken>(datePath), Literal(format) });
        var result = fn.Execute(data, data, context);
        Assert.That(result.Success, Is.True);
        return result.Data[0].ToObject<string>()!;
    }

    [Test]
    public void FormatDate_RegistersUnderItsScriptName()
        => Assert.That(new FormatDateFunction<JToken>().FunctionName, Is.EqualTo("formatdate"));

    // ── Rendering ─────────────────────────────────────────────────────────────

    [Test]
    public void FormatDate_DutchNotation_IsRendered()
        => Assert.That(Format("$.quotedOn", "dd-MM-yyyy"), Is.EqualTo("21-08-2026"));

    [Test]
    public void FormatDate_YearOnly_IsRendered()
        => Assert.That(Format("$.quotedOn", "yyyy"), Is.EqualTo("2026"));

    [Test]
    public void FormatDate_TimeComponent_IsRendered()
        => Assert.That(Format("$.stamp", "HH:mm"), Is.EqualTo("14:05"));

    /// <summary>The format runs against the UTC value, so an offset input is normalised first.</summary>
    [Test]
    public void FormatDate_OffsetInput_IsFormattedAsUtc()
        => Assert.That(Format("$.offset", "yyyy-MM-dd HH:mm"), Is.EqualTo("2024-03-15 18:30"));

    [Test]
    public void FormatDate_IsInvariantCultureRegardlessOfTheMachine()
    {
        var previous = System.Threading.Thread.CurrentThread.CurrentCulture;
        try
        {
            System.Threading.Thread.CurrentThread.CurrentCulture =
                new System.Globalization.CultureInfo("nl-NL");
            Assert.That(Format("$.quotedOn", "MMMM"), Is.EqualTo("August"));
        }
        finally
        {
            System.Threading.Thread.CurrentThread.CurrentCulture = previous;
        }
    }

    [Test]
    public void FormatDate_FormatFromAPath_IsResolved()
    {
        var fn = new FormatDateFunction<JToken>();
        fn.SetArguments(new Arguments<JToken>
        {
            new PathValue<JToken>("$.quotedOn"), new PathValue<JToken>("$.fmt")
        });
        var result = fn.Execute(data, data, context);
        Assert.That(result.Success, Is.True);
        Assert.That(result.Data[0].ToObject<string>(), Is.EqualTo("21-08-2026"));
    }

    // ── Contract ──────────────────────────────────────────────────────────────

    [Test]
    public void FormatDate_InvalidFormatString_ReturnsFailed()
    {
        var fn = new FormatDateFunction<JToken>();
        fn.SetArguments(new Arguments<JToken>
        {
            new PathValue<JToken>("$.quotedOn"), Literal("yyyy-MM-dd'")
        });
        Assert.That(fn.Execute(data, data, context).Success, Is.False);
    }

    [Test]
    public void FormatDate_EmptyFormatString_ReturnsFailed()
    {
        var fn = new FormatDateFunction<JToken>();
        fn.SetArguments(new Arguments<JToken>
        {
            new PathValue<JToken>("$.quotedOn"), Literal("")
        });
        Assert.That(fn.Execute(data, data, context).Success, Is.False);
    }

    [Test]
    public void FormatDate_TooFewArgs_ReturnsFailed()
    {
        var fn = new FormatDateFunction<JToken>();
        fn.SetArguments(new Arguments<JToken> { new PathValue<JToken>("$.quotedOn") });
        Assert.That(fn.Execute(data, data, context).Success, Is.False);
    }

    [Test]
    public void FormatDate_TooManyArgs_ReturnsFailed()
    {
        var fn = new FormatDateFunction<JToken>();
        fn.SetArguments(new Arguments<JToken>
        {
            new PathValue<JToken>("$.quotedOn"), Literal("yyyy"), Literal("extra")
        });
        Assert.That(fn.Execute(data, data, context).Success, Is.False);
    }

    [Test]
    public void FormatDate_PathNotFound_ReturnsFailed()
    {
        var fn = new FormatDateFunction<JToken>();
        fn.SetArguments(new Arguments<JToken> { new PathValue<JToken>("$.missing"), Literal("yyyy") });
        Assert.That(fn.Execute(data, data, context).Success, Is.False);
    }

    [Test]
    public void FormatDate_FoundButNull_ReturnsFailed()
    {
        var fn = new FormatDateFunction<JToken>();
        fn.SetArguments(new Arguments<JToken> { new PathValue<JToken>("$.nullDate"), Literal("yyyy") });
        Assert.That(fn.Execute(data, data, context).Success, Is.False);
    }

    // ── The idiom it replaces ─────────────────────────────────────────────────

    /// <summary>
    /// The samples strip the dashes out of a date with =replace($.date,'-','') to make a
    /// comparable YYYYMMDD number. A format string says that in one step.
    /// </summary>
    [Test]
    public void FormatDate_Compact_MatchesTheReplaceIdiomFromTheSamples()
        => Assert.That(Format("$.quotedOn", "yyyyMMdd"), Is.EqualTo("20260821"));
}
