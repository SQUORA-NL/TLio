using Newtonsoft.Json.Linq;
using NUnit.Framework;
using TLio.Client;
using TLio.Commands;
using TLio.Core.Models;
using TLio.Json;

namespace TLio.UnitTests.CommandsTests;

/// <summary>
/// Ported from JLio.UnitTests.CommandsTests.DecisionTableJsonParseTests.
/// Adaptations:
///   - NUnit classic API → Assert.That() constraint model (NUnit 4)
///   - JLio engine API → ScriptEngine&lt;JToken&gt; with ParseOptions
///
/// Verifies that DecisionTable commands can be round-tripped through the
/// JSON script parser: a JSON array containing a "decisionTable" command
/// must deserialize and execute correctly against a JToken data context.
/// </summary>
[TestFixture]
public class DecisionTableJsonParseTests
{
    private ScriptEngine<JToken> engine = null!;

    [SetUp]
    public void Setup()
    {
        var options = ParseOptions<JToken>.CreateDefault();
        engine = new ScriptEngine<JToken>(options.CommandsProvider, options.FunctionsProvider);
    }

    // ── Basic parse + execute ──────────────────────────────────────────────────

    [Test]
    public void CanParseAndExecuteSimpleDecisionTable()
    {
        const string script = @"[
          {
            ""command"": ""decisionTable"",
            ""path"": ""$"",
            ""config"": {
              ""inputs"":  [{ ""name"": ""status"", ""path"": ""@.status"" }],
              ""outputs"": [{ ""name"": ""label"",  ""path"": ""@.label""  }],
              ""rules"": [
                {
                  ""priority"": 0,
                  ""conditions"": { ""status"": ""=active"" },
                  ""results"":    { ""label"":  ""A"" }
                }
              ]
            }
          }
        ]";

        var data = JToken.Parse("{ \"status\": \"active\" }");
        var result = engine.Execute(script, data, JsonExecutionContext.CreateDefault());

