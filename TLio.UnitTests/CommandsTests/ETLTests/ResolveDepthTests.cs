using Microsoft.Extensions.Logging;
using Newtonsoft.Json.Linq;
using NUnit.Framework;
using TLio.Core.Contracts;
using TLio.Core.Models;
using TLio.Extensions.ETL.Commands;
using TLio.Extensions.ETL.Commands.Models;
using TLio.Json;

namespace TLio.UnitTests.CommandsTests.ETLTests;

/// <summary>
/// Resolve is a lookup-and-enrich: for every node the path selects, find the entries in a
/// reference collection whose key matches, and write derived values back onto the target.
///
/// The parts that carry the risk are the ones where "how many matched" changes the shape of
/// what gets written — ResolveTypeBehavior — and multi-key matching, where every key must agree
/// before an entry counts as a match.
/// </summary>
[TestFixture]
public class ResolveDepthTests
{
    private IExecutionContext<JToken> _context = null!;

    [SetUp]
    public void SetUp() => _context = JsonExecutionContext.CreateDefault();

    private bool Warned() => _context.GetLogEntries().Any(e => e.Level >= LogLevel.Warning);

    /// <summary>One rule: match targets to refs on a single key, then write one value.</summary>
    private static Resolve<JToken> Rule(
        string path,
        string keyPath,
        string referenceKeyPath,
        string referencesPath,
        string targetPath,
        IFunctionSupportedValue<JToken> value,
        ResolveTypeBehavior behavior = ResolveTypeBehavior.DependingOnResult) =>
        new()
        {
            Path = path,
            ResolveSettings =
            {
                new ResolveSetting<JToken>
                {
                    ReferencesCollectionPath = referencesPath,
                    ResolveKeys = { new ResolveKey { KeyPath = keyPath, ReferenceKeyPath = referenceKeyPath } },
                    Values =
                    {
                        new ResolveValue<JToken>
                        {
                            TargetPath = targetPath,
                            Value = value,
                            ResolveTypeBehavior = behavior
                        }
                    }
                }
            }
        };

    private static FixedValue<JToken> Fixed(string json) => new(JToken.Parse(json));

    // ── Matching ──────────────────────────────────────────────────────────────

    [Test]
    public void EveryMatchingTargetIsEnriched()
    {
        var data = JToken.Parse("""
            {
              "items": [ {"code":"A"}, {"code":"B"} ],
              "refs":  [ {"code":"A","label":"Alpha"}, {"code":"B","label":"Beta"} ]
            }
            """);

        var result = Rule("$.items[*]", "@.code", "@.code", "$.refs[*]", "@.label", Fixed("\"matched\""))
            .Execute(data, _context);

        Assert.That(result.Success, Is.True);
        Assert.That(data.SelectToken("$.items[0].label")!.Value<string>(), Is.EqualTo("matched"));
        Assert.That(data.SelectToken("$.items[1].label")!.Value<string>(), Is.EqualTo("matched"));
    }

    [Test]
    public void OnlyMatchingTargetsAreTouched()
    {
        var data = JToken.Parse("""
            {
              "items": [ {"code":"A"}, {"code":"ZZZ"} ],
              "refs":  [ {"code":"A","label":"Alpha"} ]
            }
            """);

        Rule("$.items[*]", "@.code", "@.code", "$.refs[*]", "@.label", Fixed("\"matched\""))
            .Execute(data, _context);

        Assert.That(data.SelectToken("$.items[0].label"), Is.Not.Null);
        Assert.That(data.SelectToken("$.items[1].label"), Is.Null,
            "an unmatched target is left exactly as it was");
    }

    [Test]
    public void MatchingIsByValue_NotByPosition()
    {
        var data = JToken.Parse("""
            {
              "items": [ {"code":"B"} ],
              "refs":  [ {"code":"A","label":"Alpha"}, {"code":"B","label":"Beta"} ]
            }
            """);

        Rule("$.items[*]", "@.code", "@.code", "$.refs[*]", "@.found", Fixed("true"))
            .Execute(data, _context);

        Assert.That(data.SelectToken("$.items[0].found")!.Value<bool>(), Is.True,
            "the second reference matched the first target");
    }

