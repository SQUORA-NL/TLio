using Newtonsoft.Json.Linq;
using NUnit.Framework;
using TLio.Client;
using TLio.Json;

namespace TLio.UnitTests.Fixtures;

/// <summary>
/// Data-driven engine-level tests driven by (input.json, script.json, result.json)
/// fixture triplets.  Each fixture runs through ParseOptions.CreateDefault() +
/// ScriptEngine so the same files can be reused against any adapter (Phase 7).
///
/// Fixture folders live under TLio.UnitTests/Fixtures/&lt;CommandName&gt;/.
/// </summary>
[TestFixture]
public class FixtureTests
{
    private ScriptEngine<JToken> _engine = null!;

    [SetUp]
    public void SetUp()
    {
        var options = ParseOptions<JToken>.CreateDefault();
        _engine = new ScriptEngine<JToken>(options.CommandsProvider, options.FunctionsProvider);
    }

    // ── Commands ──────────────────────────────────────────────────────────────

    [TestCaseSource(typeof(FixtureTheoryLoader), nameof(FixtureTheoryLoader.Load), new object[] { "Set" })]
    public void Set(JToken input, string script, JToken expected)
        => RunFixture(input, script, expected);

    [TestCaseSource(typeof(FixtureTheoryLoader), nameof(FixtureTheoryLoader.Load), new object[] { "Add" })]
    public void Add(JToken input, string script, JToken expected)
        => RunFixture(input, script, expected);

    [TestCaseSource(typeof(FixtureTheoryLoader), nameof(FixtureTheoryLoader.Load), new object[] { "Put" })]
    public void Put(JToken input, string script, JToken expected)
        => RunFixture(input, script, expected);

    [TestCaseSource(typeof(FixtureTheoryLoader), nameof(FixtureTheoryLoader.Load), new object[] { "Remove" })]
    public void Remove(JToken input, string script, JToken expected)
        => RunFixture(input, script, expected);

    [TestCaseSource(typeof(FixtureTheoryLoader), nameof(FixtureTheoryLoader.Load), new object[] { "Copy" })]
    public void Copy(JToken input, string script, JToken expected)
        => RunFixture(input, script, expected);

    [TestCaseSource(typeof(FixtureTheoryLoader), nameof(FixtureTheoryLoader.Load), new object[] { "Move" })]
    public void Move(JToken input, string script, JToken expected)
        => RunFixture(input, script, expected);

    [TestCaseSource(typeof(FixtureTheoryLoader), nameof(FixtureTheoryLoader.Load), new object[] { "IfElse" })]
    public void IfElse(JToken input, string script, JToken expected)
        => RunFixture(input, script, expected);

    [TestCaseSource(typeof(FixtureTheoryLoader), nameof(FixtureTheoryLoader.Load), new object[] { "Merge" })]
    public void Merge(JToken input, string script, JToken expected)
        => RunFixture(input, script, expected);

    [TestCaseSource(typeof(FixtureTheoryLoader), nameof(FixtureTheoryLoader.Load), new object[] { "Compare" })]
    public void Compare(JToken input, string script, JToken expected)
        => RunFixture(input, script, expected);

    [TestCaseSource(typeof(FixtureTheoryLoader), nameof(FixtureTheoryLoader.Load), new object[] { "DecisionTable" })]
    public void DecisionTable(JToken input, string script, JToken expected)
        => RunFixture(input, script, expected);

    [TestCaseSource(typeof(FixtureTheoryLoader), nameof(FixtureTheoryLoader.Load), new object[] { "SetProperties" })]
    public void SetProperties(JToken input, string script, JToken expected)
        => RunFixture(input, script, expected);

    [TestCaseSource(typeof(FixtureTheoryLoader), nameof(FixtureTheoryLoader.Load), new object[] { "EscapeChars" })]
    public void EscapeChars(JToken input, string script, JToken expected)
        => RunFixture(input, script, expected);

    [TestCaseSource(typeof(FixtureTheoryLoader), nameof(FixtureTheoryLoader.Load), new object[] { "Notation" })]
    public void Notation(JToken input, string script, JToken expected)
        => RunFixture(input, script, expected);

    // ── Helper ────────────────────────────────────────────────────────────────

    private void RunFixture(JToken input, string script, JToken expected)
    {
        var context = JsonExecutionContext.CreateDefault();
        var result = _engine.Execute(script, input, context);

        Assert.That(result.Success, Is.True,
            $"Engine reported failure. Log:\n{string.Join("\n", context.GetLogEntries().Select(e => $"  [{e.Level}] {e.Group}: {e.Message}"))}");

        Assert.That(JToken.DeepEquals(result.Data, expected), Is.True,
            $"Result mismatch.\n  Expected: {expected}\n  Actual:   {result.Data}");
    }
}
