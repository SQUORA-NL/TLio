using Microsoft.Extensions.Logging;
using Newtonsoft.Json.Linq;
using NUnit.Framework;
using TLio.Client;
using TLio.Core.Contracts;
using TLio.Core.Models;
using TLio.Extensions.ETL;
using TLio.Extensions.ETL.Commands;
using TLio.Extensions.ETL.Commands.Models;
using TLio.Json;

namespace TLio.UnitTests.CommandsTests.ETLTests;

/// <summary>
/// A <c>targetPath</c> written as a function expression ("=...") computes the property name to
/// write from the matched reference entry, instead of naming it literally — the change that lets
/// a mapping table (code -&gt; target property name) drive a resolve without one command or
/// expression per property.
///
/// Direct C# construction exercises <see cref="ResolveValue{JToken}.TargetPathExpression"/>
/// itself; the JSON-script tests exercise the parser wiring in
/// <c>CommandConverter.ParseResolveSettings</c> that sets it from a <c>"targetPath": "=..."</c>
/// string. The ordinary "@.property" form is untouched by any of this — see
/// <see cref="ResolveDepthTests"/> and <see cref="ResolveTests"/> for that path.
/// </summary>
[TestFixture]
public class ResolveDynamicTargetPathTests
{
    private IExecutionContext<JToken> _context = null!;

    [SetUp]
    public void SetUp() => _context = JsonExecutionContext.CreateDefault();

    // ── Direct construction (TargetPathExpression set explicitly) ──────────────

    [Test]
    public void DynamicTargetPath_WritesUnderThePropertyNameTheMatchNames()
    {
        var data = JToken.Parse("""
            { "node": { "_code": "A" },
              "table": [ { "code": "A", "to": "renamedProp", "from": "renamedValue" } ] }
            """);

        var cmd = new Resolve<JToken>
        {
            Path = "$.node",
            ResolveSettings =
            {
                new ResolveSetting<JToken>
                {
                    ReferencesCollectionPath = "$.table[*]",
                    ResolveKeys = { new ResolveKey { KeyPath = "@._code", ReferenceKeyPath = "@.code" } },
                    Values =
                    {
                        new ResolveValue<JToken>
                        {
                            TargetPath = "=fetch(@.to)",
                            TargetPathExpression = new PathValue<JToken>("@.to"),
                            Value = new PathValue<JToken>("@.from")
                        }
                    }
                }
            }
        };

        var result = cmd.Execute(data, _context);

        Assert.That(result.Success, Is.True);
        Assert.That(data.SelectToken("$.node.renamedProp")?.Value<string>(), Is.EqualTo("renamedValue"));
    }

    [Test]
    public void DynamicTargetPath_ZeroMatches_WarnsAndSkips_DoesNotThrow()
    {
        var data = JToken.Parse("""
            { "node": { "_code": "ZZZ" },
              "table": [ { "code": "A", "to": "renamedProp", "from": "x" } ] }
            """);

        var cmd = new Resolve<JToken>
        {
            Path = "$.node",
            ResolveSettings =
            {
                new ResolveSetting<JToken>
                {
                    ReferencesCollectionPath = "$.table[*]",
                    ResolveKeys = { new ResolveKey { KeyPath = "@._code", ReferenceKeyPath = "@.code" } },
                    Values =
                    {
                        new ResolveValue<JToken>
                        {
                            TargetPath = "=fetch(@.to)",
                            TargetPathExpression = new PathValue<JToken>("@.to"),
                            Value = new PathValue<JToken>("@.from")
                        }
                    }
                }
            }
        };

        TLioExecutionResult<JToken>? result = null;
        Assert.That(() => result = cmd.Execute(data, _context), Throws.Nothing);

        Assert.That(result!.Success, Is.True, "an unresolved dynamic name is a no-op, not a failure");
        Assert.That(((JObject)data.SelectToken("$.node")!).Properties().Select(p => p.Name),
            Is.EquivalentTo(new[] { "_code" }), "nothing was written");
        Assert.That(_context.GetLogEntries().Any(e => e.Level == LogLevel.Warning), Is.True);
    }