        Assert.That(result.Success, Is.True);
        Assert.That(result.Data.SelectToken("$.label")?.Value<string>(), Is.EqualTo("A"));
    }

    [Test]
    public void CanParseDefaultResults()
    {
        const string script = @"[
          {
            ""command"": ""decisionTable"",
            ""path"": ""$"",
            ""config"": {
              ""inputs"":  [{ ""name"": ""s"", ""path"": ""@.s"" }],
              ""outputs"": [{ ""name"": ""r"", ""path"": ""@.r"" }],
              ""rules"": [],
              ""defaultResults"": { ""r"": ""default"" }
            }
          }
        ]";

        var data = JToken.Parse("{ \"s\": \"anything\" }");
        var result = engine.Execute(script, data, JsonExecutionContext.CreateDefault());

        Assert.That(result.Success, Is.True);
        Assert.That(result.Data.SelectToken("$.r")?.Value<string>(), Is.EqualTo("default"));
    }

    [Test]
    public void CanParseNumericConditions()
    {
        const string script = @"[
          {
            ""command"": ""decisionTable"",
            ""path"": ""$"",
            ""config"": {
              ""inputs"":  [{ ""name"": ""score"", ""path"": ""@.score"" }],
              ""outputs"": [{ ""name"": ""grade"", ""path"": ""@.grade"" }],
              ""rules"": [
                {
                  ""priority"": 0,
                  ""conditions"": { ""score"": "">=75"" },
                  ""results"":    { ""grade"": ""pass"" }
                },
                {
                  ""priority"": 1,
                  ""conditions"": { ""score"": ""<75"" },
                  ""results"":    { ""grade"": ""fail"" }
                }
              ]
            }
          }
        ]";

        var data1 = JToken.Parse("{ \"score\": 80 }");
        var r1 = engine.Execute(script, data1, JsonExecutionContext.CreateDefault());
        Assert.That(r1.Success, Is.True);
        Assert.That(r1.Data.SelectToken("$.grade")?.Value<string>(), Is.EqualTo("pass"));

        var data2 = JToken.Parse("{ \"score\": 50 }");
        var r2 = engine.Execute(script, data2, JsonExecutionContext.CreateDefault());
        Assert.That(r2.Success, Is.True);
        Assert.That(r2.Data.SelectToken("$.grade")?.Value<string>(), Is.EqualTo("fail"));
    }

    [Test]
    public void CanParseStrategyConfiguration()
    {
        const string script = @"[
          {
            ""command"": ""decisionTable"",
            ""path"": ""$"",
            ""config"": {
              ""inputs"":  [{ ""name"": ""v"", ""path"": ""@.v"" }],
              ""outputs"": [{ ""name"": ""o"", ""path"": ""@.o"" }],
              ""rules"": [
                {
                  ""priority"": 0,
                  ""conditions"": { ""v"": "">=0"" },
                  ""results"":    { ""o"": ""first"" }
                },
                {
                  ""priority"": 1,
                  ""conditions"": { ""v"": "">=0"" },
                  ""results"":    { ""o"": ""second"" }
                }
              ],
              ""strategy"": {
                ""mode"": ""allMatches"",
                ""conflictResolution"": ""lastWins""
              }
            }
          }
        ]";

        var data = JToken.Parse("{ \"v\": 5 }");
        var result = engine.Execute(script, data, JsonExecutionContext.CreateDefault());

        Assert.That(result.Success, Is.True);
        Assert.That(result.Data.SelectToken("$.o")?.Value<string>(), Is.EqualTo("second"));
    }

    [Test]
    public void CanParseMultipleTargetNodes()
    {
        const string script = @"[
          {
            ""command"": ""decisionTable"",
            ""path"": ""$.items[*]"",
            ""config"": {
              ""inputs"":  [{ ""name"": ""type"", ""path"": ""@.type"" }],
              ""outputs"": [{ ""name"": ""code"", ""path"": ""@.code"" }],
              ""rules"": [
                {
                  ""priority"": 0,
                  ""conditions"": { ""type"": ""=A"" },
                  ""results"":    { ""code"": ""alpha"" }
                },
                {
                  ""priority"": 1,
                  ""conditions"": { ""type"": ""=B"" },
                  ""results"":    { ""code"": ""beta"" }
                }
              ]
            }
          }
        ]";

        var data = JToken.Parse(@"
        {
            ""items"": [
                { ""type"": ""A"" },
                { ""type"": ""B"" }
            ]
        }");

        var result = engine.Execute(script, data, JsonExecutionContext.CreateDefault());

        Assert.That(result.Success, Is.True);
        Assert.That(result.Data.SelectToken("$.items[0].code")?.Value<string>(), Is.EqualTo("alpha"));
        Assert.That(result.Data.SelectToken("$.items[1].code")?.Value<string>(), Is.EqualTo("beta"));
    }

    // ── Unknown command still recognized ──────────────────────────────────────

    [Test]
    public void UnknownCommand_IsHandledGracefully()
    {
        const string script = @"[{ ""command"": ""decisionTable"", ""path"": null }]";

        var data = JToken.Parse("{}");
        // Should parse without throwing — path validation fails gracefully
        Assert.DoesNotThrow(() => engine.Execute(script, data, JsonExecutionContext.CreateDefault()));
    }

    // ── Combined with other commands ───────────────────────────────────────────

    [Test]
    public void CombinedScript_DecisionTableWithSetCommand()
    {
        const string script = @"[
          {
            ""command"": ""put"",
            ""path"": ""$.processed"",
            ""value"": true
          },
          {
            ""command"": ""decisionTable"",
            ""path"": ""$"",
            ""config"": {
              ""inputs"":  [{ ""name"": ""x"", ""path"": ""@.x"" }],
              ""outputs"": [{ ""name"": ""y"", ""path"": ""@.y"" }],
              ""rules"": [
                {
                  ""conditions"": { ""x"": ""=1"" },
                  ""results"":    { ""y"": ""one"" }
                }
              ]
            }
          }
        ]";

        var data = JToken.Parse("{ \"x\": 1 }");
        var result = engine.Execute(script, data, JsonExecutionContext.CreateDefault());

        Assert.That(result.Success, Is.True);
        Assert.That(result.Data.SelectToken("$.processed")?.Value<bool>(), Is.True);
        Assert.That(result.Data.SelectToken("$.y")?.Value<string>(), Is.EqualTo("one"));
    }
}
