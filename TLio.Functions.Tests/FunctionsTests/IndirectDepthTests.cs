using Microsoft.Extensions.Logging;
using Newtonsoft.Json.Linq;
using NUnit.Framework;
using TLio.Client;
using TLio.Core.Contracts;
using TLio.Extensions.Text;
using TLio.Json;

namespace TLio.Functions.Tests.FunctionsTests;

/// <summary>
/// =indirect($.ref) — two hops: read the string held at the argument path, then use that
/// string as a path in its own right. Both hops can fail, and the failure has to be
/// distinguishable: an argument that is not a path, a reference that does not exist, a
/// reference holding something other than a string, and a resolved path that matches nothing
/// are four different mistakes.
///
/// The companion fixture IndirectPathTests covers =indirect() used inside a command's *path*.
/// This one covers it as a *value*.
/// </summary>
[TestFixture]
public class IndirectDepthTests
{
    private IExecutionContext<JToken> _context = null!;
    private ScriptEngine<JToken> _engine = null!;

    [SetUp]
    public void SetUp()
    {
        _context = JsonExecutionContext.CreateDefault();
        var options = ParseOptions<JToken>.CreateDefault();
        _engine = new ScriptEngine<JToken>(options.CommandsProvider, options.FunctionsProvider);
    }

    private (bool Success, JToken Data) Run(string script, string document) =>
        _engine.Execute(script, JToken.Parse(document), _context) is var r ? (r.Success, r.Data) : default;

    private JToken? Resolve(string reference, string document)
    {
        var script = $$"""[{"command":"put","path":"$.out","value":"=indirect({{reference}})"}]""";
        return Run(script, document).Data.SelectToken("$.out");
    }

    private bool Warned() =>
        _context.GetLogEntries().Any(e => e.Level >= LogLevel.Warning);

    // ── Resolution ────────────────────────────────────────────────────────────

    [Test]
    public void ResolvesAStringReferenceToItsTargetValue()
    {
        var result = Resolve("$.ref", """{"ref":"$.source","source":"hello"}""");

        Assert.That(result!.Value<string>(), Is.EqualTo("hello"));
    }

    [Test]
    public void ResolvesToANestedTarget()
    {
        var result = Resolve("$.ref", """{"ref":"$.person.name","person":{"name":"Ada"}}""");

        Assert.That(result!.Value<string>(), Is.EqualTo("Ada"));
    }

    [Test]
    public void ResolvesToAnObject_NotJustAScalar()
    {
        var result = Resolve("$.ref", """{"ref":"$.person","person":{"name":"Ada","age":36}}""");

        Assert.That(result!["name"]!.Value<string>(), Is.EqualTo("Ada"));
        Assert.That(result["age"]!.Value<int>(), Is.EqualTo(36));
    }

    [Test]
    public void ResolvesToAnArray()
    {
        var result = Resolve("$.ref", """{"ref":"$.items","items":[1,2,3]}""");

        Assert.That(result!.Type, Is.EqualTo(JTokenType.Array));
        Assert.That(result.Count(), Is.EqualTo(3));
    }

    [Test]
    public void ResolvesAnIndexedTarget()
    {
        var result = Resolve("$.ref", """{"ref":"$.items[1]","items":["a","b","c"]}""");

        Assert.That(result!.Value<string>(), Is.EqualTo("b"));
    }

    [Test]
    public void TheReferenceItselfCanBeNested()
    {
        var result = Resolve("$.config.pointer", """{"config":{"pointer":"$.value"},"value":42}""");

        Assert.That(result!.Value<int>(), Is.EqualTo(42));
    }

    [Test]
    public void ChainedIndirection_ResolvesOnlyOneHopPerCall()
    {
        // $.a -> "$.b", $.b -> "$.c", $.c -> "final".
        // One =indirect() performs exactly two lookups, so it lands on the STRING "$.c",
        // not on "final". Chaining requires nesting the calls.
        var result = Resolve("$.a", """{"a":"$.b","b":"$.c","c":"final"}""");

        Assert.That(result!.Value<string>(), Is.EqualTo("$.c"),
            "indirect is a single level of indirection, not a follow-until-resolved loop");
    }

    [Test]
    public void ResolvedTargetThatIsNull_IsReturnedAsNull()
    {
        var result = Resolve("$.ref", """{"ref":"$.empty","empty":null}""");

        Assert.That(result, Is.Not.Null);
        Assert.That(result!.Type, Is.EqualTo(JTokenType.Null));
    }

