using Newtonsoft.Json.Linq;
using NUnit.Framework;
using TLio.Client;
using TLio.Extensions.Text;
using TLio.Json;

namespace TLio.Functions.Tests.FunctionsTests.DecisionTableTests;

/// <summary>
/// Settles the one behaviour the AFD engine-changes proposal flagged as unverified: whether
/// <c>@.</c> resolves against the *current target node* — not the document root, and not the
/// first target in a wildcard selection — when it appears as an argument nested inside a
/// function call, e.g. <c>"=concat(@.a,'-',@.b)"</c>.
///
/// It did not, as first written: a path-shaped argument inside a function call is parsed as a
/// literal string (so functions can resolve it themselves — see <c>FunctionConverter</c>'s
/// argument-parsing rule), and the resolution helper every function used for that
/// (<c>FunctionBase&lt;TNode&gt;.ResolveArg</c>) re-selected the string against
/// <c>dataContext</c>, not <c>currentNode</c> — silently ignoring which target node a wildcard
/// path was currently processing. Fixed by <c>TLio.Core.Models.RelativePathResolution</c>, now
/// shared by <c>ResolveArg</c>, <c>PathValue</c>, <c>fetch()</c>, <c>partial()</c> and the text
/// pack's regex argument helper. This fixture is the second example from
/// <c>docs/ai-ref/notation-reference.md</c> §5, run for real; the first is below.
/// </summary>
[TestFixture]
public class NestedFunctionArgumentTests
{
    private ScriptEngine<JToken> engine = null!;

    [SetUp]
    public void Setup()
    {
        var options = ParseOptions<JToken>.CreateDefault();
        options.FunctionsProvider.RegisterText<JToken>();
        engine = new ScriptEngine<JToken>(options.CommandsProvider, options.FunctionsProvider);
    }

    [Test]
    public void ConcatOfTwoRelativePaths_ResolvesPerTargetNode_NotOncePerDocument()
    {
        const string script = @"[
          {
            ""command"": ""decisionTable"",
            ""path"": ""$.items[*]"",
            ""config"": {
              ""inputs"":  [{ ""name"": ""a"", ""path"": ""@.a"" }],
              ""outputs"": [{ ""name"": ""full"", ""path"": ""@.full"" }],
              ""rules"": [
                {
                  ""priority"": 0,
                  ""conditions"": { ""a"": ""!=__never__"" },
                  ""results"":    { ""full"": ""=concat(@.a,'-',@.b)"" }
                }
              ]
            }
          }
        ]";

        var data = JToken.Parse(@"
        {
            ""items"": [
                { ""a"": ""x"", ""b"": ""y"" },
                { ""a"": ""p"", ""b"": ""q"" }
            ]
        }");

        var result = engine.Execute(script, data, JsonExecutionContext.CreateDefault());

        Assert.That(result.Success, Is.True);
        Assert.That(result.Data.SelectToken("$.items[0].full")?.Value<string>(), Is.EqualTo("x-y"));
        Assert.That(result.Data.SelectToken("$.items[1].full")?.Value<string>(), Is.EqualTo("p-q"),
            "each target node must produce its own value — not two copies of the first match");
    }

    /// <summary>
    /// The literal example from <c>docs/ai-ref/notation-reference.md</c> §5, "Path References":
    /// <c>{ "command": "set", "path": "$.items[*].full", "value": "=concat(@.first, ' ', @.last)" }</c>.
    /// </summary>
    [Test]
    public void DocumentedNotationReferenceExample_SetOverWildcardWithNestedConcat()
    {
        const string script = @"[
          { ""command"": ""set"", ""path"": ""$.items[*].full"", ""value"": ""=concat(@.first, ' ', @.last)"" }
        ]";

        var data = JToken.Parse(@"
        {
            ""items"": [
                { ""first"": ""Alice"", ""last"": ""Anderson"", ""full"": """" },
                { ""first"": ""Bob"",   ""last"": ""Baker"",    ""full"": """" }
            ]
        }");

        var result = engine.Execute(script, data, JsonExecutionContext.CreateDefault());

        Assert.That(result.Success, Is.True);
        Assert.That(result.Data.SelectToken("$.items[0].full")?.Value<string>(), Is.EqualTo("Alice Anderson"));
        Assert.That(result.Data.SelectToken("$.items[1].full")?.Value<string>(), Is.EqualTo("Bob Baker"));
    }

    /// <summary>
    /// The idiom <c>IfFunction</c>'s own doc comment names as the safe way to read an optional
    /// field: <c>=if(=exists($.a),=fetch($.a),'-')</c>. Here with <c>@.</c> over a wildcard, one
    /// entity missing the field — the shape a table-driven per-attribute mapping (AFD, or any
    /// similar catalogue-driven conversion) depends on for every optional attribute.
    ///
    /// Exercises a second, independent bug this session found and fixed alongside the one above:
    /// predicates (<c>exists</c>, <c>equals</c>, <c>isObject</c>, <c>isNull</c>, <c>in</c>,
    /// <c>matches</c>, the comparison functions, and <c>if</c>'s own condition) share
    /// <c>PredicateFunctionBase&lt;TNode&gt;.TryResolveNode</c>, a *second*, independent copy of
    /// the same root-instead-of-current-node flaw <c>ResolveArg</c> had — it does not reuse
    /// <c>ResolveArg</c> by design (it must tell "no match" apart from "failed"), so fixing
    /// <c>ResolveArg</c> alone did not fix it.
    /// </summary>
    [Test]
    public void ExistsAndFetchIdiom_PerNode_HandlesAMissingFieldOnOneEntityOnly()
    {
        const string script = @"[
          {
            ""command"": ""decisionTable"",
            ""path"": ""$.items[*]"",
            ""config"": {
              ""inputs"":  [{ ""name"": ""a"", ""path"": ""@.a"" }],
              ""outputs"": [{ ""name"": ""out"", ""path"": ""@.out"" }],
              ""rules"": [
                {
                  ""priority"": 0,
                  ""conditions"": { ""a"": ""!=__never__"" },
                  ""results"":    { ""out"": ""=if(exists(@.opt),fetch(@.opt),'-')"" }
                }
              ]
            }
          }
        ]";

        var data = JToken.Parse(@"
        {
            ""items"": [
                { ""a"": ""x"", ""opt"": ""present"" },
                { ""a"": ""y"" }
            ]
        }");

        var result = engine.Execute(script, data, JsonExecutionContext.CreateDefault());

        Assert.That(result.Success, Is.True);
        Assert.That(result.Data.SelectToken("$.items[0].out")?.Value<string>(), Is.EqualTo("present"));
        Assert.That(result.Data.SelectToken("$.items[1].out")?.Value<string>(), Is.EqualTo("-"),
            "the second entity has no 'opt' of its own — exists(@.opt) must not see the first entity's value, " +
            "and must not fall back to treating the whole document as the answer");
    }
}
