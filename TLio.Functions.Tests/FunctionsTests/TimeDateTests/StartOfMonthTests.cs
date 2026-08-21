using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using NUnit.Framework;
using TLio.Core.Contracts;
using TLio.Core.Models;
using TLio.Extensions.TimeDate;
using TLio.Json;

namespace TLio.Functions.Tests.FunctionsTests.TimeDateTests;

/// <summary>=startofmonth(date) — the first day of that date's month, as a date-only string.</summary>
[TestFixture]
public class StartOfMonthTests
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
            ""mid"":      ""2024-02-17"",
            ""stamp"":    ""2024-02-17T13:45:00Z"",
            ""first"":    ""2024-02-01"",
            ""last"":     ""2024-01-31"",
            ""janFirst"": ""2025-01-01"",
            ""nullDate"": null
        }");
    }

    private string Start(string datePath)
    {
        var fn = new StartOfMonth<JToken>();
        fn.SetArguments(new Arguments<JToken> { new PathValue<JToken>(datePath) });
        var result = fn.Execute(data, data, context);
        Assert.That(result.Success, Is.True);
        return result.Data[0].ToObject<string>()!;
    }

    [Test]
    public void StartOfMonth_MidMonth_ReturnsTheFirst()
        => Assert.That(Start("$.mid"), Is.EqualTo("2024-02-01"));

    /// <summary>A timestamp answers with a day, not with midnight of that day.</summary>
    [Test]
    public void StartOfMonth_Timestamp_DropsTheTimeComponent()
        => Assert.That(Start("$.stamp"), Is.EqualTo("2024-02-01"));

    [Test]
    public void StartOfMonth_AlreadyTheFirst_IsIdempotent()
        => Assert.That(Start("$.first"), Is.EqualTo("2024-02-01"));

    [Test]
    public void StartOfMonth_LastDayOfAMonth_StaysInThatMonth()
        => Assert.That(Start("$.last"), Is.EqualTo("2024-01-01"));

    [Test]
    public void StartOfMonth_January_DoesNotRollIntoThePreviousYear()
        => Assert.That(Start("$.janFirst"), Is.EqualTo("2025-01-01"));

    // ── Contract ──────────────────────────────────────────────────────────────

    [Test]
    public void StartOfMonth_NoArgs_ReturnsFailed()
    {
        var fn = new StartOfMonth<JToken>();
        fn.SetArguments(new Arguments<JToken>());
        Assert.That(fn.Execute(data, data, context).Success, Is.False);
    }

    [Test]
    public void StartOfMonth_TooManyArgs_ReturnsFailed()
    {
        var fn = new StartOfMonth<JToken>();
        fn.SetArguments(new Arguments<JToken>
        {
            new PathValue<JToken>("$.mid"), new PathValue<JToken>("$.first")
        });
        Assert.That(fn.Execute(data, data, context).Success, Is.False);
    }

    [Test]
    public void StartOfMonth_PathNotFound_ReturnsFailed()
    {
        var fn = new StartOfMonth<JToken>();
        fn.SetArguments(new Arguments<JToken> { new PathValue<JToken>("$.missing") });
        Assert.That(fn.Execute(data, data, context).Success, Is.False);
    }

    [Test]
    public void StartOfMonth_FoundButNull_ReturnsFailed()
    {
        var fn = new StartOfMonth<JToken>();
        fn.SetArguments(new Arguments<JToken> { new PathValue<JToken>("$.nullDate") });
        Assert.That(fn.Execute(data, data, context).Success, Is.False);
    }

    /// <summary>
    /// There is no workaround for this today — a term boundary had to be assembled by string
    /// surgery. Paired with =endofmonth(...) it gives the whole month as two dates.
    /// </summary>
    [Test]
    public void StartOfMonth_PairedWithEndOfMonth_BracketsTheMonth()
    {
        var end = new EndOfMonth<JToken>();
        end.SetArguments(new Arguments<JToken> { new PathValue<JToken>("$.mid") });
        var endResult = end.Execute(data, data, context);

        Assert.That(Start("$.mid"), Is.EqualTo("2024-02-01"));
        Assert.That(endResult.Data[0].ToObject<string>(), Is.EqualTo("2024-02-29"));
    }
}
