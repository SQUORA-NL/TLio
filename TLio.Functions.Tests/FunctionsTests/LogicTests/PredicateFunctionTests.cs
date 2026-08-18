using Newtonsoft.Json.Linq;
using NUnit.Framework;
using TLio.Client;
using TLio.Json;

namespace TLio.Functions.Tests.FunctionsTests.LogicTests;

/// <summary>
/// Coverage for the built-in boolean predicates used as ifElse / decisionTable conditions.
/// Each case runs through the real engine so the notation is verified along with the logic.
/// </summary>
[TestFixture]
public class PredicateFunctionTests
{
    private const string Document = """
    {
      "name": "Sanne",
      "age": 37,
      "ageText": "37",
      "premium": 14.5,
      "active": true,
      "activeText": "true",
      "nickname": null,
      "empty": "",
      "tags": ["a", "b"],
      "noTags": [],
      "address": { "city": "Utrecht" },
      "status": "gold",
      "allowed": ["gold", "silver"],
      "email": "sanne@example.nl"
    }
    """;

    private ScriptEngine<JToken> _engine = null!;

    [SetUp]
    public void Setup()
    {
        var options = ParseOptions<JToken>.CreateDefault();
        _engine = new ScriptEngine<JToken>(options.CommandsProvider, options.FunctionsProvider);
    }

    private bool Eval(string expression)
    {
        var context = JsonExecutionContext.CreateDefault();
        var script = $@"[{{ ""command"": ""add"", ""path"": ""$.out"", ""value"": ""{expression}"" }}]";
        var result = _engine.Execute(script, JToken.Parse(Document), context);
        var node = result.Data["out"];
        Assert.That(node, Is.Not.Null, $"'{expression}' produced no value");
        Assert.That(node!.Type, Is.EqualTo(JTokenType.Boolean), $"'{expression}' did not return a boolean");
        return node.Value<bool>();
    }

    // ── equals / notEquals ────────────────────────────────────────────────────

    [TestCase("=equals($.name, 'Sanne')", true)]
    [TestCase("=equals($.name, 'Other')", false)]
    [TestCase("=equals($.age, 37)", true)]
    [TestCase("=equals($.age, 38)", false)]
    [TestCase("=equals($.age, $.ageText)", true)]        // number vs numeric text
    [TestCase("=equals($.active, true)", true)]
    [TestCase("=equals($.nickname, null)", true)]
    [TestCase("=equals($.missing, null)", true)]          // absent behaves like null
    [TestCase("=equals($.missing, $.alsoMissing)", true)]
    [TestCase("=equals($.name, $.missing)", false)]
    [TestCase("=notEquals($.name, 'Other')", true)]
    [TestCase("=notEquals($.age, 37)", false)]
    public void Equality(string expression, bool expected)
        => Assert.That(Eval(expression), Is.EqualTo(expected));

    // ── ordering ──────────────────────────────────────────────────────────────

    [TestCase("=greaterThan($.age, 30)", true)]
    [TestCase("=greaterThan($.age, 37)", false)]
    [TestCase("=greaterOrEqual($.age, 37)", true)]
    [TestCase("=lessThan($.premium, 20)", true)]
    [TestCase("=lessThan($.premium, 14.5)", false)]
    [TestCase("=lessOrEqual($.premium, 14.5)", true)]
    [TestCase("=greaterThan($.name, 'A')", true)]         // ordinal text ordering
    [TestCase("=lessThan($.name, 'A')", false)]
    [TestCase("=greaterThan($.missing, 1)", false)]       // missing operand is not orderable
    [TestCase("=greaterThan($.address, 1)", false)]       // object is not orderable
    public void Ordering(string expression, bool expected)
        => Assert.That(Eval(expression), Is.EqualTo(expected));

    // ── and / or / not ────────────────────────────────────────────────────────

    [TestCase("=and(=equals($.name, 'Sanne'), =greaterThan($.age, 30))", true)]
    [TestCase("=and(equals($.name, 'Sanne'), greaterThan($.age, 30))", true)]   // inner = optional
    [TestCase("=and(equals($.name, 'Sanne'), greaterThan($.age, 90))", false)]
    [TestCase("=or(equals($.name, 'Other'), greaterThan($.age, 30))", true)]
    [TestCase("=or(equals($.name, 'Other'), greaterThan($.age, 90))", false)]
    [TestCase("=not(equals($.name, 'Other'))", true)]
    [TestCase("=not($.active)", false)]
    [TestCase("=and($.active, $.activeText)", true)]      // "true" text is truthy
    [TestCase("=and($.active, $.age)", false)]            // a number is not truthy
    [TestCase("=or(equals($.a, 1), equals($.b, 2), equals($.age, 37))", true)]
    public void BooleanLogic(string expression, bool expected)
        => Assert.That(Eval(expression), Is.EqualTo(expected));

    // ── existence and null ────────────────────────────────────────────────────

    [TestCase("=exists($.name)", true)]
    [TestCase("=exists($.nickname)", true)]              // present but null still exists
    [TestCase("=exists($.missing)", false)]
    [TestCase("=exists($.address.city)", true)]
    [TestCase("=exists($.address.zip)", false)]
    [TestCase("=isNull($.nickname)", true)]
    [TestCase("=isNull($.missing)", true)]
    [TestCase("=isNull($.name)", false)]
    [TestCase("=isNull($.empty)", false)]                // empty string is not null
    public void Existence(string expression, bool expected)
        => Assert.That(Eval(expression), Is.EqualTo(expected));

