using Newtonsoft.Json.Linq;
using NUnit.Framework;
using TLio.Client;
using TLio.Json;

namespace TLio.Functions.Tests.Fixtures;

/// <summary>
/// Data-driven engine-level tests for built-in functions (Fetch, Indirect, Partial, Promote, ScriptPath).
/// Fixture triplets live under TLio.Functions.Tests/Fixtures/&lt;FunctionName&gt;/.
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

    [TestCaseSource(typeof(FixtureTheoryLoader), nameof(FixtureTheoryLoader.Load), new object[] { "Fetch" })]
    public void Fetch(JToken input, string script, JToken expected)
        => RunFixture(input, script, expected);

    [TestCaseSource(typeof(FixtureTheoryLoader), nameof(FixtureTheoryLoader.Load), new object[] { "Indirect" })]
    public void Indirect(JToken input, string script, JToken expected)
        => RunFixture(input, script, expected);

    [TestCaseSource(typeof(FixtureTheoryLoader), nameof(FixtureTheoryLoader.Load), new object[] { "Partial" })]
    public void Partial(JToken input, string script, JToken expected)
        => RunFixture(input, script, expected);

    [TestCaseSource(typeof(FixtureTheoryLoader), nameof(FixtureTheoryLoader.Load), new object[] { "Promote" })]
    public void Promote(JToken input, string script, JToken expected)
        => RunFixture(input, script, expected);

    [TestCaseSource(typeof(FixtureTheoryLoader), nameof(FixtureTheoryLoader.Load), new object[] { "ToArray" })]
    public void ToArray(JToken input, string script, JToken expected)
        => RunFixture(input, script, expected);

    [TestCaseSource(typeof(FixtureTheoryLoader), nameof(FixtureTheoryLoader.Load), new object[] { "ScriptPath" })]
    public void ScriptPath(JToken input, string script, JToken expected)
        => RunFixture(input, script, expected);

    [TestCaseSource(typeof(FixtureTheoryLoader), nameof(FixtureTheoryLoader.Load), new object[] { "Notation" })]
    public void Notation(JToken input, string script, JToken expected)
        => RunFixture(input, script, expected);

    [TestCaseSource(typeof(FixtureTheoryLoader), nameof(FixtureTheoryLoader.LoadSingle), new object[] { "Logic/if" })]
    public void Logic_If(JToken input, string script, JToken expected)
        => RunFixture(input, script, expected);

    [TestCaseSource(typeof(FixtureTheoryLoader), nameof(FixtureTheoryLoader.LoadSingle), new object[] { "Logic/coalesce" })]
    public void Logic_Coalesce(JToken input, string script, JToken expected)
        => RunFixture(input, script, expected);

    [TestCaseSource(typeof(FixtureTheoryLoader), nameof(FixtureTheoryLoader.LoadSingle), new object[] { "Logic/between" })]
    public void Logic_Between(JToken input, string script, JToken expected)
        => RunFixture(input, script, expected);

    [TestCaseSource(typeof(FixtureTheoryLoader), nameof(FixtureTheoryLoader.LoadSingle), new object[] { "Collections/distinct" })]
    public void Collections_Distinct(JToken input, string script, JToken expected)
        => RunFixture(input, script, expected);

    [TestCaseSource(typeof(FixtureTheoryLoader), nameof(FixtureTheoryLoader.LoadSingle), new object[] { "Collections/sort" })]
    public void Collections_Sort(JToken input, string script, JToken expected)
        => RunFixture(input, script, expected);

    [TestCaseSource(typeof(FixtureTheoryLoader), nameof(FixtureTheoryLoader.LoadSingle), new object[] { "Collections/sortby" })]
    public void Collections_SortBy(JToken input, string script, JToken expected)
        => RunFixture(input, script, expected);

    [TestCaseSource(typeof(FixtureTheoryLoader), nameof(FixtureTheoryLoader.LoadSingle), new object[] { "Collections/last" })]
    public void Collections_Last(JToken input, string script, JToken expected)
        => RunFixture(input, script, expected);

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
