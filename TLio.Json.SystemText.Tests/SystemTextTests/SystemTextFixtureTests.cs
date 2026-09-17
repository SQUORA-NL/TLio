using System.Text.Json.Nodes;
using NUnit.Framework;
using TLio.Client;
using TLio.Json.SystemText;
using TLio.Json.SystemText.Tests.Fixtures;

namespace TLio.Json.SystemText.Tests.SystemTextTests;

/// <summary>
/// Adapter compliance tests: runs the same fixture triplets as TLio.UnitTests SystemTextFixtureTests
/// but scoped to TLio.Json.SystemText.Tests for project-level consistency.
///
/// Pass = System.Text.Json adapter is behaviorally equivalent to the Newtonsoft adapter
/// for the same inputs and scripts.
/// </summary>
[TestFixture]
public class SystemTextFixtureTests
{
    private ScriptEngine<JsonNode> _engine = null!;
    private SystemTextJsonNodeAdapter _adapter = null!;

    [SetUp]
    public void SetUp()
    {
        var options = ParseOptions<JsonNode>.CreateDefault();
        _engine = new ScriptEngine<JsonNode>(options.CommandsProvider, options.FunctionsProvider);
        _adapter = new SystemTextJsonNodeAdapter();
    }

    // ── Commands ──────────────────────────────────────────────────────────────

    [TestCaseSource(typeof(FixtureTheoryLoader), nameof(FixtureTheoryLoader.LoadRaw), new object[] { "Set" })]
    public void Set(string inputJson, string script, string expectedJson)
        => RunFixture(inputJson, script, expectedJson);

    [TestCaseSource(typeof(FixtureTheoryLoader), nameof(FixtureTheoryLoader.LoadRaw), new object[] { "Add" })]
    public void Add(string inputJson, string script, string expectedJson)
        => RunFixture(inputJson, script, expectedJson);

    [TestCaseSource(typeof(FixtureTheoryLoader), nameof(FixtureTheoryLoader.LoadRaw), new object[] { "Put" })]
    public void Put(string inputJson, string script, string expectedJson)
        => RunFixture(inputJson, script, expectedJson);

    [TestCaseSource(typeof(FixtureTheoryLoader), nameof(FixtureTheoryLoader.LoadRaw), new object[] { "Remove" })]
    public void Remove(string inputJson, string script, string expectedJson)
        => RunFixture(inputJson, script, expectedJson);

    [TestCaseSource(typeof(FixtureTheoryLoader), nameof(FixtureTheoryLoader.LoadRaw), new object[] { "Copy" })]
    public void Copy(string inputJson, string script, string expectedJson)
        => RunFixture(inputJson, script, expectedJson);

    [TestCaseSource(typeof(FixtureTheoryLoader), nameof(FixtureTheoryLoader.LoadRaw), new object[] { "Move" })]
    public void Move(string inputJson, string script, string expectedJson)
        => RunFixture(inputJson, script, expectedJson);

    [TestCaseSource(typeof(FixtureTheoryLoader), nameof(FixtureTheoryLoader.LoadRaw), new object[] { "IfElse" })]
    public void IfElse(string inputJson, string script, string expectedJson)
        => RunFixture(inputJson, script, expectedJson);

    [TestCaseSource(typeof(FixtureTheoryLoader), nameof(FixtureTheoryLoader.LoadRaw), new object[] { "Merge" })]
    public void Merge(string inputJson, string script, string expectedJson)
        => RunFixture(inputJson, script, expectedJson);

    [TestCaseSource(typeof(FixtureTheoryLoader), nameof(FixtureTheoryLoader.LoadRaw), new object[] { "Compare" })]
    public void Compare(string inputJson, string script, string expectedJson)
        => RunFixture(inputJson, script, expectedJson);

    // ── Functions ─────────────────────────────────────────────────────────────

    [TestCaseSource(typeof(FixtureTheoryLoader), nameof(FixtureTheoryLoader.LoadRaw), new object[] { "Fetch" })]
    public void Fetch(string inputJson, string script, string expectedJson)
        => RunFixture(inputJson, script, expectedJson);

    [TestCaseSource(typeof(FixtureTheoryLoader), nameof(FixtureTheoryLoader.LoadRaw), new object[] { "Indirect" })]
    public void Indirect(string inputJson, string script, string expectedJson)
        => RunFixture(inputJson, script, expectedJson);

    [TestCaseSource(typeof(FixtureTheoryLoader), nameof(FixtureTheoryLoader.LoadRaw), new object[] { "Partial" })]
    public void Partial(string inputJson, string script, string expectedJson)
        => RunFixture(inputJson, script, expectedJson);

    [TestCaseSource(typeof(FixtureTheoryLoader), nameof(FixtureTheoryLoader.LoadRaw), new object[] { "Promote" })]
    public void Promote(string inputJson, string script, string expectedJson)
        => RunFixture(inputJson, script, expectedJson);

    [TestCaseSource(typeof(FixtureTheoryLoader), nameof(FixtureTheoryLoader.LoadRaw), new object[] { "ToArray" })]
    public void ToArray(string inputJson, string script, string expectedJson)
        => RunFixture(inputJson, script, expectedJson);

    [TestCaseSource(typeof(FixtureTheoryLoader), nameof(FixtureTheoryLoader.LoadRaw), new object[] { "ScriptPath" })]
    public void ScriptPath(string inputJson, string script, string expectedJson)
        => RunFixture(inputJson, script, expectedJson);

    // ── Helper ────────────────────────────────────────────────────────────────

    private void RunFixture(string inputJson, string script, string expectedJson)
    {
        var input    = _adapter.Parse(inputJson);
        var expected = _adapter.Parse(expectedJson);
        var context  = SystemTextJsonExecutionContext.CreateDefault();

        var result = _engine.Execute(script, input, context);

        Assert.That(result.Success, Is.True,
            $"Engine reported failure. Log:\n{string.Join("\n", context.GetLogEntries().Select(e => $"  [{e.Level}] {e.Group}: {e.Message}"))}");

        Assert.That(JsonNode.DeepEquals(result.Data, expected), Is.True,
            $"Result mismatch.\n  Expected: {expected}\n  Actual:   {result.Data}");
    }
}
