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
/// Proves that the MCP feedback quality is sufficient for an AI agent to converge in
/// at most 2 iterations — even when the first attempt contains realistic mistakes.
///
/// Each test:
///   1. Runs a deliberately-flawed first script (wrong command, wrong type, missing fields, wrong path).
///   2. Asserts that the feedback (Suggestions + refined gap report) exposes ALL problems.
///   3. Builds a corrected script using only that feedback.
///   4. Asserts convergence by iteration 2.
///
/// Scenarios cover six real-world domains with six distinct mistake types:
///   A. Healthcare   — 'set' on non-existent fields (should be 'add') → noops
///   B. Financial    — partial script misses entire structural group
///   C. SaaS upgrade — mutates complete but feature-flag Adds entirely omitted
///   D. DevOps config — structural renames done, groupings into nested objects missed
///   E. Retail        — wrong value types (string "12.99" instead of number 12.99)
///   F. HR transfer   — 'set' on new nested fields creates empty containers, forgets mutations
/// </summary>
[TestFixture]
public sealed class McpTwoIterationTests
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

    // ── Helpers ───────────────────────────────────────────────────────────────

    private ExecuteResult Execute(string doc, string script) =>
        (ExecuteResult)_execution.Execute(doc, "json", script);

    private AnalyzeResult Analyze(string input, string target, string? intent = null, string? priorTraceJson = null) =>
        (AnalyzeResult)_analysis.Analyze(input, "json", target, "json", intent, priorTraceJson);

    private static void Section(string label) =>
        Console.WriteLine($"\n{'─',1} {label} {'─',1}");

    private static void PrintGap(AnalyzeResult r, string label)
    {
        Section($"{label}  [{r.Changes.Count} changes]");
        Console.WriteLine($"  Summary: {r.Summary}");
        foreach (var c in r.Changes)
            Console.WriteLine($"    [{c.ChangeType,-6}] {c.Description}");
    }

    private static void PrintTrace(ExecuteResult r, string label)
    {
        Section($"{label}  success={r.Success}");
        foreach (var t in r.Trace)
            Console.WriteLine($"    [{t.Outcome,-7}] {t.CommandName} @ {t.Path}  matched={t.MatchedCount}");
        foreach (var s in r.Suggestions)
            Console.WriteLine($"    SUGGESTION: {s}");
        foreach (var e in r.Errors)
            Console.WriteLine($"    ERROR: {e}");
    }

    // ════════════════════════════════════════════════════════════════════════
    // A. Healthcare — patient record: emergency contact added
    //    Mistake: agent uses 'set' on fields that don't yet exist → noops.
    //    Feedback: Suggestions expose every noop; refined gap shows 'add' is needed.
    //    Convergence by iteration 2.
    // ════════════════════════════════════════════════════════════════════════

    private const string PatientSource = """
        {
          "patient_id": "PAT-3301",
          "name": "Maria Santos",
          "dob": "1985-07-22",
          "contact": {
            "phone": "+1-555-0241",
            "email": "maria@example.com"
          },
          "blood_type": "A+",
          "allergies": ["penicillin"]
        }
        """;

    private const string PatientTarget = """
        {
          "patient_id": "PAT-3301",
          "name": "Maria Santos",
          "dob": "1985-07-22",
          "contact": {
            "phone": "+1-555-0241",
            "email": "maria@example.com"
          },
          "blood_type": "A+",
          "allergies": ["penicillin"],
          "emergency_contact": {
            "name": "Jorge Santos",
            "relationship": "spouse",
            "phone": "+1-555-0242"
          },
          "consent_given": true,
          "last_updated": "2024-11-20"
        }
        """;

    [Test]
    public void A_Healthcare_SetOnNonExistentFields_SuggestionsGuideToAdd()
    {
        var gap = Analyze(PatientSource, PatientTarget);
        PrintGap(gap, "A | Initial gap");

        // ── Iteration 1: agent confuses 'set' with 'add'; also forgets consent_given and last_updated
        const string attempt1 = """
            [
              {"command":"set","path":"$.emergency_name","value":"Jorge Santos"},
              {"command":"set","path":"$.emergency_phone","value":"+1-555-0242"},
              {"command":"set","path":"$.emergency_relationship","value":"spouse"}
            ]
            """;

        var exec1 = Execute(PatientSource, attempt1);
        PrintTrace(exec1, "A | Iteration 1 — 'set' on non-existent fields");

        // All three must noop — 'set' requires the field to already exist
        Assert.That(exec1.Trace.Count(t => t.Outcome == "noop"), Is.EqualTo(3),
            "All 'set' commands on missing fields must noop");

        // Suggestions must enumerate every noop and guide toward 'add'
        // (+1 for the trailing tlio_guide hint appended to all non-empty suggestion lists)
        Assert.That(exec1.Suggestions.Count, Is.GreaterThanOrEqualTo(3),
            "One suggestion per noop — agent must see all problems at once");
        foreach (var s in exec1.Suggestions.Where(s => s.StartsWith("[noop]")))
        {
            Assert.That(s, Does.Contain("noop").IgnoreCase, "Suggestion must label outcome 'noop'");
            Assert.That(s, Does.Contain("add").Or.Contain("tlio_analyze"),
                "Suggestion must mention 'add' command or point to tlio_analyze");
        }

        // ── Refined gap: supply prior trace so resolution tagging works
        var traceJson1 = JsonSerializer.Serialize(exec1.Trace);
        var refinedGap = Analyze(exec1.Output, PatientTarget,
            intent: "add emergency_contact group, consent_given, last_updated",
            priorTraceJson: traceJson1);
        PrintGap(refinedGap, "A | Refined gap after iteration 1");

        // Source was not modified → all remaining changes are Adds
        Assert.That(refinedGap.Changes.All(c => c.ChangeType == "Add"),
            Is.True, "All remaining changes must be Add (document unchanged after full-noop iteration)");

        // Every Add description must carry json_value — agent needs type-correct values
        foreach (var add in refinedGap.Changes.Where(c => !c.Description.Contains("{}") && !c.Description.Contains("[]")))
            Assert.That(add.Description, Does.Contain("json_value:"),
                $"Add at {add.TargetPath} must specify json_value so agent writes correct type");

        // ── Iteration 2: corrected script derived entirely from the refined gap report
        var script2 = BuildScriptFromGaps(refinedGap.Changes);
        var exec2 = Execute(exec1.Output, script2);
        PrintTrace(exec2, "A | Iteration 2 — 'add' commands from gap report");

        Assert.That(exec2.Success, Is.True, "Iteration 2 must succeed");
        Assert.That(exec2.Suggestions, Is.Empty, "No suggestions when all commands succeed");

        var finalGap = Analyze(exec2.Output, PatientTarget);
        PrintGap(finalGap, "A | Final verification");
        Assert.That(finalGap.Changes, Is.Empty,
            $"Must converge by iteration 2. Remaining: {finalGap.Summary}");
    }

    // ════════════════════════════════════════════════════════════════════════
    // B. Financial — payment webhook normalization
    //    Mistake: handles simple renames but skips structural grouping entirely.
    //    Feedback: refined gap shows all unfinished structural renames.
    //    Convergence by iteration 2.
    // ════════════════════════════════════════════════════════════════════════

    private const string PaymentSource = """
        {
          "txn_id": "TXN-88210",
          "amount_cents": 24999,
          "currency": "USD",
          "status": "captured",
          "payer_email": "buyer@example.com",
          "payer_name": "John Buyer",
          "created": "2024-11-01T14:30:00Z"
        }
        """;

    private const string PaymentTarget = """
        {
          "id": "TXN-88210",
          "amount": {
            "value_cents": 24999,
            "currency": "USD"
          },
          "state": "settled",
          "payer": {
            "email": "buyer@example.com",
            "name": "John Buyer"
          },
          "created_at": "2024-11-01T14:30:00Z"
        }
        """;

    [Test]
    public void B_Financial_PartialScript_StructuralGroupsMissed_Converges()
    {
        var gap = Analyze(PaymentSource, PaymentTarget);
        PrintGap(gap, "B | Initial gap");

        // ── Iteration 1: handles top-level renames and status value, skips groupings
        const string attempt1 = """
            [
              {"command":"copy","fromPath":"$.txn_id","toPath":"$.id"},
              {"command":"remove","path":"$.txn_id"},
              {"command":"copy","fromPath":"$.created","toPath":"$.created_at"},
              {"command":"remove","path":"$.created"},
              {"command":"set","path":"$.status","value":"settled"}
            ]
            """;

        var exec1 = Execute(PaymentSource, attempt1);
        PrintTrace(exec1, "B | Iteration 1 — top-level renames only");

        Assert.That(exec1.Success, Is.True, "Iteration 1 must succeed");
        Assert.That(exec1.Suggestions, Is.Empty,
            "No noops expected — all commands targeted existing fields");

        // Gap must still show the structural groupings that were skipped
        var traceJson1 = JsonSerializer.Serialize(exec1.Trace);
        var refinedGap = Analyze(exec1.Output, PaymentTarget,
            intent: "group amount fields, group payer fields, rename status→state",
            priorTraceJson: traceJson1);
        PrintGap(refinedGap, "B | Refined gap — groupings and status rename remaining");

        Assert.That(refinedGap.Changes.Count, Is.GreaterThan(0),
            "Structural groupings must appear in the refined gap");
        Assert.That(refinedGap.Changes.Any(c => c.TargetPath.Contains("amount")),
            Is.True, "Refined gap must identify amount group still needed");
        Assert.That(refinedGap.Changes.Any(c => c.TargetPath.Contains("payer")),
            Is.True, "Refined gap must identify payer group still needed");
        // status value was set to "settled" — diff detects Rename ($.status → $.state, value="settled")
        Assert.That(refinedGap.Changes.Any(c =>
                (c.ChangeType is "Rename" or "Remove" or "Add") &&
                (c.SourcePath.Contains("status") || c.TargetPath.Contains("state"))),
            Is.True, "Refined gap must identify status→state rename still needed");

        // ── Iteration 2: apply all remaining changes from gap
        var script2 = BuildScriptFromGaps(refinedGap.Changes);
        var exec2 = Execute(exec1.Output, script2);
        PrintTrace(exec2, "B | Iteration 2 — structural groupings");

        Assert.That(exec2.Success, Is.True, "Iteration 2 must succeed");

        var finalGap = Analyze(exec2.Output, PaymentTarget);
        PrintGap(finalGap, "B | Final verification");
        Assert.That(finalGap.Changes, Is.Empty,
            $"Must converge by iteration 2. Remaining: {finalGap.Summary}");
    }

    // ════════════════════════════════════════════════════════════════════════
    // C. SaaS upgrade — subscription plan upgrade
    //    Mistake: mutates (plan, seats, billing_cycle) are done correctly but
    //             all Add operations (feature flags, upgraded_at) are omitted.
    //    Feedback: refined gap lists every missing Add with json_value.
    //    Convergence by iteration 2.
    // ════════════════════════════════════════════════════════════════════════

    private const string SubscriptionSource = """
        {
          "subscription_id": "SUB-1140",
          "customer_id": "CUST-8801",
          "plan": "basic",
          "seats": 5,
          "billing_cycle": "monthly",
          "active": true
        }
        """;

    private const string SubscriptionTarget = """
        {
          "subscription_id": "SUB-1140",
          "customer_id": "CUST-8801",
          "plan": "enterprise",
          "seats": 50,
          "billing_cycle": "annual",
          "active": true,
          "features": {
            "sso": true,
            "audit_log": true,
            "custom_roles": true,
            "api_access": true
          },
          "upgraded_at": "2024-11-15T09:00:00Z"
        }
        """;

    [Test]
    public void C_SaaS_MutatesDoneAddsMissed_GapReportCompletesScript()
    {
        var gap = Analyze(SubscriptionSource, SubscriptionTarget);
        PrintGap(gap, "C | Initial gap");

        // ── Iteration 1: agent only applies the three Mutates, omits all Adds
        const string attempt1 = """
            [
              {"command":"set","path":"$.plan","value":"enterprise"},
              {"command":"set","path":"$.seats","value":50},
              {"command":"set","path":"$.billing_cycle","value":"annual"}
            ]
            """;

        var exec1 = Execute(SubscriptionSource, attempt1);
        PrintTrace(exec1, "C | Iteration 1 — mutates only, no Adds");

        Assert.That(exec1.Success, Is.True, "Iteration 1 must succeed");
        Assert.That(exec1.Suggestions, Is.Empty, "No noops — all set commands targeted existing fields");

        // 3 mutates applied → 5 Adds remain (features group + upgraded_at)
        var refinedGap = Analyze(exec1.Output, SubscriptionTarget,
            intent: "add features object with sso, audit_log, custom_roles, api_access; add upgraded_at");
        PrintGap(refinedGap, "C | Refined gap — Add operations only");

        Assert.That(refinedGap.Changes.All(c => c.ChangeType == "Add"),
            Is.True, "All remaining changes must be Add type");
        Assert.That(refinedGap.Changes.Count, Is.EqualTo(5),
            "5 Adds expected: 4 feature flags + upgraded_at");

        // Every boolean json_value must be 'true' (bool), not '\"true\"' (string)
        var boolAdds = refinedGap.Changes.Where(c => c.Description.Contains("json_value: true")).ToList();
        Assert.That(boolAdds.Count, Is.EqualTo(4),
            "4 feature flags must carry json_value: true (boolean, not string)");

        // ── Iteration 2: apply all Adds from gap
        var script2 = BuildScriptFromGaps(refinedGap.Changes);
        var exec2 = Execute(exec1.Output, script2);
        PrintTrace(exec2, "C | Iteration 2 — Add feature flags and upgraded_at");

        Assert.That(exec2.Success, Is.True, "Iteration 2 must succeed");
        Assert.That(exec2.Suggestions, Is.Empty, "No suggestions expected");

        var finalGap = Analyze(exec2.Output, SubscriptionTarget);
        PrintGap(finalGap, "C | Final verification");
        Assert.That(finalGap.Changes, Is.Empty,
            $"Must converge by iteration 2. Remaining: {finalGap.Summary}");

        // Spot-check: booleans written as true, not "true"
        var output = JObject.Parse(exec2.Output);
        Assert.That(output["features"]!["sso"]!.Type, Is.EqualTo(JTokenType.Boolean),
            "features.sso must be a JSON boolean, not a string");
        Assert.That(output["features"]!["sso"]!.Value<bool>(), Is.True);
    }

    // ════════════════════════════════════════════════════════════════════════
    // D. DevOps — service config migration
    //    Mistake: handles top-level renames and a value mutation correctly but
    //             misses all structural groupings (scaling, resources, observability).
    //    Feedback: refined gap shows the remaining structural Renames and Adds.
    //    Convergence by iteration 2.
    // ════════════════════════════════════════════════════════════════════════

    private const string ServiceConfigSource = """
        {
          "service_name": "auth-service",
          "runtime_version": "3.11",
          "max_instances": 5,
          "min_instances": 1,
          "cpu_millicores": 250,
          "memory_mb": 512,
          "exposed_port": 8443,
          "log_level": "warn"
        }
        """;

    private const string ServiceConfigTarget = """
        {
          "name": "auth-service",
          "runtime": "3.11",
          "scaling": {
            "max": 5,
            "min": 1
          },
          "resources": {
            "cpu_millicores": 250,
            "memory_mb": 512
          },
          "port": 8443,
          "log_level": "info",
          "observability": {
            "tracing": true,
            "metrics": true
          }
        }
        """;

    [Test]
    public void D_DevOps_TopLevelRenamesDone_NestedGroupingsMissed_Converges()
    {
        var gap = Analyze(ServiceConfigSource, ServiceConfigTarget);
        PrintGap(gap, "D | Initial gap");

        // ── Iteration 1: handles the obvious single-field renames and the value mutation
        // but ignores the structural groupings (scaling, resources, observability)
        const string attempt1 = """
            [
              {"command":"copy","fromPath":"$.service_name","toPath":"$.name"},
              {"command":"remove","path":"$.service_name"},
              {"command":"copy","fromPath":"$.runtime_version","toPath":"$.runtime"},
              {"command":"remove","path":"$.runtime_version"},
              {"command":"copy","fromPath":"$.exposed_port","toPath":"$.port"},
              {"command":"remove","path":"$.exposed_port"},
              {"command":"set","path":"$.log_level","value":"info"}
            ]
            """;

        var exec1 = Execute(ServiceConfigSource, attempt1);
        PrintTrace(exec1, "D | Iteration 1 — top-level renames + log_level");

        Assert.That(exec1.Success, Is.True);
        Assert.That(exec1.Suggestions, Is.Empty, "No noops — all commands hit existing fields");

        // Four flat fields remain (max_instances, min_instances, cpu_millicores, memory_mb)
        // plus the two new observability booleans
        var refinedGap = Analyze(exec1.Output, ServiceConfigTarget,
            intent: "move max/min into scaling, cpu/memory into resources, add observability",
            priorTraceJson: JsonSerializer.Serialize(exec1.Trace));
        PrintGap(refinedGap, "D | Refined gap — groupings and observability remain");

        // Structural renames should still show up
        Assert.That(refinedGap.Changes.Any(c => c.TargetPath.Contains("scaling")),
            Is.True, "Refined gap must identify scaling group still missing");
        Assert.That(refinedGap.Changes.Any(c => c.TargetPath.Contains("resources")),
            Is.True, "Refined gap must identify resources group still missing");
        Assert.That(refinedGap.Changes.Any(c => c.TargetPath.Contains("observability")),
            Is.True, "Refined gap must identify observability group still missing");

        // Observability adds must have json_value: true
        foreach (var add in refinedGap.Changes.Where(c => c.ChangeType == "Add" && c.TargetPath.Contains("observability")))
            Assert.That(add.Description, Does.Contain("json_value: true"),
                $"Observability Add at {add.TargetPath} must specify json_value: true");

        // ── Iteration 2: apply structural moves from gap
        var script2 = BuildScriptFromGaps(refinedGap.Changes);
        var exec2 = Execute(exec1.Output, script2);
        PrintTrace(exec2, "D | Iteration 2 — structural groupings");

        Assert.That(exec2.Success, Is.True);

        var finalGap = Analyze(exec2.Output, ServiceConfigTarget);
        PrintGap(finalGap, "D | Final verification");
        Assert.That(finalGap.Changes, Is.Empty,
            $"Must converge by iteration 2. Remaining: {finalGap.Summary}");
    }

    // ════════════════════════════════════════════════════════════════════════
    // E. Retail — product price + availability update
    //    Mistake: agent reads intent loosely and sets price to a wrong approximate
    //             value, also misses stock_count and last_audited entirely.
    //    Feedback: refined gap shows exact json_value for each remaining change
    //              (number without quotes, not string); agent corrects in iteration 2.
    //    Convergence by iteration 2.
    // ════════════════════════════════════════════════════════════════════════

    private const string ProductSource = """
        {
          "sku": "SKU-4455",
          "name": "Premium Coffee Blend",
          "price": 14.99,
          "in_stock": true,
          "stock_count": 200,
          "weight_oz": 12
        }
        """;

    private const string ProductTarget = """
        {
          "sku": "SKU-4455",
          "name": "Premium Coffee Blend",
          "price": 12.99,
          "in_stock": false,
          "stock_count": 165,
          "weight_oz": 12,
          "last_audited": "2024-11-10"
        }
        """;

    [Test]
    public void E_Retail_WrongValues_GapDescribesExactJsonValue_Converges()
    {
        var gap = Analyze(ProductSource, ProductTarget);
        PrintGap(gap, "E | Initial gap");

        // ── Iteration 1: agent reads intent "lower the price, mark out of stock" but uses
        //   an approximate value for price (12.50 instead of 12.99) and misses stock_count
        //   and last_audited entirely.
        const string attempt1 = """
            [
              {"command":"set","path":"$.price","value":12.50},
              {"command":"set","path":"$.in_stock","value":false}
            ]
            """;

        var exec1 = Execute(ProductSource, attempt1);
        PrintTrace(exec1, "E | Iteration 1 — wrong price value, missed stock_count and last_audited");

        Assert.That(exec1.Success, Is.True);
        Assert.That(exec1.Trace.All(t => t.Outcome == "success"), Is.True,
            "Both set commands on existing fields succeed");
        Assert.That(exec1.Suggestions, Is.Empty, "No noops");

        // ── Refined gap: analyze exposes wrong price value and missing fields
        var refinedGap = Analyze(exec1.Output, ProductTarget,
            intent: "fix price to correct value; update stock_count; add last_audited");
        PrintGap(refinedGap, "E | Refined gap — wrong price + missing fields");

        // price: 12.5 → 12.99 — the json_value must be a number (not a quoted string)
        var priceChange = refinedGap.Changes.FirstOrDefault(c => c.SourcePath == "$.price");
        Assert.That(priceChange, Is.Not.Null,
            "Analyze must detect wrong price (12.5 set by agent ≠ target 12.99)");
        Assert.That(priceChange!.ChangeType, Is.EqualTo("Mutate"));
        Assert.That(priceChange.Description, Does.Contain("json_value: 12.99"),
            "Price description must show exact numeric json_value: 12.99 without quotes — agent applies it directly");

        // stock_count and last_audited still missing
        Assert.That(refinedGap.Changes.Any(c => c.SourcePath == "$.stock_count"),
            Is.True, "Refined gap must expose stock_count Mutate");
        Assert.That(refinedGap.Changes.Any(c => c.ChangeType == "Add" && c.TargetPath.Contains("last_audited")),
            Is.True, "Refined gap must show last_audited Add");

        // stock_count description must also carry a numeric json_value
        var scChange = refinedGap.Changes.First(c => c.SourcePath == "$.stock_count");
        Assert.That(scChange.Description, Does.Contain("json_value: 165"),
            "stock_count json_value must be the number 165, not the string '165'");

        // ── Iteration 2: apply all remaining corrections from gap descriptions
        var script2 = BuildScriptFromGaps(refinedGap.Changes);
        var exec2 = Execute(exec1.Output, script2);
        PrintTrace(exec2, "E | Iteration 2 — correct values from gap");

        Assert.That(exec2.Success, Is.True);

        var finalGap = Analyze(exec2.Output, ProductTarget);
        PrintGap(finalGap, "E | Final verification");
        Assert.That(finalGap.Changes, Is.Empty,
            $"Must converge by iteration 2. Remaining: {finalGap.Summary}");

        // Confirm types are correct in output (numbers stay numbers, booleans stay booleans)
        var output = JObject.Parse(exec2.Output);
        Assert.That(output["price"]!.Type, Is.EqualTo(JTokenType.Float), "price must be a number");
        Assert.That(output["price"]!.Value<double>(), Is.EqualTo(12.99).Within(0.001));
        Assert.That(output["in_stock"]!.Type, Is.EqualTo(JTokenType.Boolean), "in_stock must be a boolean");
        Assert.That(output["in_stock"]!.Value<bool>(), Is.False);
    }

    // ════════════════════════════════════════════════════════════════════════
    // F. HR — employee department transfer
    //    Mistake: 'set' on new nested fields (transfer.*) — EnsurePath creates the
    //             full path including the leaf for the FIRST command, so the first
    //             transfer.* set succeeds; subsequent ones noop because the parent
    //             exists but the other leaf properties do not.
    //             Agent also forgets title and salary_band mutations entirely.
    //    Feedback: 2 suggestions (noop) + refined gap exposes title/salary_band
    //              Mutates and remaining transfer fields as Adds.
    //    Convergence by iteration 2.
    // ════════════════════════════════════════════════════════════════════════

    private const string EmployeeSource = """
        {
          "employee_id": "EMP-7721",
          "name": "Sarah Chen",
          "email": "sarah.chen@company.com",
          "department": "engineering",
          "manager_id": "EMP-1001",
          "title": "Senior Engineer",
          "salary_band": "L4",
          "location": "New York",
          "full_time": true
        }
        """;

    private const string EmployeeTarget = """
        {
          "employee_id": "EMP-7721",
          "name": "Sarah Chen",
          "email": "sarah.chen@company.com",
          "department": "product",
          "manager_id": "EMP-2005",
          "title": "Senior Product Engineer",
          "salary_band": "L5",
          "location": "New York",
          "full_time": true,
          "transfer": {
            "from_department": "engineering",
            "effective_date": "2024-12-01",
            "approved_by": "EMP-0001"
          }
        }
        """;

    [Test]
    public void F_HR_SetOnNewNestedFields_SuggestionsAndGapGuideFullCorrection()
    {
        var gap = Analyze(EmployeeSource, EmployeeTarget);
        PrintGap(gap, "F | Initial gap");

        // ── Iteration 1:
        //   • correctly sets department and manager_id
        //   • uses 'set' (not 'add') for new nested transfer.* fields → noops
        //     ('set' only updates nodes that already exist; it never scaffolds the path)
        //   • forgets title and salary_band mutations entirely
        const string attempt1 = """
            [
              {"command":"set","path":"$.department","value":"product"},
              {"command":"set","path":"$.manager_id","value":"EMP-2005"},
              {"command":"set","path":"$.transfer.from_department","value":"engineering"},
              {"command":"set","path":"$.transfer.effective_date","value":"2024-12-01"},
              {"command":"set","path":"$.transfer.approved_by","value":"EMP-0001"}
            ]
            """;

        var exec1 = Execute(EmployeeSource, attempt1);
        PrintTrace(exec1, "F | Iteration 1 — set on new nested fields, forgotten mutations");

        Assert.That(exec1.Success, Is.True, "Iteration 1 does not fail");

        // All three 'set $.transfer.*' commands noop: the transfer object does not exist and
        // 'set' never creates it. Each warns and the script continues with the document untouched.
        var noops = exec1.Trace.Where(t => t.Outcome == "noop").ToList();
        Assert.That(noops.Count, Is.EqualTo(3),
            "from_department, effective_date and approved_by all noop — 'set' does not scaffold a missing path");

        // Suggestions must expose the two noops with clear guidance
        // (+1 for the trailing tlio_guide hint appended to all non-empty suggestion lists)
        Assert.That(exec1.Suggestions.Count, Is.GreaterThanOrEqualTo(2),
            "One suggestion per noop — agent sees both problem paths at once");
        foreach (var s in exec1.Suggestions.Where(s => s.StartsWith("[noop]")))
        {
            Assert.That(s, Does.Contain("noop").IgnoreCase);
            Assert.That(s, Does.Contain("add").Or.Contain("tlio_analyze"),
                "Suggestion must guide toward 'add' for creating new fields");
        }

        // Partial document: department and manager_id are updated; no transfer object was invented
        var partial = JObject.Parse(exec1.Output);
        Assert.That(partial["department"]!.ToString(), Is.EqualTo("product"));
        Assert.That(partial["manager_id"]!.ToString(), Is.EqualTo("EMP-2005"));
        Assert.That(partial["transfer"], Is.Null,
            "'set' left the document untouched — no half-built transfer object to clean up afterwards");

        // ── Refined gap: operate on exec1 output; prior trace marks noops as unresolved
        var traceJson1 = JsonSerializer.Serialize(exec1.Trace);
        var refinedGap = Analyze(exec1.Output, EmployeeTarget,
            intent: "fix title and salary_band; use 'add' for the missing transfer fields",
            priorTraceJson: traceJson1);
        PrintGap(refinedGap, "F | Refined gap — all remaining issues visible");

        // Must detect title and salary_band as Mutates
        Assert.That(refinedGap.Changes.Any(c => c.ChangeType == "Mutate" && c.SourcePath.Contains("title")),
            Is.True, "Refined gap must expose title Mutate (was omitted from iteration 1)");
        Assert.That(refinedGap.Changes.Any(c => c.ChangeType == "Mutate" && c.SourcePath.Contains("salary_band")),
            Is.True, "Refined gap must expose salary_band Mutate (was omitted from iteration 1)");

        // Must detect the transfer.* fields still missing
        Assert.That(refinedGap.Changes.Any(c => c.TargetPath.Contains("transfer")),
            Is.True, "Refined gap must expose transfer fields still missing");

        // All Mutate descriptions must supply json_value
        foreach (var m in refinedGap.Changes.Where(c => c.ChangeType == "Mutate"))
            Assert.That(m.Description, Does.Contain("json_value:"),
                $"Mutate at {m.SourcePath} must include json_value for type-correct script");

        // ── Iteration 2: full correction using only the gap feedback
        var script2 = BuildScriptFromGaps(refinedGap.Changes);
        var exec2 = Execute(exec1.Output, script2);
        PrintTrace(exec2, "F | Iteration 2 — complete correction");

        Assert.That(exec2.Success, Is.True, "Iteration 2 must succeed");
        Assert.That(exec2.Suggestions, Is.Empty, "No suggestions after successful correction");

        var finalGap = Analyze(exec2.Output, EmployeeTarget);
        PrintGap(finalGap, "F | Final verification");
        Assert.That(finalGap.Changes, Is.Empty,
            $"Must converge by iteration 2. Remaining: {finalGap.Summary}");

        // Spot-check final output correctness
        var finalDoc = JObject.Parse(exec2.Output);
        Assert.That(finalDoc["title"]!.ToString(), Is.EqualTo("Senior Product Engineer"));
        Assert.That(finalDoc["salary_band"]!.ToString(), Is.EqualTo("L5"));
        Assert.That(finalDoc["transfer"]!["from_department"]!.ToString(), Is.EqualTo("engineering"));
        Assert.That(finalDoc["transfer"]!["approved_by"]!.ToString(), Is.EqualTo("EMP-0001"));
    }

    // ════════════════════════════════════════════════════════════════════════
    // G. Meta-assertion: every scenario's gap description is self-sufficient
    //    — the agent never needs domain knowledge to build a correct script.
    //    All scalar Add/Mutate descriptions must embed json_value so the agent
    //    can construct a type-correct command without guessing.
    // ════════════════════════════════════════════════════════════════════════

    [Test]
    public void G_AllScenarios_GapDescriptions_AreSelfSufficientForScriptGeneration()
    {
        var scenarios = new (string Src, string Tgt, string Label)[]
        {
            (PatientSource,         PatientTarget,         "healthcare"),
            (PaymentSource,         PaymentTarget,         "financial"),
            (SubscriptionSource,    SubscriptionTarget,    "saas-upgrade"),
            (ServiceConfigSource,   ServiceConfigTarget,   "devops-config"),
            (ProductSource,         ProductTarget,         "retail-product"),
            (EmployeeSource,        EmployeeTarget,        "hr-transfer"),
        };

        var failures = new List<string>();

        foreach (var (src, tgt, label) in scenarios)
        {
            var result = Analyze(src, tgt);
            foreach (var c in result.Changes.Where(c => c.ChangeType is "Add" or "Mutate"))
            {
                if (c.Description.Contains("{}") || c.Description.Contains("[]"))
                    continue;  // container nodes have no scalar value to annotate
                if (!c.Description.Contains("json_value:"))
                    failures.Add($"[{label}] {c.ChangeType} at '{c.TargetPath ?? c.SourcePath}': " +
                                 $"no json_value — agent cannot determine correct type");
            }

            // Also verify: RENAME descriptions always name both commands
            foreach (var r in result.Changes.Where(c => c.ChangeType == "Rename"))
            {
                if (!r.Description.Contains("copy", StringComparison.OrdinalIgnoreCase) ||
                    !r.Description.Contains("remove", StringComparison.OrdinalIgnoreCase))
                    failures.Add($"[{label}] Rename from '{r.SourcePath}' → '{r.TargetPath}': " +
                                 $"description missing copy/remove instructions");
            }
        }

        if (failures.Count > 0)
        {
            Console.WriteLine("Descriptions that are NOT self-sufficient:");
            failures.ForEach(f => Console.WriteLine($"  {f}"));
        }

        Assert.That(failures, Is.Empty,
            "Every scalar Add/Mutate/Rename description must be self-sufficient for script generation");
    }

    // ── Simulate an AI agent building a type-correct script from a gap report ──
    // Reads the "json_value: <value>." marker embedded in descriptions.
    // JToken.Parse ensures correct types: true→bool, 42→int, "x"→string.

    private static string BuildScriptFromGaps(IReadOnlyList<ChangeItem> changes)
    {
        var commands = new JArray();
        foreach (var change in changes)
        {
            switch (change.ChangeType)
            {
                case "Mutate":
                {
                    var jv = ExtractJsonValue(change.Description);
                    if (jv is not null)
                        commands.Add(new JObject
                        {
                            ["command"] = "set",
                            ["path"] = change.SourcePath,
                            ["value"] = jv
                        });
                    break;
                }
                case "Add":
                {
                    var jv = ExtractJsonValue(change.Description);
                    if (jv is not null)
                        commands.Add(new JObject
                        {
                            ["command"] = "add",
                            ["path"] = change.TargetPath,
                            ["value"] = jv
                        });
                    break;
                }
                case "Remove":
                    commands.Add(new JObject { ["command"] = "remove", ["path"] = change.SourcePath });
                    break;
                case "Rename":
                    commands.Add(new JObject
                    {
                        ["command"] = "copy",
                        ["fromPath"] = change.SourcePath,
                        ["toPath"] = change.TargetPath
                    });
                    commands.Add(new JObject { ["command"] = "remove", ["path"] = change.SourcePath });
                    break;
            }
        }
        return commands.ToString(Newtonsoft.Json.Formatting.None);
    }

    private static JToken? ExtractJsonValue(string description)
    {
        const string marker = "json_value: ";
        var start = description.IndexOf(marker, StringComparison.Ordinal);
        if (start < 0) return null;
        start += marker.Length;
        var end = description.LastIndexOf('.');
        if (end <= start) return null;
        var raw = description[start..end].Trim();
        try { return JToken.Parse(raw); }
        catch { return JValue.CreateString(raw); }
    }
}
