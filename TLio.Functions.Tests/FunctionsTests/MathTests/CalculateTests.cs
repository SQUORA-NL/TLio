using Microsoft.Extensions.Logging;
using Newtonsoft.Json.Linq;
using NUnit.Framework;
using TLio.Client;
using TLio.Core.Contracts;
using TLio.Core.Models;
using TLio.Core.Models.Logging;
using TLio.Extensions.Math;
using TLio.Extensions.Text;
using TLio.Json;

namespace TLio.Functions.Tests.FunctionsTests.MathTests;

/// <summary>
/// =calculate(expression) — arithmetic over a string expression, evaluated by
/// System.Data.DataTable.Compute.
///
/// The expression argument can be a literal ('2+3') or a path to a string held in the
/// document ($.expr), which is what makes the function useful: the arithmetic can travel
/// with the data. Results follow the shared math contract — whole numbers come back as
/// integers, fractional ones as floating point.
///
/// Two behaviours diverge from JLio and are pinned at the bottom of this fixture rather
/// than left undiscovered: `{{$.path}}` substitution inside the expression, and European
/// comma decimals, are both unsupported here.
/// </summary>
[TestFixture]
public class CalculateTests
{
    private JToken data = null!;
    private IExecutionContext<JToken> context = null!;

    [SetUp]
    public void Setup()
    {
        context = JsonExecutionContext.CreateDefault();
        data = JToken.Parse("""{ "add": "2 + 3", "complex": "10 * (4 - 1)", "modulo": "7 % 3" }""");
    }

    /// <summary>Runs an expression through the full engine and returns the written result.</summary>
    private JToken? Eval(string expression, string document = "{}")
    {
        var ctx = JsonExecutionContext.CreateDefault();
        var options = ParseOptions<JToken>.CreateDefault();
        options.FunctionsProvider.RegisterMath<JToken>();
        var engine = new ScriptEngine<JToken>(options.CommandsProvider, options.FunctionsProvider);

        var script = $$"""[{"command":"add","path":"$.result","value":"{{expression}}"}]""";
        var result = engine.Execute(script, JToken.Parse(document), ctx);

        LastLog = ctx.GetLogEntries().ToList();
        LastSuccess = result.Success;
        return result.Data.SelectToken("$.result");
    }

    private List<LogEntry> LastLog = new();
    private bool LastSuccess;

    private void AssertNoErrors() =>
        Assert.That(LastLog.Any(e => e.Level >= LogLevel.Warning), Is.False,
            $"unexpected log: {string.Join(" | ", LastLog.Select(e => $"[{e.Level}] {e.Message}"))}");

    // ── Basic arithmetic ──────────────────────────────────────────────────────

    [TestCase("2+3", 5)]
    [TestCase("10-7", 3)]
    [TestCase("6*4", 24)]
    [TestCase("15/3", 5)]
    [TestCase("17%5", 2)]
    [TestCase("2 + 3", 5)]
    [TestCase("  7  *  6  ", 42)]
    public void BasicArithmetic(string expression, double expected)
    {
        var result = Eval($"=calculate('{expression}')");

        Assert.That(LastSuccess, Is.True);
        AssertNoErrors();
        Assert.That(result!.Value<double>(), Is.EqualTo(expected));
    }

    // ── Operator precedence ───────────────────────────────────────────────────

    [TestCase("2+3*4", 14)]
    [TestCase("2*3+4", 10)]
    [TestCase("10-2*3", 4)]
    [TestCase("20/4+3", 8)]
    [TestCase("2+10%4", 4)]
    public void PrecedenceFollowsArithmeticRules(string expression, double expected)
    {
        var result = Eval($"=calculate('{expression}')");

        Assert.That(LastSuccess, Is.True);
        Assert.That(result!.Value<double>(), Is.EqualTo(expected));
    }

    // ── Parentheses ───────────────────────────────────────────────────────────

