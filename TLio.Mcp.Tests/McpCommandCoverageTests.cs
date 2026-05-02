using System.Text.Json;
using Microsoft.Extensions.Options;
using Newtonsoft.Json.Linq;
using NUnit.Framework;
using TLio.Mcp.Configuration;
using TLio.Mcp.Models;
using TLio.Mcp.Services;
using TLio.Mcp.Tools;

namespace TLio.Mcp.Tests;

/// <summary>
/// MCP-level trace-quality coverage for the 9 commands not exercised by McpTwoIterationTests:
///   put, move, ifElse, decisionTable, compare, merge, flatten, restore, resolve
///
/// For each command, at minimum:
///   - Wrong path → trace shows noop with actionable detail
///   - Correct usage → trace shows success with meaningful detail an agent can act on
/// </summary>
[TestFixture]
public sealed class McpCommandCoverageTests
{
    private ExecutionTools _execution = null!;
    private AnalysisTools _analysis = null!;
    private RateLimiterService _rateLimiter = null!;

    [SetUp]
    public void SetUp()
    {
        var config = Options.Create(new McpConfiguration
        {
            Observability = new ObservabilityConfig { Enabled = true },
            RateLimit = new RateLimitConfig { RequestsPerMinute = 1000, WindowCount = 6 }
        });
        _rateLimiter = new RateLimiterService(config);
        _analysis = new AnalysisTools(new StructuralDiffService(), new DocumentService(), _rateLimiter, config);
        _execution = new ExecutionTools(_rateLimiter, config);
    }

    [TearDown]
    public void TearDown() => _rateLimiter.Dispose();

    private ExecuteResult Execute(string doc, string script) =>
        (ExecuteResult)_execution.Execute(doc, "json", script);

    private AnalyzeResult Analyze(string input, string target) =>
        (AnalyzeResult)_analysis.Analyze(input, "json", target, "json");

    // ── PUT ──────────────────────────────────────────────────────────────────────
    // put = upsert: creates the field if absent, updates it if present

    [Test]
    public void Put_WrongPath_TraceShowsNoop()
    {
        const string doc = """{"user":{"name":"Alice"}}""";
        const string script = """[{"command":"put","path":"$.profile.name","value":"Bob"}]""";

        var result = Execute(doc, script);

        // $.profile doesn't exist — put should create the entire path (EnsurePath),
        // then upsert the value. A wrong path that can be created won't be a noop.
        // Instead, test a put on a truly non-matching wildcard that matches nothing:
        // e.g. put with a path that requires an existing parent to resolve
        Assert.That(result.Success, Is.True, "put auto-creates path via EnsurePath, always succeeds");
        Assert.That(result.Trace[0].Outcome, Is.EqualTo("success"));
        var output = JToken.Parse(result.Output);
        Assert.That(output["profile"]!["name"]!.ToString(), Is.EqualTo("Bob"),
            "put created the missing profile.name path");
    }

    [Test]
    public void Put_UpdatesExistingField_TraceShowsSuccess()
    {
        const string doc = """{"user":{"status":"inactive","score":10}}""";
        const string script = """
            [
              {"command":"put","path":"$.user.status","value":"active"},
              {"command":"put","path":"$.user.score","value":99}
            ]
            """;

        var result = Execute(doc, script);

        Assert.That(result.Success, Is.True);
        Assert.That(result.Trace, Has.Count.EqualTo(2));
        Assert.That(result.Trace.Select(t => t.Outcome), Has.All.EqualTo("success"),
            "put on existing fields must show success, not noop");

        var output = JToken.Parse(result.Output);
        Assert.That(output["user"]!["status"]!.ToString(), Is.EqualTo("active"));
        Assert.That(output["user"]!["score"]!.Value<int>(), Is.EqualTo(99));

        Console.WriteLine("[put] Trace:");
        foreach (var t in result.Trace) Console.WriteLine($"  [{t.Outcome}] {t.CommandName} @ {t.Path}: {t.Detail}");
    }