    // ── type checks ───────────────────────────────────────────────────────────

    [TestCase("=isString($.name)", true)]
    [TestCase("=isString($.ageText)", true)]             // JSON type wins over appearance
    [TestCase("=isString($.age)", false)]
    [TestCase("=isNumber($.age)", true)]
    [TestCase("=isNumber($.premium)", true)]
    [TestCase("=isNumber($.ageText)", false)]
    [TestCase("=isBoolean($.active)", true)]
    [TestCase("=isBoolean($.activeText)", false)]
    [TestCase("=isArray($.tags)", true)]
    [TestCase("=isArray($.noTags)", true)]
    [TestCase("=isArray($.address)", false)]
    [TestCase("=isObject($.address)", true)]
    [TestCase("=isObject($.tags)", false)]
    [TestCase("=isString($.missing)", false)]            // missing is no type at all
    [TestCase("=isNumber($.missing)", false)]
    public void TypeChecks(string expression, bool expected)
        => Assert.That(Eval(expression), Is.EqualTo(expected));

    // ── membership and pattern ────────────────────────────────────────────────

    [TestCase("=in($.status, 'gold', 'silver')", true)]
    [TestCase("=in($.status, 'bronze', 'silver')", false)]
    [TestCase("=in($.status, $.allowed)", true)]          // options from an array
    [TestCase("=in($.age, 36, 37, 38)", true)]
    [TestCase("=in($.missing, 'gold')", false)]
    [TestCase("=matches($.email, '^[^@]+@[^@]+\\\\.[a-z]+$')", true)]
    [TestCase("=matches($.name, '^S')", true)]
    [TestCase("=matches($.name, '^X')", false)]
    [TestCase("=matches($.missing, '.*')", false)]
    public void MembershipAndPattern(string expression, bool expected)
        => Assert.That(Eval(expression), Is.EqualTo(expected));

    // ── use as an ifElse condition ────────────────────────────────────────────

    [Test]
    public void Predicate_DrivesIfElseBranch()
    {
        const string script = """
        [{
          "command": "ifElse",
          "condition": "=and(greaterOrEqual($.age, 18), in($.status, $.allowed))",
          "ifScript":   [{ "command": "add", "path": "$.tier", "value": "eligible" }],
          "elseScript": [{ "command": "add", "path": "$.tier", "value": "rejected" }]
        }]
        """;

        var result = _engine.Execute(script, JToken.Parse(Document), JsonExecutionContext.CreateDefault());
        Assert.That(result.Data["tier"]!.Value<string>(), Is.EqualTo("eligible"));
    }

    [Test]
    public void Predicate_DrivesIfElseElseBranch()
    {
        const string script = """
        [{
          "command": "ifElse",
          "condition": "=and(greaterOrEqual($.age, 18), equals($.status, 'bronze'))",
          "ifScript":   [{ "command": "add", "path": "$.tier", "value": "eligible" }],
          "elseScript": [{ "command": "add", "path": "$.tier", "value": "rejected" }]
        }]
        """;

        var result = _engine.Execute(script, JToken.Parse(Document), JsonExecutionContext.CreateDefault());
        Assert.That(result.Data["tier"]!.Value<string>(), Is.EqualTo("rejected"));
    }

    [Test]
    public void MissingPathCondition_TakesElseBranch()
    {
        const string script = """
        [{
          "command": "ifElse",
          "condition": "=exists($.nothingHere)",
          "ifScript":   [{ "command": "add", "path": "$.tier", "value": "yes" }],
          "elseScript": [{ "command": "add", "path": "$.tier", "value": "no" }]
        }]
        """;

        var result = _engine.Execute(script, JToken.Parse(Document), JsonExecutionContext.CreateDefault());
        Assert.That(result.Data["tier"]!.Value<string>(), Is.EqualTo("no"));
    }

    // ── argument validation ───────────────────────────────────────────────────

    [TestCase("=equals($.name)")]
    [TestCase("=greaterThan($.age)")]
    [TestCase("=not()")]
    [TestCase("=in($.status)")]
    [TestCase("=matches($.email)")]
    public void MissingArguments_FailWithWarning(string expression)
    {
        var context = JsonExecutionContext.CreateDefault();
        var script = $@"[{{ ""command"": ""add"", ""path"": ""$.out"", ""value"": ""{expression}"" }}]";
        var result = _engine.Execute(script, JToken.Parse(Document), context);

        Assert.That(result.Data["out"], Is.Null);
        Assert.That(context.GetLogEntries().Any(e =>
            e.Level >= Microsoft.Extensions.Logging.LogLevel.Warning), Is.True);
    }

    [Test]
    public void InvalidRegexPattern_LogsErrorAndFails()
    {
        var context = JsonExecutionContext.CreateDefault();
        const string script = @"[{ ""command"": ""add"", ""path"": ""$.out"", ""value"": ""=matches($.email, '([')"" }]";
        var result = _engine.Execute(script, JToken.Parse(Document), context);

        Assert.That(result.Data["out"], Is.Null);
        Assert.That(context.GetLogEntries().Any(e =>
            e.Level == Microsoft.Extensions.Logging.LogLevel.Error), Is.True);
    }
}