    [TestCase("(2+3)*4", 20)]
    [TestCase("2+(3*4)", 14)]
    [TestCase("((2+3)*4)+1", 21)]
    [TestCase("(10-6)/(2*2)", 1)]
    [TestCase("(5+3)*(2-1)", 8)]
    [TestCase("(2*(3+4))-(5*2)", 4)]
    [TestCase("((2+3)*(4+1))-((3*2)+4)", 15)]
    public void Parentheses(string expression, double expected)
    {
        var result = Eval($"=calculate('{expression}')");

        Assert.That(LastSuccess, Is.True);
        AssertNoErrors();
        Assert.That(result!.Value<double>(), Is.EqualTo(expected).Within(0.0001));
    }

    // ── Decimals (period notation) ─────────────────────────────────────────────

    [TestCase("2.5+3.7", 6.2)]
    [TestCase("10.5-7.25", 3.25)]
    [TestCase("3.14*2", 6.28)]
    [TestCase("15.75/3.15", 5)]
    [TestCase("(2.5+1.5)*3.2", 12.8)]
    [TestCase("1.234567*2", 2.469134)]
    public void PeriodDecimals(string expression, double expected)
    {
        var result = Eval($"=calculate('{expression}')");

        Assert.That(LastSuccess, Is.True);
        AssertNoErrors();
        Assert.That(result!.Value<double>(), Is.EqualTo(expected).Within(0.000001));
    }

    [Test]
    public void PeriodDecimals_AreParsedRegardlessOfTheAmbientCulture()
    {
        // A culture whose decimal separator is ',' must not change how '2.5' is read —
        // otherwise the same script would compute different answers on different machines.
        var original = System.Threading.Thread.CurrentThread.CurrentCulture;
        try
        {
            System.Threading.Thread.CurrentThread.CurrentCulture =
                new System.Globalization.CultureInfo("nl-NL");

            var result = Eval("=calculate('2.5+3.7')");

            Assert.That(LastSuccess, Is.True);
            Assert.That(result!.Value<double>(), Is.EqualTo(6.2).Within(0.000001));
        }
        finally
        {
            System.Threading.Thread.CurrentThread.CurrentCulture = original;
        }
    }

    // ── Negative numbers ──────────────────────────────────────────────────────

    [TestCase("-5+3", -2)]
    [TestCase("3-5", -2)]
    [TestCase("-4*-3", 12)]
    [TestCase("(0-8)/2", -4)]
    [TestCase("-2.5*2", -5)]
    public void NegativeNumbers(string expression, double expected)
    {
        var result = Eval($"=calculate('{expression}')");

        Assert.That(LastSuccess, Is.True);
        Assert.That(result!.Value<double>(), Is.EqualTo(expected).Within(0.000001));
    }

    // ── Integer vs floating-point output ──────────────────────────────────────

    [TestCase("15/3", JTokenType.Integer)]
    [TestCase("2+3", JTokenType.Integer)]
    [TestCase("4*0.5", JTokenType.Integer)]     // 2.0 is whole → integer
    [TestCase("7/2", JTokenType.Float)]
    [TestCase("2.5+3.7", JTokenType.Float)]
    public void WholeResultsComeBackAsIntegers(string expression, JTokenType expectedType)
    {
        var result = Eval($"=calculate('{expression}')");

        Assert.That(LastSuccess, Is.True);
        Assert.That(result!.Type, Is.EqualTo(expectedType),
            "the math pack reports whole numbers as integers so downstream type checks hold");
    }

    [Test]
    public void IntegerDivisionIsNotTruncated()
    {
        var result = Eval("=calculate('7/2')");

        Assert.That(result!.Value<double>(), Is.EqualTo(3.5),
            "DataTable.Compute promotes to floating point — 7/2 is 3.5, not 3");
    }

    // ── Expression read from the document ─────────────────────────────────────

    [Test]
    public void ExpressionCanComeFromAPath()
    {
        var result = Eval("=calculate($.expr)", """{"expr":"2+3"}""");

        Assert.That(LastSuccess, Is.True);
        AssertNoErrors();
        Assert.That(result!.Value<double>(), Is.EqualTo(5));
    }