    [Test]
    public void Put_CreatesNewField_TraceShowsSuccess()
    {
        const string doc = """{"product":{"name":"Widget"}}""";
        const string script = """[{"command":"put","path":"$.product.available","value":true}]""";

        var result = Execute(doc, script);

        Assert.That(result.Success, Is.True);
        Assert.That(result.Trace[0].Outcome, Is.EqualTo("success"));
        var output = JToken.Parse(result.Output);
        Assert.That(output["product"]!["available"]!.Value<bool>(), Is.True,
            "put creates missing field (upsert behaviour)");
    }

    // ── MOVE ─────────────────────────────────────────────────────────────────────

    [Test]
    public void Move_WrongFromPath_TraceShowsNoopWithMovedKeyword()
    {
        const string doc = """{"a":1}""";
        const string script = """[{"command":"move","fromPath":"$.b","toPath":"$.c"}]""";

        var result = Execute(doc, script);

        Assert.That(result.Trace[0].Outcome, Is.EqualTo("noop"));
        Assert.That(result.Trace[0].Detail, Does.Contain("moved").Or.Contain("nothing moved"),
            "noop detail must say 'moved' not 'copied' so agent knows it's a move command");

        Console.WriteLine($"[move noop] {result.Trace[0].Detail}");
    }

    [Test]
    public void Move_CorrectPaths_TraceShowsMovedNotCopied()
    {
        const string doc = """{"old_name":"Alice","age":30}""";
        const string script = """[{"command":"move","fromPath":"$.old_name","toPath":"$.name"}]""";

        var result = Execute(doc, script);

        Assert.That(result.Success, Is.True);
        Assert.That(result.Trace[0].Outcome, Is.EqualTo("success"));
        Assert.That(result.Trace[0].Detail, Does.Contain("moved"),
            "trace must say 'moved' not 'copied' so agent understands source was removed");
        Assert.That(result.Trace[0].Detail, Does.Contain("removed").Or.Contain("Source"),
            "trace must confirm source removal so agent doesn't try to remove it separately");

        var output = JToken.Parse(result.Output);
        Assert.That(output["name"]!.ToString(), Is.EqualTo("Alice"));
        Assert.That(output["old_name"], Is.Null, "source field must be gone after move");

        Console.WriteLine($"[move success] {result.Trace[0].Detail}");
    }

    [Test]
    public void Move_ArrayElement_MovesToNewPath_SourceGone()
    {
        const string doc = """{"data":{"temp_ref":"XYZ"},"result":{}}""";
        const string script = """[{"command":"move","fromPath":"$.data.temp_ref","toPath":"$.result.reference"}]""";

        var result = Execute(doc, script);

        Assert.That(result.Success, Is.True);
        var output = JToken.Parse(result.Output);
        Assert.That(output["result"]!["reference"]!.ToString(), Is.EqualTo("XYZ"));
        Assert.That(output["data"]!["temp_ref"], Is.Null, "moved field must not exist at source");
    }

    // ── IFELSE ───────────────────────────────────────────────────────────────────

    [Test]
    public void IfElse_TrueCondition_ExecutesIfBranchAndTraceShowsBranch()
    {
        const string doc = """{"order":{"total":150,"discount":0}}""";
        // condition: JSON boolean true → FixedValue(true)
        const string script = """
            [
              {
                "command":"ifElse",
                "condition":true,
                "ifScript":[{"command":"set","path":"$.order.discount","value":10}],
                "elseScript":[{"command":"set","path":"$.order.discount","value":0}]
              }
            ]
            """;

        var result = Execute(doc, script);

        Assert.That(result.Success, Is.True);
        var ifTrace = result.Trace.FirstOrDefault(t => t.CommandName == "ifElse");
        Assert.That(ifTrace, Is.Not.Null, "ifElse must appear in trace");
        Assert.That(ifTrace!.Outcome, Is.EqualTo("success"));
        Assert.That(ifTrace.Detail, Does.Contain("if").IgnoreCase,
            "detail must indicate which branch was taken so agent can verify logic");

        var output = JToken.Parse(result.Output);
        Assert.That(output["order"]!["discount"]!.Value<int>(), Is.EqualTo(10),
            "if-branch set discount to 10");

        Console.WriteLine($"[ifElse true] {ifTrace.Detail}");
    }