    [Test]
    public void AnAbsoluteKeyPathIsEvaluatedAgainstTheTargetNode_NotTheDocumentRoot()
    {
        // Documented, not endorsed. Resolve.GetValues sends a non-'@.' key path to
        // SelectNodes(path, token) — with the TARGET as the root — so '$.wanted' looks for a
        // 'wanted' property inside $.items[0], not at the document root. That is the opposite
        // of the framework's stated rule that function paths resolve against the document root
        // (TLio_AI_Reference.md, "Function path resolution uses document ROOT"), so a key path
        // that reads naturally as a document lookup silently matches nothing.
        var data = JToken.Parse("""
            {
              "wanted": "B",
              "items": [ {"n":1} ],
              "refs":  [ {"code":"A"}, {"code":"B"} ]
            }
            """);

        var result = Rule("$.items[*]", "$.wanted", "@.code", "$.refs[*]", "@.hit", Fixed("true"))
            .Execute(data, _context);

        Assert.That(result.Success, Is.True, "it is a silent non-match, not an error");
        Assert.That(data.SelectToken("$.items[0].hit"), Is.Null,
            "if this ever resolves, absolute key paths have been rooted at the document");
    }

    [Test]
    public void AnAbsoluteKeyPathResolvesWhenTheKeyLivesOnTheTargetItself()
    {
        // The corollary of the above: '$.code' works, because '$' is the target node.
        var data = JToken.Parse("""
            {
              "items": [ {"code":"B"} ],
              "refs":  [ {"code":"A"}, {"code":"B"} ]
            }
            """);

        Rule("$.items[*]", "$.code", "@.code", "$.refs[*]", "@.hit", Fixed("true"))
            .Execute(data, _context);

        Assert.That(data.SelectToken("$.items[0].hit")!.Value<bool>(), Is.True);
    }

    // ── Multi-key matching: every key must agree ──────────────────────────────

    private static Resolve<JToken> TwoKeyRule(ResolveTypeBehavior behavior =
        ResolveTypeBehavior.DependingOnResult) => new()
    {
        Path = "$.items[*]",
        ResolveSettings =
        {
            new ResolveSetting<JToken>
            {
                ReferencesCollectionPath = "$.refs[*]",
                ResolveKeys =
                {
                    new ResolveKey { KeyPath = "@.code",   ReferenceKeyPath = "@.code" },
                    new ResolveKey { KeyPath = "@.region", ReferenceKeyPath = "@.region" }
                },
                Values =
                {
                    new ResolveValue<JToken>
                    {
                        TargetPath = "@.hit",
                        Value = new FixedValue<JToken>(JToken.Parse("true")),
                        ResolveTypeBehavior = behavior
                    }
                }
            }
        }
    };

    [Test]
    public void AllKeysMustMatch_NotJustTheFirst()
    {
        var data = JToken.Parse("""
            {
              "items": [ {"code":"A","region":"NL"} ],
              "refs":  [ {"code":"A","region":"BE"} ]
            }
            """);

        TwoKeyRule().Execute(data, _context);

        Assert.That(data.SelectToken("$.items[0].hit"), Is.Null,
            "the code agreed but the region did not — a partial match is not a match");
    }

    [Test]
    public void AllKeysMatching_Resolves()
    {
        var data = JToken.Parse("""
            {
              "items": [ {"code":"A","region":"NL"} ],
              "refs":  [ {"code":"A","region":"BE"}, {"code":"A","region":"NL"} ]
            }
            """);

        TwoKeyRule().Execute(data, _context);

        Assert.That(data.SelectToken("$.items[0].hit")!.Value<bool>(), Is.True);
    }

    // ── ResolveTypeBehavior decides the written shape ─────────────────────────

    [Test]
    public void AlwaysAsArray_WritesAnArrayEvenForOneMatch()
    {
        var data = JToken.Parse("""
            {
              "items": [ {"code":"A"} ],
              "refs":  [ {"code":"A","label":"Alpha"} ]
            }
            """);

        Rule("$.items[*]", "@.code", "@.code", "$.refs[*]", "@.labels",
            Fixed("\"x\""), ResolveTypeBehavior.AlwaysAsArray).Execute(data, _context);

        Assert.That(data.SelectToken("$.items[0].labels")!.Type, Is.EqualTo(JTokenType.Array),
            "a caller that always expects a list gets one, so it never has to branch on the count");
    }

    [Test]
    public void AlwaysAsArray_WritesAnEmptyArrayWhenNothingMatched()
    {
        var data = JToken.Parse("""
            {
              "items": [ {"code":"ZZZ"} ],
              "refs":  [ {"code":"A","label":"Alpha"} ]
            }
            """);

        Rule("$.items[*]", "@.code", "@.code", "$.refs[*]", "@.labels",
            Fixed("\"x\""), ResolveTypeBehavior.AlwaysAsArray).Execute(data, _context);

        var written = data.SelectToken("$.items[0].labels");
        Assert.That(written, Is.Not.Null, "the property is written even with no matches");
        Assert.That(written!.Type, Is.EqualTo(JTokenType.Array));
        Assert.That(written.Count(), Is.EqualTo(0));
    }