    [Test]
    public void ExpressionFromAPath_SupportsParenthesesAndDecimals()
    {
        var result = Eval("=calculate($.expr)", """{"expr":"(1.5+2.5)*3"}""");

        Assert.That(result!.Value<double>(), Is.EqualTo(12).Within(0.000001));
    }

    [Test]
    public void ExpressionFromANestedPath()
    {
        var result = Eval("=calculate($.calc.formula)", """{"calc":{"formula":"8*8"}}""");

        Assert.That(result!.Value<double>(), Is.EqualTo(64));
    }

    [Test]
    public void ExpressionFromAPath_ThatHoldsANumberRatherThanAString()
    {
        // TryGetString coerces a numeric node, so a bare number is a valid one-term expression
        var result = Eval("=calculate($.n)", """{"n":42}""");

        Assert.That(LastSuccess, Is.True);
        Assert.That(result!.Value<double>(), Is.EqualTo(42));
    }

    // ── Composition with other functions ──────────────────────────────────────

    [Test]
    public void ExpressionCanBeBuiltByAnInnerFunction()
    {
        var ctx = JsonExecutionContext.CreateDefault();
        var options = ParseOptions<JToken>.CreateDefault();
        options.FunctionsProvider.RegisterMath<JToken>();
        options.FunctionsProvider.RegisterText<JToken>();
        var engine = new ScriptEngine<JToken>(options.CommandsProvider, options.FunctionsProvider);

        const string script = """[{"command":"add","path":"$.result","value":"=calculate(=concat($.a,'+',$.b))"}]""";
        var result = engine.Execute(script, JToken.Parse("""{"a":"2","b":"3"}"""), ctx);

        Assert.That(result.Success, Is.True,
            $"log: {string.Join(" | ", ctx.GetLogEntries().Select(e => e.Message))}");
        Assert.That(result.Data.SelectToken("$.result")!.Value<double>(), Is.EqualTo(5));
    }

    // ── Error handling: every failure warns and does not throw ────────────────

    [TestCase("(2+3", "closing parenthesis")]
    [TestCase("abc", null)]
    [TestCase("2+", null)]
    [TestCase("*5", null)]
    public void MalformedExpression_FailsWithALoggedReasonAndNeverThrows(string expression, string? detail)
    {
        JToken? result = null;
        Assert.That(() => result = Eval($"=calculate('{expression}')"), Throws.Nothing);

        Assert.That(LastSuccess, Is.False, "an unevaluable expression is a script failure");
        Assert.That(LastLog.Any(e => e.Level >= LogLevel.Warning && e.Message.Contains("calculate")), Is.True,
            "the reason must reach the log so the author can see what broke");
        if (detail != null)
            Assert.That(LastLog.Any(e => e.Message.Contains(detail)), Is.True,
                "the underlying evaluator message is passed through, not swallowed");
        Assert.That(result, Is.Null, "nothing is written when the expression cannot be evaluated");
    }

    [Test]
    public void DoubledPlus_ReadsAsAUnaryPlusRatherThanFailing()
    {
        // Documenting the evaluator's tolerance: '2++3' is 2 + (+3), not a syntax error.
        var result = Eval("=calculate('2++3')");

        Assert.That(LastSuccess, Is.True);
        Assert.That(result!.Value<double>(), Is.EqualTo(5));
    }

    [Test]
    public void EmptyExpression_Fails()
    {
        Assert.That(() => Eval("=calculate('')"), Throws.Nothing);

        Assert.That(LastSuccess, Is.False);
        Assert.That(LastLog.Any(e => e.Level >= LogLevel.Warning), Is.True);
    }

    [Test]
    public void Calculate_PathNotFound_ReturnsFailed()
    {
        var fn = new Calculate<JToken>();
        fn.SetArguments(new Arguments<JToken> { new PathValue<JToken>("$.missing") });

        var result = fn.Execute(data, data, context);

        Assert.That(result.Success, Is.False);
        Assert.That(context.GetLogEntries().Any(e => e.Message.Contains("path not found")), Is.True);
    }

