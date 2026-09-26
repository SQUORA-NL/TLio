using Newtonsoft.Json.Linq;
using NUnit.Framework;
using TLio.Client;
using TLio.Extensions.Looping;
using TLio.Json;

namespace TLio.UnitTests.CommandsTests.LoopingTests;

/// <summary>
/// A compiled script is a singleton: one host builds it once (<see cref="ScriptEngine{TNode}.Compile"/>)
/// and executes it per request against a fresh document and a fresh <see cref="TLio.Core.Contracts.IExecutionContext{TNode}"/>
/// — exactly how <c>samples/TLio.Sample.Actus.Api</c> hosts <c>pam-envelope.json</c>. That means every
/// command instance inside it, including each <c>set path="@"</c> nested in a <c>forEach</c>, is shared
/// across concurrent executions. A prior bug resolved "@" by temporarily overwriting the command's own
/// <c>Path</c> property and restoring it in a <c>finally</c> — safe single-threaded, but two concurrent
/// executions racing on that shared field corrupted each other's array writes under load. It never showed
/// up in single-threaded tests, only under real concurrency, which is what this fixture forces.
/// </summary>
[TestFixture]
public class ConcurrentExecutionTests
{
    [Test]
    public void CompiledForEachScript_ProducesCorrectResultsUnderConcurrentExecution()
    {
        var options = ParseOptions<JToken>.CreateDefault();
        options.CommandsProvider.RegisterLooping<JToken>();
        var engine = new ScriptEngine<JToken>(options.CommandsProvider, options.FunctionsProvider);
        var adapter = JsonExecutionContext.CreateDefault().NodeAdapter;

        // set path="@" replaces each element in place, keyed off its own value — a wrong resolved
        // path (another thread's index) would either overwrite the wrong slot or leave one unset.
        var compiled = engine.Compile("""
            [ { "command": "forEach", "path": "$.items", "commands": [
                  { "command": "set", "path": "@",
                    "value": { "original": "=fetch(@.v)", "n": "=fetch(@.n)" } }
              ] } ]
            """, adapter);

        const int concurrentRuns = 64;
        var results = new string[concurrentRuns];
        var errors = new Exception?[concurrentRuns];

        Parallel.For(0, concurrentRuns, i =>
        {
            try
            {
                var input = JToken.Parse($$"""
                    { "items": [ {"v":"run{{i}}-a","n":{{i}}}, {"v":"run{{i}}-b","n":{{i + 1}}},
                                  {"v":"run{{i}}-c","n":{{i + 2}}} ] }
                    """);
                var context = JsonExecutionContext.CreateDefault();
                var result = compiled.Execute(input, context);
                results[i] = result.Success
                    ? result.Data.ToString(Newtonsoft.Json.Formatting.None)
                    : "FAILED: " + string.Join(" | ", context.GetLogEntries().Select(e => e.Message));
            }
            catch (Exception ex)
            {
                errors[i] = ex;
            }
        });

        Assert.That(errors, Has.All.Null, "No execution should throw.");
        for (var i = 0; i < concurrentRuns; i++)
        {
            var expected = $$"""{"items":[{"original":"run{{i}}-a","n":{{i}}},{"original":"run{{i}}-b","n":{{i + 1}}},{"original":"run{{i}}-c","n":{{i + 2}}}]}""";
            Assert.That(results[i], Is.EqualTo(expected), $"run {i} was corrupted by a concurrent execution");
        }
    }
}
