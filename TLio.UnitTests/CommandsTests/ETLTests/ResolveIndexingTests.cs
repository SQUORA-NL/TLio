using Newtonsoft.Json.Linq;
using NUnit.Framework;
using TLio.Core.Contracts;
using TLio.Core.Models;
using TLio.Extensions.ETL.Commands;
using TLio.Extensions.ETL.Commands.Models;
using TLio.Json;

namespace TLio.UnitTests.CommandsTests.ETLTests;

/// <summary>
/// Resolve's join used to be a linear scan of the reference collection per target — O(targets ×
/// references). It is now indexed once per setting and reused for every target. These tests
/// exercise the paths a hash-bucket index can get wrong if it is not careful: many candidates
/// sharing a bucket, type-sensitive equality across a large table (so a coarse bucket key cannot
/// produce a false positive), an array-based ("[*]") key — which is deliberately excluded from
/// indexing and must still fall back to an exact scan — and a large reference collection, so the
/// join is exercised at a scale a full scan would make slow.
/// </summary>
[TestFixture]
public class ResolveIndexingTests
{
    private IExecutionContext<JToken> _context = null!;

    [SetUp]
    public void SetUp() => _context = JsonExecutionContext.CreateDefault();

    private static Resolve<JToken> Rule(
        string path, string keyPath, string referenceKeyPath, string referencesPath,
        string targetPath, IFunctionSupportedValue<JToken> value,
        ResolveTypeBehavior behavior = ResolveTypeBehavior.DependingOnResult) => new()
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
                    new ResolveValue<JToken> { TargetPath = targetPath, Value = value, ResolveTypeBehavior = behavior }
                }
            }
        }
    };

    [Test]
    public void EachTargetFindsItsOwnMatch_AmongManyReferences()
    {
        var refs = new JArray();
        for (var i = 0; i < 5000; i++)
            refs.Add(JObject.Parse($$"""{ "code": "C{{i}}", "label": "label-{{i}}" }"""));

        var items = new JArray(
            JObject.Parse("""{ "code": "C1"    }"""),
            JObject.Parse("""{ "code": "C2500" }"""),
            JObject.Parse("""{ "code": "C4999" }"""),
            JObject.Parse("""{ "code": "C-nope" }"""));

        var data = new JObject { ["items"] = items, ["refs"] = refs };

        Rule("$.items[*]", "@.code", "@.code", "$.refs[*]", "@.label", new PathValue<JToken>("@.label"))
            .Execute(data, _context);

        Assert.That(data.SelectToken("$.items[0].label")!.Value<string>(), Is.EqualTo("label-1"));
        Assert.That(data.SelectToken("$.items[1].label")!.Value<string>(), Is.EqualTo("label-2500"));
        Assert.That(data.SelectToken("$.items[2].label")!.Value<string>(), Is.EqualTo("label-4999"));
        Assert.That(data.SelectToken("$.items[3].label"), Is.Null, "not present in the reference table");
    }

    [Test]
    public void TypeMismatch_StillDoesNotMatch_AmongManyCandidates()
    {
        // Every reference's code is the NUMBER 1 — same normalized-string bucket a target key
        // of "1" (a string) would also land in, if the bucket key were the only equality check.
        // A coarse/colliding bucket must never turn into a false positive.
        var refs = new JArray();
        for (var i = 0; i < 500; i++)
            refs.Add(JObject.Parse($$"""{ "code": 1, "seq": {{i}} }"""));

        var data = JObject.Parse("""{ "items": [ { "code": "1" } ] }""");
        ((JObject)data).Add("refs", refs);

        Rule("$.items[*]", "@.code", "@.code", "$.refs[*]", "@.hit", new FixedValue<JToken>(JToken.Parse("true")))
            .Execute(data, _context);

        Assert.That(data.SelectToken("$.items[0].hit"), Is.Null,
            "a number and the string spelling of it are different keys, however many candidates share the bucket");
    }

    [Test]
    public void ManyReferencesShareOneKey_AllAreReturned()
    {
        var refs = new JArray();
        for (var i = 0; i < 50; i++)
            refs.Add(JObject.Parse($$"""{ "code": "A", "seq": {{i}} }"""));

        var data = JObject.Parse("""{ "items": [ { "code": "A" } ] }""");
        ((JObject)data).Add("refs", refs);

        Rule("$.items[*]", "@.code", "@.code", "$.refs[*]", "@.seqs",
            new PathValue<JToken>("@.seq"), ResolveTypeBehavior.AlwaysAsArray).Execute(data, _context);

        Assert.That(data.SelectToken("$.items[0].seqs")!.Count(), Is.EqualTo(50),
            "every reference bucketed under the same key must still be found, not just the first");
    }

    private static Resolve<JToken> TwoKeyRule() => new()
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
                Values = { new ResolveValue<JToken> { TargetPath = "@.seq", Value = new PathValue<JToken>("@.seq") } }
            }
        }
    };

    [Test]
    public void CompositeKey_BothFieldsMustAgree_AtScale()
    {
        var refs = new JArray();
        var regions = new[] { "NL", "BE", "DE" };
        for (var i = 0; i < 1000; i++)
            refs.Add(JObject.Parse($$"""
                { "code": "C{{i}}", "region": "{{regions[i % 3]}}", "seq": {{i}} }
                """));

        // 242 % 3 == 2 -> "DE", so this is the single matching row.
        var matching = JObject.Parse("""{ "items": [ { "code": "C242", "region": "DE" } ] }""");
        matching.Add("refs", refs.DeepClone());
        TwoKeyRule().Execute(matching, _context);
        Assert.That(matching.SelectToken("$.items[0].seq")!.Value<int>(), Is.EqualTo(242));

        var mismatched = JObject.Parse("""{ "items": [ { "code": "C242", "region": "NL" } ] }""");
        mismatched.Add("refs", refs);
        TwoKeyRule().Execute(mismatched, _context);
        Assert.That(mismatched.SelectToken("$.items[0].seq"), Is.Null,
            "the code matched but the region did not — a partial match under a shared bucket key must still fail");
    }

    [Test]
    public void ArrayBasedKey_StillMatchesCorrectly_ViaExactScanFallback()
    {
        // "[*]" keys are excluded from the hash index by design (their extraction yields more
        // than one value) and always fall back to an exact scan — this is that fallback path.
        var data = JToken.Parse("""
            {
              "items": [ { "tags": ["red", "blue"] } ],
              "refs":  [
                { "tags": ["green"] },
                { "tags": ["blue", "yellow"] }
              ]
            }
            """);

        Rule("$.items[*]", "@.tags[*]", "@.tags[*]", "$.refs[*]", "@.hit", new FixedValue<JToken>(JToken.Parse("true")))
            .Execute(data, _context);

        // An array-based ("[*]") key always writes an array result (ResolveTypeBehavior's
        // arrayBased rule), one entry per matching reference — here, one.
        var hit = data.SelectToken("$.items[0].hit");
        Assert.That(hit, Is.Not.Null, "the second reference shares the 'blue' tag with the target");
        Assert.That(hit!.Count(), Is.EqualTo(1));
        Assert.That(hit![0]!.Value<bool>(), Is.True);
    }

    [Test]
    public void AReferenceMissingTheKeyProperty_IsSkipped_AmongManyReferences()
    {
        var refs = new JArray { JObject.Parse("""{ "noCode": 1 }""") };
        for (var i = 0; i < 500; i++)
            refs.Add(JObject.Parse($$"""{ "code": "C{{i}}" }"""));

        var data = JObject.Parse("""{ "items": [ { "code": "C1" } ] }""");
        ((JObject)data).Add("refs", refs);

        Rule("$.items[*]", "@.code", "@.code", "$.refs[*]", "@.hit", new FixedValue<JToken>(JToken.Parse("true")))
            .Execute(data, _context);

        Assert.That(data.SelectToken("$.items[0].hit")!.Value<bool>(), Is.True);
    }
}
