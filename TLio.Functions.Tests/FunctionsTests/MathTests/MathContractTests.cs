using Microsoft.Extensions.Logging;
using Newtonsoft.Json.Linq;
using NUnit.Framework;
using TLio.Client;
using TLio.Core.Contracts;
using TLio.Extensions.Math;
using TLio.Json;

namespace TLio.Functions.Tests.FunctionsTests.MathTests;

/// <summary>
/// The null / missing / non-numeric contract that every math function inherits from
/// MathFunctionBase, exercised across the whole pack rather than one function at a time.
///
/// The contract has three distinct outcomes and they must not blur into each other:
///   • path not found      → error, the function fails
///   • found but null      → treated as 0, the function succeeds
///   • found but not numeric → error, the function fails (unless it parses as a number)
///
/// Getting these wrong is how a silent 0 ends up in a total that should have failed loudly.
/// </summary>
[TestFixture]
public class MathContractTests
{
    /// <summary>Aggregates take a set of values; scalars take exactly one.</summary>
    private static readonly string[] Aggregates = { "sum", "avg", "min", "max", "median", "count" };

    /// <summary>Every aggregate except count, which answers 0 for a missing path instead.</summary>
    private static readonly string[] FailingAggregates = { "sum", "avg", "min", "max", "median" };
    private static readonly string[] Scalars = { "abs", "ceiling", "floor", "round", "sqrt" };

    private IExecutionContext<JToken> _context = null!;
    private ScriptEngine<JToken> _engine = null!;

    [SetUp]
    public void SetUp()
    {
        _context = JsonExecutionContext.CreateDefault();
        var options = ParseOptions<JToken>.CreateDefault();
        options.FunctionsProvider.RegisterMath<JToken>();
        _engine = new ScriptEngine<JToken>(options.CommandsProvider, options.FunctionsProvider);
    }

    private (bool Success, JToken? Out) Run(string expression, string document)
    {
        var script = $$"""[{"command":"put","path":"$.out","value":"{{expression}}"}]""";
        var r = _engine.Execute(script, JToken.Parse(document), _context);
        return (r.Success, r.Data.SelectToken("$.out"));
    }

    private bool LoggedError() =>
        _context.GetLogEntries().Any(e => e.Level >= LogLevel.Warning);

    // ── Path not found → failure, for every function ──────────────────────────

    [TestCaseSource(nameof(FailingAggregates))]
    [TestCaseSource(nameof(Scalars))]
    public void PathNotFound_FailsAndSaysSo(string function)
    {
        var (success, output) = Run($"={function}($.nowhere)", """{"a":1}""");

        Assert.That(success, Is.False, $"{function} must not invent a value for a missing path");
        Assert.That(output, Is.Null);
        Assert.That(_context.GetLogEntries().Any(e => e.Message.Contains("path not found")), Is.True,
            "the message distinguishes a missing path from a bad value");
    }

    // ── Found-but-null → treated as zero, for every function ──────────────────

    [TestCaseSource(nameof(Scalars))]
    public void ExplicitNull_IsTreatedAsZero_ForScalarFunctions(string function)
    {
        var (success, output) = Run($"={function}($.n)", """{"n":null}""");

        Assert.That(success, Is.True, $"{function} treats a present-but-null value as 0");
        Assert.That(output!.Value<double>(), Is.EqualTo(0));
        Assert.That(LoggedError(), Is.False, "a null is expected input, not a problem to report");
    }

    [Test]
    public void ExplicitNull_ContributesZeroToASum()
    {
        var (success, output) = Run("=sum($.values)", """{"values":[1,null,2]}""");

        Assert.That(success, Is.True);
        Assert.That(output!.Value<double>(), Is.EqualTo(3), "the null counted as 0, it did not abort");
    }

    [Test]
    public void ExplicitNull_StillCountsTowardsAnAverage()
    {
        // Consequence worth pinning: null-as-zero drags the mean down rather than being skipped.
        var (success, output) = Run("=avg($.values)", """{"values":[3,null,3]}""");

        Assert.That(success, Is.True);
        Assert.That(output!.Value<double>(), Is.EqualTo(2),
            "3 values summing to 6 — null is a zero, not an omission");
    }

    // ── Non-numeric → failure ─────────────────────────────────────────────────

    [TestCaseSource(nameof(Scalars))]
    public void NonNumericText_Fails(string function)
    {
        var (success, _) = Run($"={function}($.n)", """{"n":"banana"}""");

        Assert.That(success, Is.False, $"{function} must not coerce nonsense to a number");
        Assert.That(_context.GetLogEntries().Any(e => e.Message.Contains("non-numeric")), Is.True);
    }

    [Test]
    public void NonNumericInsideAnArray_FailsTheWholeAggregate()
    {
        var (success, _) = Run("=sum($.values)", """{"values":[1,"banana",2]}""");

        Assert.That(success, Is.False, "a partial total would be worse than no total");
    }

    [Test]
    public void CountAnsersZeroForAMissingPath_UnlikeEveryOtherAggregate()
    {
        // Documented, not endorsed. sum/avg/min/max/median all fail on a path that matches
        // nothing; count returns 0 instead. "0 items" and "I could not find that" are
        // different answers, and a caller cannot tell them apart.
        var (success, output) = Run("=count($.nowhere)", """{"a":1}""");

        Assert.That(success, Is.True);
        Assert.That(output!.Value<double>(), Is.EqualTo(0),
            "if this ever fails instead, count has been aligned with the other aggregates");
    }

