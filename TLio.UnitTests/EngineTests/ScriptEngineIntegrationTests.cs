using Newtonsoft.Json.Linq;
using NUnit.Framework;
using TLio.Client;
using TLio.Core.Contracts;
using TLio.Json;

namespace TLio.UnitTests.EngineTests;

/// <summary>
/// Ported from JLio.UnitTests.EngineTests.JLioEngineIntegrationTests.
/// Adaptations:
///   - JLioEngine → ScriptEngine&lt;JToken&gt; with ParseOptions
///   - NUnit classic API → Assert.That() constraint model (NUnit 4)
///
/// End-to-end tests combining multiple commands, conditional branching,
/// recursive-descent paths, and array manipulation.
/// </summary>
[TestFixture]
public class ScriptEngineIntegrationTests
{
    private ScriptEngine<JToken> engine = null!;
    private IExecutionContext<JToken> context = null!;

    [SetUp]
    public void Setup()
    {
        var options = ParseOptions<JToken>.CreateDefault();
        engine = new ScriptEngine<JToken>(options.CommandsProvider, options.FunctionsProvider);
        context = JsonExecutionContext.CreateDefault();
    }

    // ── Copy + remove (move pattern) ──────────────────────────────────────────

    [Test]
    public void Execute_CopyThenRemove_MovesValue()
    {
        var data = JToken.Parse(@"{ ""src"": ""moved"" }");
        const string script = @"[
            { ""command"": ""copy"",   ""fromPath"": ""$.src"", ""toPath"": ""$.dst"" },
            { ""command"": ""remove"", ""path"": ""$.src"" }
        ]";
        var result = engine.Execute(script, data, context);
        Assert.That(result.Success, Is.True);
        Assert.That(result.Data.SelectToken("$.src"), Is.Null);
        Assert.That(result.Data.SelectToken("$.dst")?.Value<string>(), Is.EqualTo("moved"));
    }

    // ── IfElse conditional branching ──────────────────────────────────────────

    [Test]
    public void Execute_IfElse_TrueCondition_RunsIfScript()
    {
        var data = JToken.Parse("{}");
        const string script = @"[
            {
                ""command"":  ""ifElse"",
                ""condition"": true,
                ""ifScript"":   [{ ""command"": ""put"", ""path"": ""$.branch"", ""value"": ""if"" }],
                ""elseScript"": [{ ""command"": ""put"", ""path"": ""$.branch"", ""value"": ""else"" }]
            }
        ]";
        var result = engine.Execute(script, data, context);
        Assert.That(result.Success, Is.True);
        Assert.That(result.Data.SelectToken("$.branch")?.Value<string>(), Is.EqualTo("if"));
    }

    [Test]
    public void Execute_IfElse_FalseCondition_RunsElseScript()
    {
        var data = JToken.Parse("{}");
        const string script = @"[
            {
                ""command"":  ""ifElse"",
                ""condition"": false,
                ""ifScript"":   [{ ""command"": ""put"", ""path"": ""$.branch"", ""value"": ""if"" }],
                ""elseScript"": [{ ""command"": ""put"", ""path"": ""$.branch"", ""value"": ""else"" }]
            }
        ]";
        var result = engine.Execute(script, data, context);
        Assert.That(result.Success, Is.True);
        Assert.That(result.Data.SelectToken("$.branch")?.Value<string>(), Is.EqualTo("else"));
    }

    [Test]
    public void Execute_IfElse_FetchConditionTrue_RunsIfScript()
    {
        var data = JToken.Parse(@"{ ""flag"": true }");
        const string script = @"[
            {
                ""command"":  ""ifElse"",
                ""condition"": ""=fetch($.flag)"",
                ""ifScript"":   [{ ""command"": ""put"", ""path"": ""$.result"", ""value"": ""yes"" }],
                ""elseScript"": [{ ""command"": ""put"", ""path"": ""$.result"", ""value"": ""no"" }]
            }
        ]";
        var result = engine.Execute(script, data, context);
        Assert.That(result.Success, Is.True);
        Assert.That(result.Data.SelectToken("$.result")?.Value<string>(), Is.EqualTo("yes"));
    }

    // ── Nested object creation ────────────────────────────────────────────────

    [Test]
    public void Execute_PutDeepPath_CreatesIntermediateObjects()
    {
        var data = JToken.Parse("{}");
        const string script = @"[
            { ""command"": ""put"", ""path"": ""$.person.address.city"", ""value"": ""Amsterdam"" }
        ]";
        var result = engine.Execute(script, data, context);
        Assert.That(result.Success, Is.True);
        Assert.That(result.Data.SelectToken("$.person.address.city")?.Value<string>(), Is.EqualTo("Amsterdam"));
    }

    // ── Array manipulation ────────────────────────────────────────────────────

    [Test]
    public void Execute_AddToArray_AppendsElements()
    {
        // To append to an existing array, target the array as the parent using a
        // sub-path (e.g. "$.items.el"). When the selected parent IS an array,
        // Add appends the value rather than setting a named property.
        var data = JToken.Parse(@"{ ""items"": [] }");
        const string script = @"[
            { ""command"": ""add"", ""path"": ""$.items.el"", ""value"": ""first""  },
            { ""command"": ""add"", ""path"": ""$.items.el"", ""value"": ""second"" }
        ]";
        var result = engine.Execute(script, data, context);
        Assert.That(result.Success, Is.True);
        var arr = result.Data.SelectToken("$.items") as JArray;
        Assert.That(arr!.Count, Is.EqualTo(2));
        Assert.That(arr[0].Value<string>(), Is.EqualTo("first"));
        Assert.That(arr[1].Value<string>(), Is.EqualTo("second"));
    }

    // ── Recursive-descent path ────────────────────────────────────────────────

    [Test]
    public void Execute_RecursiveDescentSet_UpdatesAllMatchingLeaves()
    {
        var data = JToken.Parse(@"{
            ""a"": { ""score"": 1 },
            ""b"": { ""score"": 2 }
        }");
        const string script = @"[{ ""command"": ""set"", ""path"": ""$..score"", ""value"": 0 }]";
        var result = engine.Execute(script, data, context);
        Assert.That(result.Success, Is.True);
        Assert.That(result.Data.SelectToken("$.a.score")?.Value<int>(), Is.EqualTo(0));
        Assert.That(result.Data.SelectToken("$.b.score")?.Value<int>(), Is.EqualTo(0));
    }

    // ── Full multi-command transformation ─────────────────────────────────────

    [Test]
    public void Execute_FullTransformation_CombinesMultipleCommandTypes()
    {
        var data = JToken.Parse(@"{
            ""name"": ""original"",
            ""temp"": ""discard"",
            ""source"": ""to-copy""
        }");
        const string script = @"[
            { ""command"": ""set"",    ""path"": ""$.name"",   ""value"": ""updated"" },
            { ""command"": ""remove"", ""path"": ""$.temp""  },
            { ""command"": ""copy"",   ""fromPath"": ""$.source"", ""toPath"": ""$.dest"" },
            { ""command"": ""put"",    ""path"": ""$.status"", ""value"": ""done"" }
        ]";
        var result = engine.Execute(script, data, context);
        Assert.That(result.Success, Is.True);
        Assert.That(result.Data.SelectToken("$.name")?.Value<string>(), Is.EqualTo("updated"));
        Assert.That(result.Data.SelectToken("$.temp"), Is.Null);
        Assert.That(result.Data.SelectToken("$.dest")?.Value<string>(), Is.EqualTo("to-copy"));
        Assert.That(result.Data.SelectToken("$.status")?.Value<string>(), Is.EqualTo("done"));
    }
}