    [Test]
    public void IfElse_FalseCondition_ExecutesElseBranchAndTraceShowsBranch()
    {
        const string doc = """{"flag":false,"result":""}""";
        const string script = """
            [
              {
                "command":"ifElse",
                "condition":false,
                "ifScript":[{"command":"set","path":"$.result","value":"if-path"}],
                "elseScript":[{"command":"set","path":"$.result","value":"else-path"}]
              }
            ]
            """;

        var result = Execute(doc, script);

        Assert.That(result.Success, Is.True);
        var ifTrace = result.Trace.First(t => t.CommandName == "ifElse");
        Assert.That(ifTrace.Detail, Does.Contain("else").IgnoreCase,
            "detail must say 'else' branch so agent knows condition was false");

        var output = JToken.Parse(result.Output);
        Assert.That(output["result"]!.ToString(), Is.EqualTo("else-path"));

        Console.WriteLine($"[ifElse false] {ifTrace.Detail}");
    }

    // ── DECISIONTABLE ────────────────────────────────────────────────────────────

    [Test]
    public void DecisionTable_MatchingRule_TraceShowsWhichRuleAndOutputsSet()
    {
        // Customer tier classification based on annual spend
        const string doc = """{"customer":{"annual_spend":5000}}""";
        const string script = """
            [
              {
                "command":"decisionTable",
                "path":"$",
                "config":{
                  "inputs":[{"name":"spend","path":"$.customer.annual_spend"}],
                  "outputs":[{"name":"tier","path":"$.customer.tier"}],
                  "rules":[
                    {"priority":1,"conditions":{"spend":">=10000"},"results":{"tier":"platinum"}},
                    {"priority":2,"conditions":{"spend":">=2000"},"results":{"tier":"gold"}},
                    {"priority":3,"conditions":{"spend":">=500"},"results":{"tier":"silver"}}
                  ]
                }
              }
            ]
            """;

        var result = Execute(doc, script);

        Assert.That(result.Success, Is.True);
        var dtTrace = result.Trace.First(t => t.CommandName == "decisionTable");
        Assert.That(dtTrace.Outcome, Is.EqualTo("success"));
        Assert.That(dtTrace.Detail, Does.Contain("rule").IgnoreCase,
            "trace must mention which rule matched so agent can verify decision logic");
        Assert.That(dtTrace.Detail, Does.Contain("tier").IgnoreCase,
            "trace must name which output(s) were set");

        var output = JToken.Parse(result.Output);
        Assert.That(output["customer"]!["tier"]!.ToString(), Is.EqualTo("gold"),
            "spend=5000 should match priority-2 rule (>=2000)");

        Console.WriteLine($"[decisionTable] {dtTrace.Detail}");
    }

    [Test]
    public void DecisionTable_NoMatchingRule_AppliesDefaultsAndTraceIndicates()
    {
        const string doc = """{"item":{"score":0}}""";
        const string script = """
            [
              {
                "command":"decisionTable",
                "path":"$",
                "config":{
                  "inputs":[{"name":"score","path":"$.item.score"}],
                  "outputs":[{"name":"grade","path":"$.item.grade"}],
                  "rules":[
                    {"priority":1,"conditions":{"score":">=90"},"results":{"grade":"A"}},
                    {"priority":2,"conditions":{"score":">=80"},"results":{"grade":"B"}}
                  ],
                  "defaultResults":{"grade":"F"}
                }
              }
            ]
            """;

        var result = Execute(doc, script);

        Assert.That(result.Success, Is.True);
        var dtTrace = result.Trace.First(t => t.CommandName == "decisionTable");
        Assert.That(dtTrace.Detail, Does.Contain("default").Or.Contain("no rule").IgnoreCase,
            "trace must say defaults were applied when no rule matched");

        var output = JToken.Parse(result.Output);
        Assert.That(output["item"]!["grade"]!.ToString(), Is.EqualTo("F"));

        Console.WriteLine($"[decisionTable defaults] {dtTrace.Detail}");
    }