    [Test]
    public void Calculate_NoArgs_ReturnsFailed()
    {
        var fn = new Calculate<JToken>();
        fn.SetArguments(new Arguments<JToken>());

        var result = fn.Execute(data, data, context);

        Assert.That(result.Success, Is.False);
        Assert.That(context.GetLogEntries().Any(e => e.Message.Contains("one expression argument required")), Is.True);
    }

    [Test]
    public void Calculate_ArgumentIsAnObject_ReturnsFailed()
    {
        var objData = JToken.Parse("""{"expr":{"nested":true}}""");
        var fn = new Calculate<JToken>();
        fn.SetArguments(new Arguments<JToken> { new PathValue<JToken>("$.expr") });

        var result = fn.Execute(objData, objData, context);

        Assert.That(result.Success, Is.False, "an object is not an expression");
    }

    // ── Expressions held in the document, via the fixture data ────────────────

    [TestCase("$.add", 5)]
    [TestCase("$.complex", 30)]
    [TestCase("$.modulo", 1)]
    public void FixtureExpressions_EvaluateThroughTheFunctionDirectly(string path, double expected)
    {
        var fn = new Calculate<JToken>();
        fn.SetArguments(new Arguments<JToken> { new PathValue<JToken>(path) });

        var result = fn.Execute(data, data, context);

        Assert.That(result.Success, Is.True);
        Assert.That(context.NodeAdapter.TryGetDouble(result.Data.First!), Is.EqualTo(expected));
    }

    // ── Divergences from JLio, pinned so they are visible rather than assumed ──

    [TestCase("2+{{$.v}}", """{"v":3}""")]
    [TestCase("{{$.a}}*{{$.b}}", """{"a":4,"b":7}""")]
    [TestCase("{{$.user.age}}+5", """{"user":{"age":25}}""")]
    public void CurlyBraceVariableSubstitution_IsNotSupported(string expression, string document)
    {
        // JLio's calculate interpolates {{$.path}} inside the expression string. TLio does not:
        // the expression reaches DataTable.Compute verbatim and fails on '{'. The supported
        // equivalent is to build the expression with =concat(...) or store it in the document.
        var result = Eval($"=calculate('{expression}')", document);

        Assert.That(LastSuccess, Is.False);
        Assert.That(result, Is.Null);
        Assert.That(LastLog.Any(e => e.Message.Contains("Cannot interpret token '{'")), Is.True,
            "if this ever starts passing, {{path}} substitution has been implemented — update this test");
    }

    [TestCase("2,5+3,7")]
    [TestCase("0,1+0,2")]
    public void CommaDecimalNotation_IsNotSupported(string expression)
    {
        // JLio accepts European comma decimals. TLio requires period decimals; a comma reads
        // as an argument separator to the evaluator and is a syntax error.
        var result = Eval($"=calculate('{expression}')");

        Assert.That(LastSuccess, Is.False);
        Assert.That(result, Is.Null);
        Assert.That(LastLog.Any(e => e.Message.Contains("Syntax error")), Is.True);
    }

    [Test]
    public void DivisionByZero_ProducesANonFiniteNumber()
    {
        // Documented, not endorsed: DataTable.Compute yields double.Infinity, which Newtonsoft
        // serialises as the STRING "Infinity" — not valid JSON as a number. A caller reading
        // $.result as a number gets a string. Worth deciding on separately.
        var result = Eval("=calculate('5/0')");

        Assert.That(LastSuccess, Is.True, "the evaluator does not consider this an error");
        Assert.That(result!.Value<string>(), Is.EqualTo("Infinity"));
        Assert.That(result.Type, Is.EqualTo(JTokenType.Float));
    }

    [Test]
    public void ZeroDividedByZero_ProducesNaN()
    {
        var result = Eval("=calculate('0/0')");

        Assert.That(LastSuccess, Is.True);
        Assert.That(result!.Value<string>(), Is.EqualTo("NaN"));
    }
}