    [Test]
    public void DependingOnResult_WritesNothingWhenNothingMatched()
    {
        var data = JToken.Parse("""
            {
              "items": [ {"code":"ZZZ"} ],
              "refs":  [ {"code":"A"} ]
            }
            """);

        Rule("$.items[*]", "@.code", "@.code", "$.refs[*]", "@.hit",
            Fixed("true"), ResolveTypeBehavior.DependingOnResult).Execute(data, _context);

        Assert.That(data.SelectToken("$.items[0].hit"), Is.Null,
            "unlike AlwaysAsArray, the default behaviour leaves the target untouched");
    }

    [Test]
    public void MultipleReferenceMatches_AreAllRepresented()
    {
        var data = JToken.Parse("""
            {
              "items": [ {"code":"A"} ],
              "refs":  [ {"code":"A","label":"one"}, {"code":"A","label":"two"} ]
            }
            """);

        Rule("$.items[*]", "@.code", "@.code", "$.refs[*]", "@.labels",
            Fixed("\"x\""), ResolveTypeBehavior.AlwaysAsArray).Execute(data, _context);

        Assert.That(data.SelectToken("$.items[0].labels")!.Count(), Is.EqualTo(2),
            "two references matched, so two entries are written");
    }

    // ── Missing and malformed inputs ──────────────────────────────────────────

    [Test]
    public void AMissingReferenceCollection_WarnsAndDoesNotThrow()
    {
        var data = JToken.Parse("""{ "items": [ {"code":"A"} ] }""");

        TLioExecutionResult<JToken>? result = null;
        Assert.That(() => result = Rule("$.items[*]", "@.code", "@.code", "$.nowhere[*]",
            "@.hit", Fixed("true")).Execute(data, _context), Throws.Nothing);

        Assert.That(result!.Success, Is.True, "a missing reference collection is a no-op, not a failure");
        Assert.That(data.SelectToken("$.items[0].hit"), Is.Null);
        Assert.That(Warned(), Is.True);
    }

    [Test]
    public void AnEmptyReferenceCollection_ResolvesNothing()
    {
        var data = JToken.Parse("""{ "items": [ {"code":"A"} ], "refs": [] }""");

        var result = Rule("$.items[*]", "@.code", "@.code", "$.refs[*]", "@.hit", Fixed("true"))
            .Execute(data, _context);

        Assert.That(result.Success, Is.True);
        Assert.That(data.SelectToken("$.items[0].hit"), Is.Null);
    }

    [Test]
    public void APathThatSelectsNoTargets_IsANoOp()
    {
        var data = JToken.Parse("""{ "items": [], "refs": [ {"code":"A"} ] }""");
        var before = data.DeepClone();

        var result = Rule("$.items[*]", "@.code", "@.code", "$.refs[*]", "@.hit", Fixed("true"))
            .Execute(data, _context);

        Assert.That(result.Success, Is.True);
        Assert.That(JToken.DeepEquals(data, before), Is.True);
    }

    [Test]
    public void ATargetMissingTheKeyProperty_DoesNotMatchAndDoesNotThrow()
    {
        var data = JToken.Parse("""
            {
              "items": [ {"other":1} ],
              "refs":  [ {"code":"A"} ]
            }
            """);

        TLioExecutionResult<JToken>? result = null;
        Assert.That(() => result = Rule("$.items[*]", "@.code", "@.code", "$.refs[*]",
            "@.hit", Fixed("true")).Execute(data, _context), Throws.Nothing);

        Assert.That(data.SelectToken("$.items[0].hit"), Is.Null);
    }

    [Test]
    public void AReferenceMissingTheKeyProperty_IsSkipped()
    {
        var data = JToken.Parse("""
            {
              "items": [ {"code":"A"} ],
              "refs":  [ {"noCode":1}, {"code":"A","label":"Alpha"} ]
            }
            """);

        Rule("$.items[*]", "@.code", "@.code", "$.refs[*]", "@.hit", Fixed("true"))
            .Execute(data, _context);

        Assert.That(data.SelectToken("$.items[0].hit")!.Value<bool>(), Is.True,
            "the usable reference still matched");
    }

