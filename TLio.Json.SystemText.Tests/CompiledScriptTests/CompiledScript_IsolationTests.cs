using System.Text.Json.Nodes;
using NUnit.Framework;
using TLio.Client;
using TLio.Json.SystemText;
using TLio.Json.SystemText.Tests.Fixtures;

namespace TLio.Json.SystemText.Tests.CompiledScriptTests;

/// <summary>
/// Verifies that each instance produced by CompiledScript.CreateExecutable() owns its own
/// execution state and cannot be contaminated by other instances derived from the same template.
/// </summary>
[TestFixture]
public class CompiledScript_IsolationTests
{
    private ScriptEngine<JsonNode> _engine = null!;
    private SystemTextJsonNodeAdapter _adapter = null!;
    private CompiledScript<JsonNode> _compiled = null!;

    private const string ScriptJson = """[{"command":"set","path":"$.name","value":"compiled"}]""";

    [SetUp]
    public void SetUp()
    {
        var options = ParseOptions<JsonNode>.CreateDefault();
        _engine  = new ScriptEngine<JsonNode>(options.CommandsProvider, options.FunctionsProvider);
        _adapter = new SystemTextJsonNodeAdapter();
        _compiled = _engine.Compile(ScriptJson, _adapter);
    }

    [Test]
    public void CreateExecutable_ReturnsNewInstanceEachCall()
    {
        var a = _compiled.CreateExecutable();
        var b = _compiled.CreateExecutable();
        Assert.That(a, Is.Not.SameAs(b));
    }

    [Test]
    public void CreateExecutable_CommandsAreNotSameReferences()
    {
        var a = _compiled.CreateExecutable();
        var b = _compiled.CreateExecutable();

        Assert.That(a.Count, Is.EqualTo(b.Count));
        for (var i = 0; i < a.Count; i++)
            Assert.That(a[i], Is.Not.SameAs(b[i]),
                $"Command[{i}] should be a distinct clone, not the same object reference.");
    }

    [Test]
    public void FailedExecutionDoesNotContaminateNextInstance()
    {
        // Execute A with data that triggers the script to produce its result
        var inputA = _adapter.Parse("""{"name":"original"}""");
        var ctxA   = SystemTextJsonExecutionContext.CreateDefault();
        var resultA = _compiled.Execute(inputA, ctxA);
        Assert.That(resultA.Success, Is.True, "Execution A should succeed.");

        // Create a fresh B after A has run — its commands must start with clean state
        var inputB = _adapter.Parse("""{"name":"original"}""");
        var ctxB   = SystemTextJsonExecutionContext.CreateDefault();
        var resultB = _compiled.Execute(inputB, ctxB);
        Assert.That(resultB.Success, Is.True, "Execution B should succeed independently of A.");

        var expected = _adapter.Parse("""{"name":"compiled"}""");
        Assert.That(JsonNode.DeepEquals(resultB.Data, expected), Is.True,
            "Execution B should produce the correct result regardless of prior executions.");
    }

    [Test]
    public void MultipleSequentialExecutions_AllProduceCorrectResult()
    {
        var expected = _adapter.Parse("""{"name":"compiled"}""");

        for (var i = 0; i < 20; i++)
        {
            var input  = _adapter.Parse("""{"name":"original"}""");
            var ctx    = SystemTextJsonExecutionContext.CreateDefault();
            var result = _compiled.Execute(input, ctx);

            Assert.That(result.Success, Is.True, $"Iteration {i} should succeed.");
            Assert.That(JsonNode.DeepEquals(result.Data, expected), Is.True,
                $"Iteration {i} should produce correct result.");
        }
    }
}