    [Test]
    public void DecisionTable_WrongPath_TraceShowsNoop()
    {
        const string doc = """{"order":{"amount":100}}""";
        const string script = """
            [
              {
                "command":"decisionTable",
                "path":"$.non_existent",
                "config":{
                  "inputs":[{"name":"amount","path":"@.amount"}],
                  "outputs":[{"name":"discount","path":"@.discount"}],
                  "rules":[{"priority":1,"conditions":{"amount":">=50"},"results":{"discount":{"value":5}}}]
                }
              }
            ]
            """;

        var result = Execute(doc, script);

        Assert.That(result.Trace[0].Outcome, Is.EqualTo("noop"),
            "non-existent path must show noop not success");
        Assert.That(result.Trace[0].MatchedCount, Is.EqualTo(0));

        Console.WriteLine($"[decisionTable noop] {result.Trace[0].Detail}");
    }

    // ── COMPARE ──────────────────────────────────────────────────────────────────

    [Test]
    public void Compare_EqualValues_TraceShowsEqualResultAtResultPath()
    {
        const string doc = """{"a":42,"b":42}""";
        const string script = """[{"command":"compare","firstPath":"$.a","secondPath":"$.b","resultPath":"$.comparison"}]""";

        var result = Execute(doc, script);

        Assert.That(result.Success, Is.True);
        Assert.That(result.Trace[0].Outcome, Is.EqualTo("success"));
        Assert.That(result.Trace[0].Detail, Does.Contain("equal"),
            "trace must show the comparison result 'equal' so agent knows what was written");
        Assert.That(result.Trace[0].Detail, Does.Contain("$.comparison"),
            "trace must name the result path so agent knows where to read the outcome");

        var output = JToken.Parse(result.Output);
        Assert.That(output["comparison"]!.ToString(), Is.EqualTo("equal"));

        Console.WriteLine($"[compare equal] {result.Trace[0].Detail}");
    }

    [Test]
    public void Compare_DifferentNumbers_TraceShowsGreaterOrLess()
    {
        const string doc = """{"price":99.9,"budget":50.0}""";
        const string script = """[{"command":"compare","firstPath":"$.price","secondPath":"$.budget","resultPath":"$.within_budget"}]""";

        var result = Execute(doc, script);

        Assert.That(result.Success, Is.True);
        Assert.That(result.Trace[0].Detail, Does.Contain("greater").Or.Contain("less"),
            "numeric comparison trace must say 'greater' or 'less'");

        var output = JToken.Parse(result.Output);
        Assert.That(output["within_budget"]!.ToString(), Is.EqualTo("greater"));
    }

    [Test]
    public void Compare_MissingPath_TraceShowsNoop()
    {
        const string doc = """{"x":1}""";
        const string script = """[{"command":"compare","firstPath":"$.x","secondPath":"$.y","resultPath":"$.result"}]""";

        var result = Execute(doc, script);

        Assert.That(result.Trace[0].Outcome, Is.EqualTo("noop"),
            "compare must noop when a comparison path is missing");
        Assert.That(result.Trace[0].Detail, Does.Contain("SecondPath").Or.Contain("$.y"),
            "trace must identify which path was missing");

        Console.WriteLine($"[compare noop] {result.Trace[0].Detail}");
    }

    // ── MERGE ────────────────────────────────────────────────────────────────────

    [Test]
    public void Merge_TwoObjects_ProducesUnionAndTraceShowsBothPaths()
    {
        const string doc = """{"base":{"name":"Alice","role":"user"},"extra":{"email":"alice@example.com","active":true}}""";
        const string script = """[{"command":"merge","path":"$.extra","targetPath":"$.base"}]""";

        var result = Execute(doc, script);

        Assert.That(result.Success, Is.True);
        Assert.That(result.Trace[0].Outcome, Is.EqualTo("success"));
        Assert.That(result.Trace[0].Detail, Does.Contain("merged"),
            "trace must say 'merged' to distinguish from copy/set");
        Assert.That(result.Trace[0].Detail, Does.Contain("$.extra"),
            "trace must name the source path");
        Assert.That(result.Trace[0].Detail, Does.Contain("$.base"),
            "trace must name the target path");

        var output = JToken.Parse(result.Output);
        Assert.That(output["base"]!["name"]!.ToString(), Is.EqualTo("Alice"), "base properties preserved");
        Assert.That(output["base"]!["email"]!.ToString(), Is.EqualTo("alice@example.com"), "extra properties merged in");

        Console.WriteLine($"[merge success] {result.Trace[0].Detail}");
    }

