using Newtonsoft.Json.Linq;
using NUnit.Framework;
using TLio.Client;
using TLio.Core.Contracts;
using TLio.Extensions.Math;
using TLio.Json;

namespace TLio.Functions.Tests.FunctionsTests.MathTests;

/// <summary>
/// The decimal separator contract, stated once and in one place.
///
/// **TLio supports '.' as the decimal separator, and only '.'.**
///
/// That is not a TLio preference — it is what the data formats mandate:
///   • JSON     — RFC 8259 §6: `decimal-point = %x2E ; .` A comma is not valid in a number,
///                and JSON has no locale mechanism at all.
///   • XML      — XML Schema xs:decimal / xs:double use '.' likewise.
///   • YAML 1.2 — the core schema's number production is JSON-compatible.
///
/// So a *native* number can never carry a comma in any format TLio reads; `{"n": 3,5}` is not
/// a parse of one number, it is a syntax error. The separator question only arises for numbers
/// carried as STRINGS — `{"n": "3,5"}` is valid JSON — and there the interpretation is entirely
/// application-defined, because nothing in the document says which locale wrote it.
///
/// TLio's current answer to that case is inconsistent, and both halves are pinned below.
/// </summary>
[TestFixture]
public class DecimalSeparatorTests
{
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

    private (bool Success, JToken? Out) Run(string expression, string document = "{}")
    {
        var script = $$"""[{"command":"put","path":"$.out","value":"{{expression}}"}]""";
        var r = _engine.Execute(script, JToken.Parse(document), _context);
        return (r.Success, r.Data.SelectToken("$.out"));
    }

    // ── Native numbers: '.' only, and that is the format's rule ───────────────

    [Test]
    public void ANativeJsonNumberUsesAPeriod()
    {
        var (success, output) = Run("=abs($.n)", """{"n":3.5}""");

        Assert.That(success, Is.True);
        Assert.That(output!.Value<double>(), Is.EqualTo(3.5));
    }

    [Test]
    public void ANumberWithACommaIsNotEvenValidJson()
    {
        // Not a TLio behaviour — a fact about the format. `{"n": 3,5}` is two things, not one
        // number, so the document never reaches TLio in the first place.
        Assert.That(() => JToken.Parse("""{"n": 3,5}"""), Throws.Exception,
            "RFC 8259 defines decimal-point as %x2E; a comma cannot appear inside a number");
    }

    [Test]
    public void NativeNumbersAreReadIdenticallyUnderACommaDecimalCulture()
    {
        var original = System.Threading.Thread.CurrentThread.CurrentCulture;
        try
        {
            System.Threading.Thread.CurrentThread.CurrentCulture =
                new System.Globalization.CultureInfo("nl-NL");

            var (success, output) = Run("=sum($.values)", """{"values":[1.5,2.25]}""");

            Assert.That(success, Is.True);
            Assert.That(output!.Value<double>(), Is.EqualTo(3.75),
                "the machine's locale must never change what a document means");
        }
        finally
        {
            System.Threading.Thread.CurrentThread.CurrentCulture = original;
        }
    }

    // ── Numbers carried as strings: where the ambiguity actually lives ────────

    [Test]
    public void APeriodDecimalStringIsParsed()
    {
        var (success, output) = Run("=abs($.n)", """{"n":"3.5"}""");

        Assert.That(success, Is.True);
        Assert.That(output!.Value<double>(), Is.EqualTo(3.5));
    }

    [Test]
    public void ACommaDecimalString_IsReadAsAThousandsSeparator_ByMathFunctions()
    {
        // "3,5" means three-point-five to a European author. TLio reads it as thirty-five,
        // because JsonNodeAdapter.TryGetDouble goes through Newtonsoft's Value<double?>(),
        // which uses Convert.ToDouble under the invariant culture — and that ACCEPTS ',' as a
        // thousands separator. A tenfold error, no warning.
        //
        // Note MathFunctionBase has a strict fallback (double.TryParse with NumberStyles.Float,
        // which rejects thousands separators) — but TryGetDouble succeeds first, so the strict
        // path is never reached.
        var (success, output) = Run("=abs($.n)", """{"n":"3,5"}""");

        Assert.That(success, Is.True);
        Assert.That(output!.Value<double>(), Is.EqualTo(35),
            "if this ever returns 3.5 or fails, the separator rule has been decided deliberately");
    }

    [Test]
    public void ACommaDecimalString_IsRejectedByCalculate()
    {
        // The same notation, the same pack, the opposite answer: =calculate hands the
        // expression to DataTable.Compute, which treats ',' as an argument separator and
        // reports a syntax error. Two functions disagree about what "2,5" means.
        var (success, output) = Run("=calculate('2,5+3,7')");

        Assert.That(success, Is.False);
        Assert.That(output, Is.Null);
        Assert.That(_context.GetLogEntries().Any(e => e.Message.Contains("Syntax error")), Is.True);
    }

    [Test]
    public void ThousandsGroupingInAStringIsAccepted()
    {
        // The flip side of the same rule, and the reading that is presumably intended.
        var (success, output) = Run("=abs($.n)", """{"n":"1,234"}""");

        Assert.That(success, Is.True);
        Assert.That(output!.Value<double>(), Is.EqualTo(1234));
    }

    [Test]
    public void AStringNumberIsParsedInvariantly_NotByTheAmbientCulture()
    {
        // Whatever the comma rule ends up being, it must not depend on the machine.
        var original = System.Threading.Thread.CurrentThread.CurrentCulture;
        try
        {
            System.Threading.Thread.CurrentThread.CurrentCulture =
                new System.Globalization.CultureInfo("nl-NL");

            var (success, output) = Run("=abs($.n)", """{"n":"3.5"}""");

            Assert.That(success, Is.True);
            Assert.That(output!.Value<double>(), Is.EqualTo(3.5),
                "under nl-NL a culture-aware parse would read '3.5' as 35 — it must not");
        }
        finally
        {
            System.Threading.Thread.CurrentThread.CurrentCulture = original;
        }
    }
}
