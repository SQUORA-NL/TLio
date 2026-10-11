using Microsoft.Extensions.Logging;
using Newtonsoft.Json.Linq;
using NUnit.Framework;
using TLio.Client;
using TLio.Core.Contracts;
using TLio.Extensions.Math;
using TLio.Extensions.Text;
using TLio.Json;

namespace TLio.Functions.Tests.FunctionsTests.MathTests;

/// <summary>
/// Infix arithmetic in function values: <c>=round($.principal * $.rate * $.yearFraction, 2)</c>.
///
/// The parser turns the expression into the existing sum / subtract / multiply / divide / modulo /
/// pow calls, so these tests pin the grammar (precedence, associativity, the whitespace rule) and
/// the fallbacks — not the arithmetic itself, which the per-function fixtures already cover.
/// </summary>
[TestFixture]
public class InfixExpressionTests
{
    private const string Data = """
        { "principal": 1000, "rate": 0.05, "yearFraction": 0.5, "a": 6, "b": 4,
          "unit-price": 5, "first": "Sanne", "last": "Berg", "x": 2.345 }
        """;

    private IExecutionContext<JToken> _context = null!;
    private ScriptEngine<JToken> _engine = null!;

    [SetUp]
    public void SetUp()
    {
        _context = JsonExecutionContext.CreateDefault();
        var options = ParseOptions<JToken>.CreateDefault();
        options.FunctionsProvider.RegisterMath<JToken>();
        options.FunctionsProvider.RegisterText<JToken>();
        _engine = new ScriptEngine<JToken>(options.CommandsProvider, options.FunctionsProvider);
    }

    private JToken? Run(string expression)
    {
        var script = $$"""[{"command":"put","path":"$.out","value":"{{expression.Replace("\"", "\\\"")}}"}]""";
        var result = _engine.Execute(script, JToken.Parse(Data), _context);
        return result.Data.SelectToken("$.out");
    }

    private double Number(string expression)
    {
        var token = Run(expression);
        Assert.That(token, Is.Not.Null, $"{expression} produced no value");
        return token!.Value<double>();
    }

    private bool Warned() => _context.GetLogEntries().Any(e => e.Level >= LogLevel.Warning);

    // ── The case that started this ────────────────────────────────────────────

    [Test]
    public void InterestFormula_FromTheOriginalQuestion()
        => Assert.That(Number("=round($.principal * $.rate * $.yearFraction, 2)"), Is.EqualTo(25));

    [Test]
    public void InfixInsideAnArgument_IsEvaluated()
        => Assert.That(Number("=round($.x * 1, 1)"), Is.EqualTo(2.3));

    // ── Precedence and associativity ──────────────────────────────────────────

    [TestCase("=2 + 3 * 4", 14)]
    [TestCase("=(2 + 3) * 4", 20)]
    [TestCase("=10 - 4 - 3", 3)]
    [TestCase("=100 / 10 / 5", 2)]
    [TestCase("=2 ^ 3 ^ 2", 512)]
    [TestCase("=-2 ^ 2", -4)]
    [TestCase("=(-2) ^ 2", 4)]
    [TestCase("=10 % 4", 2)]
    [TestCase("=7 / 2", 3.5)]
    [TestCase("=1 + 2 + 3 + 4", 10)]
    [TestCase("=2 * 3 * 4", 24)]
    [TestCase("=2 * 3 + 4 * 5", 26)]
    [TestCase("=2 + 3 % 2", 3)]
    public void Precedence(string expression, double expected)
        => Assert.That(Number(expression), Is.EqualTo(expected));

    [Test]
    public void ParenthesisedStart_IsAnExpression()
        => Assert.That(Number("=($.a + $.b)"), Is.EqualTo(10));

    // ── Operands ──────────────────────────────────────────────────────────────

    [Test]
    public void PathsAndNumbersMix()
        => Assert.That(Number("=$.a * 2 - $.b"), Is.EqualTo(8));

    [Test]
    public void UnaryMinus_OnAPath()
        => Assert.That(Number("=-$.a + 10"), Is.EqualTo(4));

    [Test]
    public void NegativeLiteral_AfterAnOperator()
        => Assert.That(Number("=$.a - -5"), Is.EqualTo(11));

    [Test]
    public void UnaryMinus_OnAGroup()
        => Assert.That(Number("=-($.a + $.b)"), Is.EqualTo(-10));

    [Test]
    public void UnaryMinus_AloneOnAPath()
        => Assert.That(Number("=-$.a"), Is.EqualTo(-6));

    [Test]
    public void ScientificNotation()
        => Assert.That(Number("=1.5e2 + 0"), Is.EqualTo(150));

