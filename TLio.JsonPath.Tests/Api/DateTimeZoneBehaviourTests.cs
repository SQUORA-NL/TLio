using NUnit.Framework;
using TLio.JsonPath.Tests.Differential;

namespace TLio.JsonPath.Tests.Api;

/// <summary>
/// What the Newtonsoft dialect's date emulation does and does not depend on the machine's time zone. The README
/// states this precisely, because a query that behaves differently on a laptop and in a container is a surprise worth
/// naming. Newtonsoft.Json itself behaves the same way; these tests hold the engine to that, and hold the README to the facts.
/// </summary>
[TestFixture]
public class DateTimeZoneBehaviourTests
{
    private static readonly JsonPathEngine Default = new(new JsonPathOptions { Dialect = JsonPathDialect.Newtonsoft });
    private static readonly JsonPathEngine NoDates = new(new JsonPathOptions { Dialect = JsonPathDialect.Newtonsoft, EmulateNewtonsoftDates = false });
    private static readonly JsonPathEngine Rfc = new(new JsonPathOptions { Dialect = JsonPathDialect.Rfc9535 });

    private static string Run(JsonPathEngine e, string query, string doc) =>
        string.Join(",", EngineRunner.SelectTokens(e, doc, query).Hits!.Select(h => h.Path));

    // Equality is decided by Newtonsoft's ISO writer, which writes 'Z' for UTC and nothing for a zone-less date:
    // no time zone is consulted, so these give the same answer in every time zone. The suite is run under several
    // (see the CI matrix and the README) and the expectations below are fixed.
    [TestCase("2020-01-01T00:00:00Z", "2020-01-01T00:00:00Z", "$[0]")]
    [TestCase("2020-01-01T00:00:00.50Z", "2020-01-01T00:00:00.50Z", "")]   // written back as .5Z, so text '…50Z' does not match
    [TestCase("2020-01-01T00:00:00.5Z", "2020-01-01T00:00:00.5Z", "$[0]")]
    [TestCase("2020-06-15T12:30:45", "2020-06-15T12:30:45", "$[0]")]
    public void Equality_on_UTC_and_zone_less_dates_does_not_depend_on_the_time_zone(string stored, string literal, string expected)
    {
        var doc = $$"""[{"d":"{{stored}}"}]""";
        Assert.That(Run(Default, $"$[?(@.d == '{literal}')]", doc), Is.EqualTo(expected));
        Assert.That(NewtonsoftOracle.SelectTokens(doc, $"$[?(@.d == '{literal}')]").Hits!.Select(h => h.Path), Is.EqualTo(expected == "" ? [] : expected.Split(',')));
    }

    [Test]
    public void Ordering_a_date_against_a_string_is_done_in_local_time_exactly_as_Newtonsoft_does()
    {
        // Newtonsoft converts the string with Convert.ToDateTime, which yields *local* time for a 'Z' string, then compares
        // ticks with the stored UTC date. In UTC the two agree; elsewhere they are offset. The engine must give Newtonsoft's
        // answer on whatever machine it runs on — so assert agreement with the oracle, not a fixed value.
        var doc = """[{"d":"2020-01-01T04:00:00Z"}]""";
        foreach (var op in new[] { "<", "<=", ">", ">=" })
        {
            var q = $"$[?(@.d {op} '2020-01-01T03:00:00Z')]";
            Assert.That(Run(Default, q, doc), Is.EqualTo(string.Join(",", NewtonsoftOracle.SelectTokens(doc, q).Hits!.Select(h => h.Path))), q);
        }

        TestContext.Out.WriteLine($"time zone here: {TimeZoneInfo.Local.Id}; '<' selected: '{Run(Default, "$[?(@.d < '2020-01-01T03:00:00Z')]", doc)}'");
    }

    [Test]
    public void Turning_emulation_off_makes_every_date_comparison_machine_independent()
    {
        var doc = """[{"d":"2020-01-01T04:00:00Z"}]""";
        Assert.That(Run(NoDates, "$[?(@.d < '2020-01-01T05:00:00Z')]", doc), Is.EqualTo("$[0]"));   // plain string order
        Assert.That(Run(NoDates, "$[?(@.d == '2020-01-01T04:00:00Z')]", doc), Is.EqualTo("$[0]"));
    }

    [Test]
    public void The_RFC_dialect_never_treats_a_string_as_a_date()
    {
        var doc = """[{"d":"2020-01-01T00:00:00.50Z"}]""";
        Assert.That(Run(Rfc, "$[?(@.d == '2020-01-01T00:00:00.50Z')]", doc), Is.EqualTo("$[0]"));
    }
}