    [Test]
    public void DynamicTargetPath_MultipleMatches_WarnsAndSkips_DoesNotThrow()
    {
        var data = JToken.Parse("""
            { "node": { "_code": "A" },
              "table": [
                { "code": "A", "to": "first",  "from": "x" },
                { "code": "A", "to": "second", "from": "y" }
              ] }
            """);

        var cmd = new Resolve<JToken>
        {
            Path = "$.node",
            ResolveSettings =
            {
                new ResolveSetting<JToken>
                {
                    ReferencesCollectionPath = "$.table[*]",
                    ResolveKeys = { new ResolveKey { KeyPath = "@._code", ReferenceKeyPath = "@.code" } },
                    Values =
                    {
                        new ResolveValue<JToken>
                        {
                            TargetPath = "=fetch(@.to)",
                            TargetPathExpression = new PathValue<JToken>("@.to"),
                            Value = new PathValue<JToken>("@.from")
                        }
                    }
                }
            }
        };

        var result = cmd.Execute(data, _context);

        Assert.That(result.Success, Is.True);
        Assert.That(data.SelectToken("$.node.first"), Is.Null);
        Assert.That(data.SelectToken("$.node.second"), Is.Null);
        Assert.That(_context.GetLogEntries().Any(e => e.Level == LogLevel.Warning), Is.True);
    }

    [Test]
    public void OrdinaryAtPropertyTargetPath_IsUnaffectedWhenNoExpressionIsSet()
    {
        var data = JToken.Parse("""
            { "node": { "code": "A" }, "refs": [ { "code": "A" } ] }
            """);

        var cmd = new Resolve<JToken>
        {
            Path = "$.node",
            ResolveSettings =
            {
                new ResolveSetting<JToken>
                {
                    ReferencesCollectionPath = "$.refs[*]",
                    ResolveKeys = { new ResolveKey { KeyPath = "@.code", ReferenceKeyPath = "@.code" } },
                    Values = { new ResolveValue<JToken> { TargetPath = "@.hit", Value = new FixedValue<JToken>(JToken.Parse("true")) } }
                }
            }
        };

        cmd.Execute(data, _context);

        Assert.That(data.SelectToken("$.node.hit")!.Value<bool>(), Is.True);
    }

    // ── Through the JSON script parser (CommandConverter) ──────────────────────

    [Test]
    public void ParsedFromScript_TargetPathFunctionExpression_RenamesFromTheMatchedRow()
    {
        const string script = """
            [
              { "command": "resolve", "path": "$.node",
                "resolveSettings": [
                  { "referencesCollectionPath": "$.table[*]",
                    "resolveKeys": [ { "keyPath": "@._code", "referenceKeyPath": "@.code" } ],
                    "values": [
                      { "targetPath": "=fetch(@.to)", "value": "=fetch(@.from)" }
                    ] } ] }
            ]
            """;

        var data = JToken.Parse("""
            { "node": { "_code": "A" },
              "table": [ { "code": "A", "to": "renamedProp", "from": "renamedValue" } ] }
            """);

        var options = ParseOptions<JToken>.CreateDefault();
        options.CommandsProvider.RegisterETL<JToken>();
        var engine = new ScriptEngine<JToken>(options.CommandsProvider, options.FunctionsProvider);

        var result = engine.Execute(script, data, JsonExecutionContext.CreateDefault());

        Assert.That(result.Success, Is.True);
        Assert.That(result.Data.SelectToken("$.node.renamedProp")?.Value<string>(), Is.EqualTo("renamedValue"));
    }

    [Test]
    public void ParsedFromScript_PlainAtPropertyTargetPath_StillWorks()
    {
        const string script = """
            [
              { "command": "resolve", "path": "$.node",
                "resolveSettings": [
                  { "referencesCollectionPath": "$.refs[*]",
                    "resolveKeys": [ { "keyPath": "@.code", "referenceKeyPath": "@.code" } ],
                    "values": [ { "targetPath": "@.hit", "value": true } ] } ] }
            ]
            """;

        var data = JToken.Parse("""{ "node": { "code": "A" }, "refs": [ { "code": "A" } ] }""");

        var options = ParseOptions<JToken>.CreateDefault();
        options.CommandsProvider.RegisterETL<JToken>();
        var engine = new ScriptEngine<JToken>(options.CommandsProvider, options.FunctionsProvider);

        var result = engine.Execute(script, data, JsonExecutionContext.CreateDefault());

        Assert.That(result.Success, Is.True);
        Assert.That(result.Data.SelectToken("$.node.hit")?.Value<bool>(), Is.True);
    }
}