    [Test]
    public void CallAsAnOperand_WithAndWithoutEquals()
    {
        Assert.That(Number("=round($.x, 1) * 2"), Is.EqualTo(4.6));
        Assert.That(Number("=2 * =round($.x, 1)"), Is.EqualTo(4.6));
    }

    [Test]
    public void CallArgumentsMayThemselvesBeInfix()
        => Assert.That(Number("=max($.a * 2, $.b * 2) + 1"), Is.EqualTo(13));

    [Test]
    public void BracketedPath_MayContainOperatorCharacters()
        => Assert.That(Number("=$['unit-price'] * 2"), Is.EqualTo(10));

    [Test]
    public void FilterPath_WithSpacesAndOperatorsInsideBrackets_IsOnePath()
    {
        var script = """[{"command":"put","path":"$.out","value":"=sum($.items[?(@.n > 1)].n) * 2"}]""";
        var result = _engine.Execute(script, JToken.Parse("""{"items":[{"n":1},{"n":2},{"n":3}]}"""), _context);
        Assert.That(result.Data.SelectToken("$.out")!.Value<double>(), Is.EqualTo(10));
    }

    // ── The whitespace rule ───────────────────────────────────────────────────

    [Test]
    public void OperatorWithoutWhitespace_InsidePath_IsNotArithmetic()
    {
        // "$.a*$.b" is one path; it resolves to nothing, so nothing is written.
        Assert.That(Run("=$.a*$.b"), Is.Null);
    }

    [Test]
    public void OperatorWithoutWhitespaceAfterAGroup_IsRejectedWithAReason()
    {
        Assert.That(Run("=($.a + 1)* 2"), Is.Null);
        Assert.That(Warned(), Is.True);
        Assert.That(_context.GetLogEntries().Select(e => e.Message), Has.Some.Contains("whitespace"));
    }

    // ── Rejected and fallback forms ───────────────────────────────────────────

    [Test]
    public void BareWord_IsNotAValue()
    {
        Assert.That(Run("=$.a * rate"), Is.Null);
        Assert.That(_context.GetLogEntries().Select(e => e.Message), Has.Some.Contains("'rate' is not a value"));
    }

    [Test]
    public void UnbalancedParentheses_AreReported_NotThrown()
    {
        Assert.That(Run("=($.a + 1"), Is.Null);
        Assert.That(Run("=$.a + 1)"), Is.Null);
        Assert.That(Warned(), Is.True);
    }

    [Test]
    public void DanglingOperator_IsReported()
    {
        Assert.That(Run("=$.a +"), Is.Null);
        Assert.That(Warned(), Is.True);
    }

    [Test]
    public void QuotedOperatorInsideACall_StaysLiteralText()
        => Assert.That(Run("=concat($.first, ' - ', $.last)")!.Value<string>(), Is.EqualTo("Sanne - Berg"));

    [Test]
    public void UnquotedTextWithAnOperator_InAnArgument_FallsBackToLiteralAndWarns()
    {
        Assert.That(Run("=concat($.first, and - then, $.last)")!.Value<string>(), Is.EqualTo("Sanneand - thenBerg"));
        Assert.That(Warned(), Is.True);
    }

    [Test]
    public void PlainCalls_AreUntouched()
        => Assert.That(Number("=multiply($.a, $.b)"), Is.EqualTo(24));

    [Test]
    public void DoubleEquals_StillEscapesToALiteral()
        => Assert.That(Run("==$.a * $.b")!.Value<string>(), Is.EqualTo("=$.a * $.b"));

    // ── Registry binding ──────────────────────────────────────────────────────

    [Test]
    public void WithoutTheMathPack_ThePackIsNamed()
    {
        var context = JsonExecutionContext.CreateDefault();
        var options = ParseOptions<JToken>.CreateDefault();
        var engine = new ScriptEngine<JToken>(options.CommandsProvider, options.FunctionsProvider);

        var result = engine.Execute("""[{"command":"put","path":"$.out","value":"=$.a * $.b"}]""",
            JToken.Parse(Data), context);

        Assert.That(result.Data.SelectToken("$.out"), Is.Null);
        Assert.That(context.GetLogEntries().Select(e => e.Message), Has.Some.Contains("RegisterMath"));
    }

    [Test]
    public void SameScriptRunTwice_GivesTheSameResult()
    {
        var script = _engine.Compile(
            """[{"command":"put","path":"$.out","value":"=$.a * $.b + 1"}]""", JsonExecutionContext.CreateDefault());
        for (var i = 0; i < 3; i++)
        {
            var result = script.Execute(JToken.Parse(Data), JsonExecutionContext.CreateDefault());
            Assert.That(result.Data.SelectToken("$.out")!.Value<double>(), Is.EqualTo(25));
        }
    }
}