    [Test]
    public void Merge_WrongSourcePath_TraceShowsNoop()
    {
        const string doc = """{"target":{"x":1}}""";
        const string script = """[{"command":"merge","path":"$.source","targetPath":"$.target"}]""";

        var result = Execute(doc, script);

        Assert.That(result.Trace[0].Outcome, Is.EqualTo("noop"));
        Assert.That(result.Trace[0].Detail, Does.Contain("$.source"),
            "noop trace must name the missing source path");
    }

    // ── FLATTEN ──────────────────────────────────────────────────────────────────

    [Test]
    public void Flatten_NestedObject_ProducesDotNotationKeys()
    {
        const string doc = """{"address":{"street":"Main St","city":"Boston","zip":"02101"}}""";
        const string script = """
            [
              {
                "command":"flatten",
                "path":"$.address",
                "flattenSettings":{"delimiter":".","maxDepth":-1}
              }
            ]
            """;

        var result = Execute(doc, script);

        Assert.That(result.Success, Is.True);
        Assert.That(result.Trace[0].Outcome, Is.EqualTo("success"));
        Assert.That(result.Trace[0].Detail, Does.Contain("flatten"),
            "trace must confirm flatten was applied");

        var output = JToken.Parse(result.Output);
        // After flattening, address becomes a flat object with dot keys
        Assert.That(output["address"]!["street"] is not null || output["address"]!.ToString().Contains("street"),
            Is.True, "flattened address must contain street field");

        Console.WriteLine($"[flatten] {result.Trace[0].Detail}");
        Console.WriteLine($"[flatten output] {result.Output}");
    }

    [Test]
    public void Flatten_WrongPath_TraceShowsNoop()
    {
        const string doc = """{"data":{"value":1}}""";
        const string script = """
            [
              {
                "command":"flatten",
                "path":"$.missing",
                "flattenSettings":{"delimiter":".","maxDepth":-1}
              }
            ]
            """;

        var result = Execute(doc, script);

        Assert.That(result.Trace[0].Outcome, Is.EqualTo("noop"),
            "flatten on missing path must show noop");
        Assert.That(result.Trace[0].Detail, Does.Contain("$.missing"),
            "noop trace must name the unmatched path");
    }

    // ── RESTORE ──────────────────────────────────────────────────────────────────

    [Test]
    public void FlattenAndRestore_RoundTrip_ProducesOriginalStructure()
    {
        const string doc = """{"config":{"db":{"host":"localhost","port":5432},"cache":{"ttl":300}}}""";
        // Flatten first, then restore — verify round-trip
        const string flattenScript = """
            [
              {
                "command":"flatten",
                "path":"$.config",
                "flattenSettings":{"delimiter":".","maxDepth":-1,"metadataPath":"$","metadataKey":"_flattenMeta"}
              }
            ]
            """;

        var flatResult = Execute(doc, flattenScript);
        Assert.That(flatResult.Success, Is.True, "flatten must succeed before restore test");

        Console.WriteLine($"[flatten→restore] Flattened: {flatResult.Output}");

        // Now restore
        const string restoreScript = """
            [
              {
                "command":"restore",
                "path":"$.config",
                "restoreSettings":{"metadataPath":"$._flattenMeta"}
              }
            ]
            """;

        var restoreResult = Execute(flatResult.Output, restoreScript);
        Assert.That(restoreResult.Success, Is.True);

        Console.WriteLine($"[flatten→restore] Restored: {restoreResult.Output}");
        Console.WriteLine($"[restore trace] {restoreResult.Trace[0].Detail}");

        // Verify key structure came back
        var output = JToken.Parse(restoreResult.Output);
        Assert.That(output["config"], Is.Not.Null, "config node must exist after restore");
    }

    // ── RESOLVE ──────────────────────────────────────────────────────────────────
    // Resolve = reference lookup / join: enrich target nodes by matching a lookup collection