    [Test]
    public void ABooleanIsSilentlyCountedAsOne()
    {
        // Documented, not endorsed: true coerces to 1 rather than being rejected as
        // non-numeric, so a boolean stirred into a total shifts it without any warning.
        var (success, output) = Run("=sum($.values)", """{"values":[1,true]}""");

        Assert.That(success, Is.True);
        Assert.That(output!.Value<double>(), Is.EqualTo(2),
            "if this ever fails instead, booleans have been excluded from numeric coercion");
    }

    [Test]
    public void AnObjectIsNotANumber()
    {
        var (success, _) = Run("=sum($.values)", """{"values":[{"a":1}]}""");

        Assert.That(success, Is.False);
    }

    // ── Numeric strings are parsed, culture-independently ─────────────────────

    [TestCase("\\\"42\\\"", 42)]
    [TestCase("\\\"3.5\\\"", 3.5)]
    [TestCase("\\\"-7\\\"", -7)]
    public void ANumericStringIsAccepted(string json, double expected)
    {
        var (success, output) = Run("=abs($.n)", $$"""{"n":{{json.Replace("\\\"", "\"")}}}""");

        Assert.That(success, Is.True, "a number that arrived as text is still a number");
        Assert.That(output!.Value<double>(), Is.EqualTo(System.Math.Abs(expected)));
    }

    [Test]
    public void ANumericStringIsParsedRegardlessOfTheAmbientCulture()
    {
        var original = System.Threading.Thread.CurrentThread.CurrentCulture;
        try
        {
            System.Threading.Thread.CurrentThread.CurrentCulture =
                new System.Globalization.CultureInfo("nl-NL");

            var (success, output) = Run("=abs($.n)", """{"n":"3.5"}""");

            Assert.That(success, Is.True);
            Assert.That(output!.Value<double>(), Is.EqualTo(3.5),
                "invariant parsing — the same document must not mean different numbers per machine");
        }
        finally
        {
            System.Threading.Thread.CurrentThread.CurrentCulture = original;
        }
    }

    [Test]
    public void ACommaDecimalString_IsReadAsThousandsSeparatedAndBecomesThirtyFive()
    {
        // Documented, and the sharpest edge in this fixture: "3,5" — three-point-five to a
        // European author — is read as a thousands-separated 35. A tenfold error, silent,
        // from data that looks perfectly reasonable. Compare =calculate, which rejects the
        // same notation outright.
        var (success, output) = Run("=abs($.n)", """{"n":"3,5"}""");

        Assert.That(success, Is.True);
        Assert.That(output!.Value<double>(), Is.EqualTo(35),
            "if this ever returns 3.5 or fails, comma handling has been decided on deliberately");
    }

    // ── Empty input ───────────────────────────────────────────────────────────

    [Test]
    public void AnEmptyArray_SumsToZero()
    {
        var (success, output) = Run("=sum($.values)", """{"values":[]}""");

        Assert.That(success, Is.True);
        Assert.That(output!.Value<double>(), Is.EqualTo(0));
    }

    [Test]
    public void AnEmptyArray_CountsAsZero()
    {
        var (success, output) = Run("=count($.values)", """{"values":[]}""");

        Assert.That(success, Is.True);
        Assert.That(output!.Value<double>(), Is.EqualTo(0));
    }

    // ── Whole-number output contract ──────────────────────────────────────────

    [TestCase("=sum($.values)", "{\"values\":[1,2,3]}", JTokenType.Integer)]
    [TestCase("=avg($.values)", "{\"values\":[2,4]}", JTokenType.Integer)]
    [TestCase("=avg($.values)", "{\"values\":[1,2]}", JTokenType.Float)]
    [TestCase("=sqrt($.n)", "{\"n\":9}", JTokenType.Integer)]
    [TestCase("=sqrt($.n)", "{\"n\":2}", JTokenType.Float)]
    public void WholeResultsAreIntegers(string expression, string document, JTokenType expected)
    {
        var (success, output) = Run(expression, document);

        Assert.That(success, Is.True);
        Assert.That(output!.Type, Is.EqualTo(expected),
            "downstream type checks depend on 4.0 arriving as 4, not 4.0");
    }

    // ── Nested arrays flatten ─────────────────────────────────────────────────

    [Test]
    public void NestedArraysAreFlattenedIntoTheAggregate()
    {
        var (success, output) = Run("=sum($.values)", """{"values":[1,[2,3],[[4]]]}""");

        Assert.That(success, Is.True);
        Assert.That(output!.Value<double>(), Is.EqualTo(10));
    }

    // ── A failed math function stops the script ───────────────────────────────

    [Test]
    public void AFailedMathFunction_AbortsTheRestOfTheScript()
    {
        // Same rule as every other function failure, and different from a path that simply
        // matches nothing: the command is marked failed and TLioScript stops there.
        var script = """
            [
              {"command":"put","path":"$.out","value":"=sum($.nowhere)"},
              {"command":"put","path":"$.after","value":"ran"}
            ]
            """;
        var r = _engine.Execute(script, JToken.Parse("""{"a":1}"""), _context);

        Assert.That(r.Success, Is.False);
        Assert.That(r.Data.SelectToken("$.after"), Is.Null);
    }
}
