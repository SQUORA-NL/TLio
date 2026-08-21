using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using NUnit.Framework;
using TLio.Core.Contracts;
using TLio.Core.Models;
using TLio.Extensions.TimeDate;
using TLio.Json;

namespace TLio.Functions.Tests.FunctionsTests.TimeDateTests;

/// <summary>
/// =parsedate(text, format?). The point of the function is normalisation: whatever goes in,
/// a canonical ISO string comes out.
/// </summary>
[TestFixture]
public class ParseDateTests
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
            ""dutch"":    ""31-12-2026"",
            ""us"":       ""12/25/2026"",
            ""compact"":  ""20260821"",
            ""withTime"": ""2024-03-15 14:05"",
            ""iso"":      ""2026-08-21"",
            ""junk"":     ""not-a-date"",
            ""nullText"": null
        }");
    }

    private string Parse(string textPath, string? format = null)
    {
        var fn = new ParseDateFunction<JToken>();
        var args = new Arguments<JToken> { new PathValue<JToken>(textPath) };
        if (format != null) args.Add(Literal(format));
        fn.SetArguments(args);
        var result = fn.Execute(data, data, context);
        Assert.That(result.Success, Is.True);
        return result.Data[0].ToObject<string>()!;
    }

    [Test]
    public void ParseDate_RegistersUnderItsScriptName()
        => Assert.That(new ParseDateFunction<JToken>().FunctionName, Is.EqualTo("parsedate"));

    // ── With an explicit format ───────────────────────────────────────────────

    [Test]
    public void ParseDate_DutchNotation_BecomesIso()
        => Assert.That(Parse("$.dutch", "dd-MM-yyyy"), Is.EqualTo("2026-12-31"));

    [Test]
    public void ParseDate_CompactNotation_BecomesIso()
        => Assert.That(Parse("$.compact", "yyyyMMdd"), Is.EqualTo("2026-08-21"));

    [Test]
    public void ParseDate_TextWithATime_KeepsTheTimeComponent()
        => Assert.That(Parse("$.withTime", "yyyy-MM-dd HH:mm"), Is.EqualTo("2024-03-15T14:05:00Z"));

    // ── Without a format ──────────────────────────────────────────────────────

    [Test]
    public void ParseDate_NoFormat_UsesTheSharedFormatList()
        => Assert.That(Parse("$.dutch"), Is.EqualTo("2026-12-31"));

    [Test]
    public void ParseDate_NoFormat_ReadsUsSlashNotation()
        => Assert.That(Parse("$.us"), Is.EqualTo("2026-12-25"));

    [Test]
    public void ParseDate_AlreadyIso_RoundTripsUnchanged()
        => Assert.That(Parse("$.iso"), Is.EqualTo("2026-08-21"));

    /// <summary>A date-only value must not gain a midnight time on the way through.</summary>
    [Test]
    public void ParseDate_DateOnly_StaysDateOnly()
        => Assert.That(Parse("$.dutch", "dd-MM-yyyy"), Does.Not.Contain("T"));

    // ── Contract ──────────────────────────────────────────────────────────────

    [Test]
    public void ParseDate_TextThatDoesNotMatchTheFormat_ReturnsFailed()
    {
        var fn = new ParseDateFunction<JToken>();
        fn.SetArguments(new Arguments<JToken>
        {
            new PathValue<JToken>("$.dutch"), Literal("MM/dd/yyyy")
        });
        Assert.That(fn.Execute(data, data, context).Success, Is.False);
    }

    [Test]
    public void ParseDate_UnparseableText_ReturnsFailed()
    {
        var fn = new ParseDateFunction<JToken>();
        fn.SetArguments(new Arguments<JToken> { new PathValue<JToken>("$.junk") });
        Assert.That(fn.Execute(data, data, context).Success, Is.False);
    }

    [Test]
    public void ParseDate_NoArgs_ReturnsFailed()
    {
        var fn = new ParseDateFunction<JToken>();
        fn.SetArguments(new Arguments<JToken>());
        Assert.That(fn.Execute(data, data, context).Success, Is.False);
    }

    [Test]
    public void ParseDate_TooManyArgs_ReturnsFailed()
    {
        var fn = new ParseDateFunction<JToken>();
        fn.SetArguments(new Arguments<JToken>
        {
            new PathValue<JToken>("$.dutch"), Literal("dd-MM-yyyy"), Literal("extra")
        });
        Assert.That(fn.Execute(data, data, context).Success, Is.False);
    }

    [Test]
    public void ParseDate_PathNotFound_ReturnsFailed()
    {
        var fn = new ParseDateFunction<JToken>();
        fn.SetArguments(new Arguments<JToken> { new PathValue<JToken>("$.missing") });
        Assert.That(fn.Execute(data, data, context).Success, Is.False);
    }

    [Test]
    public void ParseDate_FoundButNull_ReturnsFailed()
    {
        var fn = new ParseDateFunction<JToken>();
        fn.SetArguments(new Arguments<JToken> { new PathValue<JToken>("$.nullText") });
        Assert.That(fn.Execute(data, data, context).Success, Is.False);
    }

    // ── The idiom it replaces ─────────────────────────────────────────────────

    /// <summary>
    /// A non-ISO input can only be normalised today with split/concat surgery, and every date
    /// function downstream needs the ISO form. One call now does it, and the result feeds
    /// =datediff(...) directly.
    /// </summary>
    [Test]
    public void ParseDate_NormalisedResult_IsUsableByTheOtherDateFunctions()
    {
        var normalised = Parse("$.dutch", "dd-MM-yyyy");
        var diff = new DateDiff<JToken>();
        diff.SetArguments(new Arguments<JToken>
        {
            Literal(normalised), new PathValue<JToken>("$.iso"), Literal("months")
        });
        var result = diff.Execute(data, data, context);

        Assert.That(result.Success, Is.True);
        Assert.That(result.Data[0].ToObject<long>(), Is.EqualTo(-4));
    }
}