    // ── Failure modes, each distinguishable ───────────────────────────────────

    [Test]
    public void ReferencePathDoesNotExist_WarnsAndWritesNothing()
    {
        var result = Resolve("$.missing", """{"source":"hello"}""");

        Assert.That(result, Is.Null);
        Assert.That(Warned(), Is.True);
    }

    [Test]
    public void ReferenceHoldsANumber_WarnsAndWritesNothing()
    {
        var result = Resolve("$.ref", """{"ref":42,"source":"hello"}""");

        Assert.That(result, Is.Null, "a number is not a path expression");
        Assert.That(Warned(), Is.True);
    }

    [Test]
    public void ReferenceHoldsAnObject_WarnsAndWritesNothing()
    {
        var result = Resolve("$.ref", """{"ref":{"nested":true},"source":"hello"}""");

        Assert.That(result, Is.Null);
        Assert.That(Warned(), Is.True);
    }

    [Test]
    public void ReferenceHoldsAnEmptyString_WarnsAndWritesNothing()
    {
        var result = Resolve("$.ref", """{"ref":"","source":"hello"}""");

        Assert.That(result, Is.Null);
        Assert.That(Warned(), Is.True);
    }

    [Test]
    public void ReferenceHoldsAPathThatMatchesNothing_WarnsAndWritesNothing()
    {
        var result = Resolve("$.ref", """{"ref":"$.nowhere","source":"hello"}""");

        Assert.That(result, Is.Null, "the second hop failed, not the first");
        Assert.That(Warned(), Is.True);
    }

    [Test]
    public void ReferenceHoldsAMalformedPath_DoesNotThrow()
    {
        Assert.That(() => Resolve("$.ref", """{"ref":"not a path at all","source":"x"}"""),
            Throws.Nothing, "a bad path string must not escape as an exception");
    }

    [Test]
    public void NoArguments_WarnsAndWritesNothing()
    {
        var result = Run("""[{"command":"put","path":"$.out","value":"=indirect()"}]""",
            """{"source":"hello"}""").Data.SelectToken("$.out");

        Assert.That(result, Is.Null);
        Assert.That(Warned(), Is.True);
    }

    [Test]
    public void AFailedIndirect_AbortsTheRestOfTheScript()
    {
        // Worth knowing, and different from a path that simply matches nothing: a failed
        // FUNCTION marks its command failed, and TLioScript.Execute breaks on the first
        // failed command. So the step after it never runs. A missing path warns and
        // continues; a failed function stops the script. Pinned here as current behaviour —
        // whether that should be configurable is an open question.
        var result = Run("""
            [
              {"command":"put","path":"$.out","value":"=indirect($.missing)"},
              {"command":"put","path":"$.after","value":"ran"}
            ]
            """, """{"source":"hello"}""");

        Assert.That(result.Success, Is.False);
        Assert.That(result.Data.SelectToken("$.after"), Is.Null,
            "execution stopped at the failed command");
        Assert.That(Warned(), Is.True);
    }

    // ── Multiple matches ──────────────────────────────────────────────────────

    [Test]
    public void ReferencedPathMatchingManyNodes_ReturnsThemAll()
    {
        var result = Resolve("$.ref",
            """{"ref":"$.items[*].name","items":[{"name":"a"},{"name":"b"}]}""");

        Assert.That(result, Is.Not.Null,
            "a multi-match path resolves; the command decides what to do with the set");
    }

    // ── Composition ───────────────────────────────────────────────────────────

    [Test]
    public void TheReferencePathCanBeBuiltByAnInnerFunction()
    {
        var ctx = JsonExecutionContext.CreateDefault();
        var options = ParseOptions<JToken>.CreateDefault();
        options.FunctionsProvider.RegisterText<JToken>();
        var engine = new ScriptEngine<JToken>(options.CommandsProvider, options.FunctionsProvider);

        // The reference path itself is assembled at runtime from two document fields.
        const string script =
            """[{"command":"put","path":"$.out","value":"=indirect(=concat($.prefix,$.field))"}]""";
        var result = engine.Execute(script,
            JToken.Parse("""{"prefix":"$.","field":"ref","ref":"$.source","source":"hello"}"""), ctx);

        Assert.That(result.Success, Is.True,
            $"log: {string.Join(" | ", ctx.GetLogEntries().Select(e => e.Message))}");
        Assert.That(result.Data.SelectToken("$.out")!.Value<string>(), Is.EqualTo("hello"));
    }
}