    [Test]
    public void KeysAreComparedByValue_SoTypesMustAgree()
    {
        var data = JToken.Parse("""
            {
              "items": [ {"code":1} ],
              "refs":  [ {"code":"1"} ]
            }
            """);

        Rule("$.items[*]", "@.code", "@.code", "$.refs[*]", "@.hit", Fixed("true"))
            .Execute(data, _context);

        Assert.That(data.SelectToken("$.items[0].hit"), Is.Null,
            "a number and the string spelling of it are different keys");
    }

    // ── Several rules in one command ──────────────────────────────────────────

    [Test]
    public void TwoResolveSettings_BothApply()
    {
        var data = JToken.Parse("""
            {
              "items":  [ {"code":"A","region":"NL"} ],
              "codes":  [ {"code":"A","label":"Alpha"} ],
              "regions":[ {"region":"NL","name":"Netherlands"} ]
            }
            """);

        var cmd = new Resolve<JToken>
        {
            Path = "$.items[*]",
            ResolveSettings =
            {
                new ResolveSetting<JToken>
                {
                    ReferencesCollectionPath = "$.codes[*]",
                    ResolveKeys = { new ResolveKey { KeyPath = "@.code", ReferenceKeyPath = "@.code" } },
                    Values = { new ResolveValue<JToken> { TargetPath = "@.codeHit", Value = Fixed("true") } }
                },
                new ResolveSetting<JToken>
                {
                    ReferencesCollectionPath = "$.regions[*]",
                    ResolveKeys = { new ResolveKey { KeyPath = "@.region", ReferenceKeyPath = "@.region" } },
                    Values = { new ResolveValue<JToken> { TargetPath = "@.regionHit", Value = Fixed("true") } }
                }
            }
        };

        var result = cmd.Execute(data, _context);

        Assert.That(result.Success, Is.True);
        Assert.That(data.SelectToken("$.items[0].codeHit")!.Value<bool>(), Is.True);
        Assert.That(data.SelectToken("$.items[0].regionHit")!.Value<bool>(), Is.True);
    }

    [Test]
    public void TwoValuesInOneSetting_AreBothWritten()
    {
        var data = JToken.Parse("""
            {
              "items": [ {"code":"A"} ],
              "refs":  [ {"code":"A","label":"Alpha"} ]
            }
            """);

        var cmd = new Resolve<JToken>
        {
            Path = "$.items[*]",
            ResolveSettings =
            {
                new ResolveSetting<JToken>
                {
                    ReferencesCollectionPath = "$.refs[*]",
                    ResolveKeys = { new ResolveKey { KeyPath = "@.code", ReferenceKeyPath = "@.code" } },
                    Values =
                    {
                        new ResolveValue<JToken> { TargetPath = "@.first",  Value = Fixed("1") },
                        new ResolveValue<JToken> { TargetPath = "@.second", Value = Fixed("2") }
                    }
                }
            }
        };

        cmd.Execute(data, _context);

        Assert.That(data.SelectToken("$.items[0].first")!.Value<int>(), Is.EqualTo(1));
        Assert.That(data.SelectToken("$.items[0].second")!.Value<int>(), Is.EqualTo(2));
    }

    // ── Nested targets ────────────────────────────────────────────────────────

    [Test]
    public void ADeepTargetPathCreatesTheIntermediateStructure()
    {
        var data = JToken.Parse("""
            {
              "items": [ {"code":"A"} ],
              "refs":  [ {"code":"A"} ]
            }
            """);

        Rule("$.items[*]", "@.code", "@.code", "$.refs[*]", "@.meta.resolved", Fixed("true"))
            .Execute(data, _context);

        Assert.That(data.SelectToken("$.items[0].meta.resolved")!.Value<bool>(), Is.True);
    }

    [Test]
    public void AnExistingTargetValueIsOverwritten()
    {
        var data = JToken.Parse("""
            {
              "items": [ {"code":"A","hit":"old"} ],
              "refs":  [ {"code":"A"} ]
            }
            """);

        Rule("$.items[*]", "@.code", "@.code", "$.refs[*]", "@.hit", Fixed("\"new\""))
            .Execute(data, _context);

        Assert.That(data.SelectToken("$.items[0].hit")!.Value<string>(), Is.EqualTo("new"));
    }

    // ── Trace ─────────────────────────────────────────────────────────────────

    [Test]
    public void ASuccessfulResolveLogsInformation()
    {
        var data = JToken.Parse("""
            {
              "items": [ {"code":"A"} ],
              "refs":  [ {"code":"A"} ]
            }
            """);

        Rule("$.items[*]", "@.code", "@.code", "$.refs[*]", "@.hit", Fixed("true"))
            .Execute(data, _context);

        Assert.That(_context.GetLogEntries().Any(e => e.Level == LogLevel.Information), Is.True);
    }
}