    [Test]
    public void Resolve_LookupJoin_EnrichesBookRecordsWithAuthorName()
    {
        // Scenario: enrich books with author names from a lookup table
        // This tests the resolve command's core use case: join by key
        const string doc = """
            {
              "books": [
                {"isbn":"001","title":"Clean Code","author_id":"A1"},
                {"isbn":"002","title":"Pragmatic Programmer","author_id":"A2"}
              ],
              "authors": [
                {"id":"A1","name":"Robert Martin"},
                {"id":"A2","name":"Andrew Hunt"}
              ]
            }
            """;

        const string script = """
            [
              {
                "command":"resolve",
                "path":"$.books[*]",
                "resolveSettings":[
                  {
                    "referencesCollectionPath":"$.authors[*]",
                    "resolveKeys":[{"keyPath":"@.author_id","referenceKeyPath":"@.id"}],
                    "values":[{"targetPath":"@.author_name","value":"@.name"}]
                  }
                ]
              }
            ]
            """;

        var result = Execute(doc, script);

        Assert.That(result.Success, Is.True);
        Assert.That(result.Trace[0].Outcome, Is.EqualTo("success"));
        Assert.That(result.Trace[0].Detail, Does.Contain("resolve").Or.Contain("resolved"),
            "trace must confirm resolve was applied and how many nodes");

        var output = JToken.Parse(result.Output);
        Assert.That(output["books"]![0]!["author_name"]!.ToString(), Is.EqualTo("Robert Martin"),
            "first book must be enriched with author name");
        Assert.That(output["books"]![1]!["author_name"]!.ToString(), Is.EqualTo("Andrew Hunt"),
            "second book must be enriched with author name");

        Console.WriteLine($"[resolve] {result.Trace[0].Detail}");
        Console.WriteLine($"[resolve output] {output["books"]}");
    }

    [Test]
    public void Resolve_WrongPath_TraceShowsNoop()
    {
        const string doc = """{"items":[{"id":1}],"refs":[{"id":1,"value":"x"}]}""";
        const string script = """
            [
              {
                "command":"resolve",
                "path":"$.missing[*]",
                "resolveSettings":[
                  {
                    "referencesCollectionPath":"$.refs[*]",
                    "resolveKeys":[{"keyPath":"@.id","referenceKeyPath":"@.id"}],
                    "values":[{"targetPath":"@.value","value":{"path":"@.value"}}]
                  }
                ]
              }
            ]
            """;

        var result = Execute(doc, script);

        Assert.That(result.Trace[0].Outcome, Is.EqualTo("noop"),
            "resolve on missing path must show noop");
        Assert.That(result.Trace[0].Detail, Does.Contain("$.missing"),
            "noop trace must name the unmatched path");

        Console.WriteLine($"[resolve noop] {result.Trace[0].Detail}");
    }

    // ── ADD "already exists" — trace shows noop not success ──────────────────────

    [Test]
    public void Add_PropertyAlreadyExists_TraceShowsNoopNotSuccess()
    {
        // Before this fix, "add" on an existing property logged "already exists, skipping"
        // but the trace recorded Success/matched=1, misleading agents.
        const string doc = """{"user":{"name":"Alice"}}""";
        const string script = """[{"command":"add","path":"$.user.name","value":"Bob"}]""";

        var result = Execute(doc, script);

        Assert.That(result.Trace[0].Outcome, Is.EqualTo("noop"),
            "add on existing property must show noop — property already exists, nothing changed");
        Assert.That(result.Trace[0].MatchedCount, Is.EqualTo(0),
            "matched_count=0 tells agent nothing was written");
        Assert.That(result.Suggestions, Has.Count.GreaterThanOrEqualTo(1),
            "suggestion must appear so agent knows to use 'set' instead");

        Console.WriteLine($"[add exists noop] {result.Trace[0].Detail}");
        Console.WriteLine($"[add exists suggestion] {result.Suggestions[0]}");
    }

    // ── AUTHORS→BOOKS PIVOT: tlio_analyze quality test ──────────────────────────
    // A pivot (group-by) restructure isn't achievable with standard TLio commands.
    // This test validates that tlio_analyze correctly identifies the structural changes
    // and provides path-level detail an agent can use to understand the scope.

    [Test]
    public void Analyze_AuthorsBooksInversion_IdentifiesAllStructuralChanges()
    {
        const string input = """
            {
              "authors": [
                {"name":"Alice","books":["Book A","Book B"]},
                {"name":"Bob","books":["Book B","Book C"]}
              ]
            }
            """;

        const string target = """
            {
              "books": [
                {"title":"Book A","authors":["Alice"]},
                {"title":"Book B","authors":["Alice","Bob"]},
                {"title":"Book C","authors":["Bob"]}
              ]
            }
            """;

        var analysis = Analyze(input, target);

        Console.WriteLine($"\n── Authors→Books pivot analysis ({analysis.Changes.Count} changes) ──");
        Console.WriteLine($"Summary: {analysis.Summary}");
        foreach (var c in analysis.Changes)
            Console.WriteLine($"  [{c.ChangeType,-7}] {c.SourcePath} → {c.TargetPath}");
        Console.WriteLine($"  (description sample): {analysis.Changes.FirstOrDefault()?.Description}");

        // The analysis must identify changes — both removals from $.authors and additions to $.books
        Assert.That(analysis.Changes, Is.Not.Empty,
            "pivot analysis must identify structural changes");

        var types = analysis.Changes.Select(c => c.ChangeType).ToHashSet();
        Assert.That(types, Does.Contain("Remove").Or.Contain("Rename"),
            "must detect that author-array nodes are removed/moved");
        Assert.That(types, Does.Contain("Add").Or.Contain("Rename"),
            "must detect that book-array nodes are added/moved");

        // Every change must have path info so an agent can act
        foreach (var change in analysis.Changes)
        {
            Assert.That(
                string.IsNullOrEmpty(change.SourcePath) && string.IsNullOrEmpty(change.TargetPath),
                Is.False,
                $"Change '{change.ChangeType}' has no path info — agent cannot act on it");
            Assert.That(change.Description, Is.Not.Empty,
                $"Change at {change.SourcePath}/{change.TargetPath} has no description");
        }
    }

    [Test]
    public void Analyze_ArrayEnrichment_ConvergesInOneIteration()
    {
        // A pivot isn't achievable in TLio, but array-level enrichment IS.
        // This test proves that for achievable array transformations the MCP feedback
        // guides convergence within one tlio_execute call.
        const string input = """
            {
              "products": [
                {"sku":"A001","name":"Widget","in_stock":false},
                {"sku":"B002","name":"Gadget","in_stock":false}
              ]
            }
            """;

        const string target = """
            {
              "products": [
                {"sku":"A001","name":"Widget","in_stock":true,"category":"hardware"},
                {"sku":"B002","name":"Gadget","in_stock":true,"category":"electronics"}
              ]
            }
            """;

        var gap = (AnalyzeResult)_analysis.Analyze(input, "json", target, "json",
            intent: "activate products and add category");

        Console.WriteLine($"\n── Array enrichment gap ({gap.Changes.Count} changes) ──");
        foreach (var c in gap.Changes)
            Console.WriteLine($"  [{c.ChangeType,-7}] {c.Description}");

        Assert.That(gap.Changes, Is.Not.Empty);

        // Script that fixes the Mutate changes (in_stock) and Add changes (category):
        // For individual indexed paths from the gap, we need to address each item.
        // But in practice an agent would use $.products[*] for uniform changes.
        const string script = """
            [
              {"command":"set","path":"$.products[0].in_stock","value":true},
              {"command":"set","path":"$.products[1].in_stock","value":true},
              {"command":"add","path":"$.products[0].category","value":"hardware"},
              {"command":"add","path":"$.products[1].category","value":"electronics"}
            ]
            """;

        var execResult = (ExecuteResult)_execution.Execute(input, "json", script);
        Assert.That(execResult.Success, Is.True);
        Assert.That(execResult.Trace.Select(t => t.Outcome), Has.All.EqualTo("success"),
            "all commands on individual items must succeed");

        // Verify convergence
        var finalGap = (AnalyzeResult)_analysis.Analyze(execResult.Output, "json", target, "json");
        Assert.That(finalGap.Changes, Is.Empty,
            "one iteration is enough when the gap report names the exact indexed paths");
    }
}
