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
/// Challenges the MCP tools with real-world schema migration scenarios drawn from
/// common software engineering domains (e-commerce, user management, API integration,
/// project management, IoT).
///
/// Each scenario verifies that the feedback returned by tlio_analyze and tlio_execute
/// is specific enough for an AI agent to converge on the correct transformation within
/// at most 3 iterations — including recovery from wrong-path mistakes.
/// </summary>
[TestFixture]
public sealed class McpComplexChallengeTests
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

    // ── Helpers ──────────────────────────────────────────────────────────────

    private ExecuteResult Execute(string doc, string script) =>
        (ExecuteResult)_execution.Execute(doc, "json", script);

    private AnalyzeResult Analyze(string input, string target, string? intent = null, string? priorTraceJson = null) =>
        (AnalyzeResult)_analysis.Analyze(input, "json", target, "json", intent, priorTraceJson);

    private static void PrintGap(AnalyzeResult r, string label)
    {
        Console.WriteLine($"\n── {label} ({r.Changes.Count} changes) ──────────────────────────────");
        Console.WriteLine($"Summary: {r.Summary}");
        foreach (var c in r.Changes)
            Console.WriteLine($"  [{c.ChangeType,-6}] {c.Description}");
    }

    private static void PrintTrace(ExecuteResult r, string label)
    {
        Console.WriteLine($"\n── {label} (Success={r.Success}) ──────────────────────────────────");
        foreach (var t in r.Trace)
            Console.WriteLine($"  [{t.Outcome,-7}] {t.CommandName} @ {t.Path} (matched={t.MatchedCount}) — {t.Detail}");
        if (r.Errors.Count > 0)
            Console.WriteLine($"  ERRORS: {string.Join(", ", r.Errors)}");
        if (r.Suggestions.Count > 0)
        {
            Console.WriteLine("  SUGGESTIONS (agent should act on these before next iteration):");
            foreach (var s in r.Suggestions)
                Console.WriteLine($"    • {s}");
        }
    }

    // ════════════════════════════════════════════════════════════════════════
    // Scenario 1: E-commerce order normalization
    //   Legacy ERP flat export → structured order management format.
    //   Changes: field renames, structural grouping (customer, shipping, amount),
    //            status value mutation.
    //   Complexity: 13+ changes, 2 structural groups with multiple sub-fields.
    // ════════════════════════════════════════════════════════════════════════

    private const string OrderLegacy = """
        {
          "order_id": "ORD-9821",
          "customer_name": "Jane Smith",
          "customer_email": "jane@example.com",
          "order_status": "pending",
          "ship_to_street": "123 Main St",
          "ship_to_city": "Springfield",
          "ship_to_zip": "62701",
          "ship_to_country": "US",
          "subtotal": 149.99,
          "tax": 12.00,
          "total": 161.99,
          "created": "2024-01-15T10:00:00Z"
        }
        """;

    private const string OrderNormalized = """
        {
          "id": "ORD-9821",
          "customer": {
            "name": "Jane Smith",
            "email": "jane@example.com"
          },
          "status": "awaiting_fulfillment",
          "shipping": {
            "street": "123 Main St",
            "city": "Springfield",
            "zip": "62701",
            "country": "US"
          },
          "amount": {
            "subtotal": 149.99,
            "tax": 12.00,
            "total": 161.99
          },
          "created_at": "2024-01-15T10:00:00Z"
        }
        """;

    [Test]
    public void Scenario1_OrderNormalization_AnalysisDetectsAllStructuralChanges()
    {
        var result = Analyze(OrderLegacy, OrderNormalized,
            intent: "normalize order schema: group customer, shipping, amount; rename top-level fields");

        PrintGap(result, "Order legacy → normalized");

        // The rename heuristic detects identical values at moved paths
        var renames = result.Changes.Where(c => c.ChangeType == "Rename").ToList();
        Console.WriteLine($"\nRename operations detected: {renames.Count}");
        foreach (var r in renames)
            Console.WriteLine($"  {r.SourcePath} → {r.TargetPath}");

        // order_id → id is a rename (same value)
        Assert.That(result.Changes.Any(c => c.ChangeType == "Rename" && c.SourcePath.Contains("order_id")),
            Is.True, "Should detect order_id → id rename");
        // order_status → status is a Mutate (value changes: pending → awaiting_fulfillment)
        Assert.That(result.Changes.Any(c => c.ChangeType == "Mutate" && c.SourcePath.Contains("order_status") || c.SourcePath.Contains("status")),
            Is.True, "Should detect status value change");
        // Structural renames to nested paths
        Assert.That(result.Changes.Any(c => c.TargetPath.Contains("customer.name")),
            Is.True, "Should detect customer.name grouping");
        Assert.That(result.Changes.Any(c => c.TargetPath.Contains("shipping.street")),
            Is.True, "Should detect shipping.street grouping");
        Assert.That(result.Changes.Any(c => c.TargetPath.Contains("amount.subtotal")),
            Is.True, "Should detect amount.subtotal grouping");

        // All rename descriptions must tell the agent the exact commands
        foreach (var rename in renames)
        {
            Assert.That(rename.Description, Does.Contain("copy").IgnoreCase,
                $"Rename at {rename.SourcePath} must mention 'copy' command");
            Assert.That(rename.Description, Does.Contain("remove").IgnoreCase,
                $"Rename at {rename.SourcePath} must mention 'remove' command");
        }
    }

    [Test]
    public void Scenario1_OrderNormalization_ConvergesInTwoIterations()
    {
        var gap = Analyze(OrderLegacy, OrderNormalized);
        PrintGap(gap, "Initial gap");

        // Iteration 1: handle the status value mutation (the one non-rename change)
        // and all the field renames in a single attempt.
        // Agent reads the gap report: Rename descriptions say "copy ... then remove"
        // and Mutate description says "set status = awaiting_fulfillment".
        const string attempt1 = """
            [
              {"command":"copy","fromPath":"$.order_id","toPath":"$.id"},
              {"command":"remove","path":"$.order_id"},
              {"command":"copy","fromPath":"$.customer_name","toPath":"$.customer.name"},
              {"command":"remove","path":"$.customer_name"},
              {"command":"copy","fromPath":"$.customer_email","toPath":"$.customer.email"},
              {"command":"remove","path":"$.customer_email"},
              {"command":"copy","fromPath":"$.ship_to_street","toPath":"$.shipping.street"},
              {"command":"remove","path":"$.ship_to_street"},
              {"command":"copy","fromPath":"$.ship_to_city","toPath":"$.shipping.city"},
              {"command":"remove","path":"$.ship_to_city"},
              {"command":"copy","fromPath":"$.ship_to_zip","toPath":"$.shipping.zip"},
              {"command":"remove","path":"$.ship_to_zip"},
              {"command":"copy","fromPath":"$.ship_to_country","toPath":"$.shipping.country"},
              {"command":"remove","path":"$.ship_to_country"},
              {"command":"copy","fromPath":"$.subtotal","toPath":"$.amount.subtotal"},
              {"command":"remove","path":"$.subtotal"},
              {"command":"copy","fromPath":"$.tax","toPath":"$.amount.tax"},
              {"command":"remove","path":"$.tax"},
              {"command":"copy","fromPath":"$.total","toPath":"$.amount.total"},
              {"command":"remove","path":"$.total"},
              {"command":"copy","fromPath":"$.created","toPath":"$.created_at"},
              {"command":"remove","path":"$.created"},
              {"command":"copy","fromPath":"$.order_status","toPath":"$.status"},
              {"command":"remove","path":"$.order_status"}
            ]
            """;

        var exec1 = Execute(OrderLegacy, attempt1);
        PrintTrace(exec1, "Iteration 1 — full structural migration");

        Assert.That(exec1.Success, Is.True, "Iteration 1 must succeed");
        Assert.That(exec1.Suggestions, Is.Empty, "No suggestions expected when all commands succeed");

        var traceJson1 = JsonSerializer.Serialize(exec1.Trace);
        var remaining1 = Analyze(exec1.Output, OrderNormalized,
            intent: "fix status value to awaiting_fulfillment",
            priorTraceJson: traceJson1);
        PrintGap(remaining1, "Gap after iteration 1");

        // Only the status value mutation should remain (order_status value was "pending",
        // we copied to $.status but didn't change the value)
        Console.WriteLine($"\nIteration 2: fix remaining {remaining1.Changes.Count} changes");

        if (remaining1.Changes.Count > 0)
        {
            var script2 = BuildScriptFromGaps(remaining1.Changes);
            var exec2 = Execute(exec1.Output, script2);
            PrintTrace(exec2, "Iteration 2 — value corrections");

            var final = Analyze(exec2.Output, OrderNormalized);
            PrintGap(final, "Final gap");

            Assert.That(exec2.Success, Is.True, "Iteration 2 must succeed");
            Assert.That(final.Changes, Is.Empty,
                $"Should converge within 2 iterations. Remaining: {final.Summary}");
        }
        else
        {
            Assert.Pass("Converged in 1 iteration — script was fully correct.");
        }
    }

    [Test]
    public void Scenario1_WrongStatusValue_SuggestionsAreHolistic()
    {
        // Simulate a first-attempt script that has the right structure but wrong values
        // AND a path error — both issues must appear in Suggestions at once.
        const string badScript = """
            [
              {"command":"set","path":"$.order_status","value":"in_progress"},
              {"command":"set","path":"$.customer_name","value":"Jane Smith"},
              {"command":"set","path":"$.nonexistent_field","value":"something"}
            ]
            """;

        var exec = Execute(OrderLegacy, badScript);
        PrintTrace(exec, "Bad script with mixed errors");

        // order_status set to "in_progress" instead of "awaiting_fulfillment" → success (value set, but still wrong)
        // customer_name set to same value → success (set was applied, no noop since field exists)
        // nonexistent_field path doesn't match anything → noop
        var noop = exec.Trace.FirstOrDefault(t => t.Outcome == "noop");
        Assert.That(noop, Is.Not.Null, "Should have at least one noop for the nonexistent path");

        Assert.That(exec.Suggestions.Count, Is.GreaterThan(0),
            "Suggestions must be populated for the noop so agent sees all issues in one pass");
        Assert.That(exec.Suggestions[0], Does.Contain("noop").IgnoreCase,
            "Suggestion must identify noop outcome");
        Assert.That(exec.Suggestions[0], Does.Contain("nonexistent_field").Or.Contain("tlio_analyze"),
            "Suggestion must either name the bad path or point to tlio_analyze for guidance");

        Console.WriteLine("\n=== Agent sees these suggestions and fixes ALL issues in next iteration ===");
        foreach (var s in exec.Suggestions)
            Console.WriteLine($"  • {s}");
    }

    // ════════════════════════════════════════════════════════════════════════
    // Scenario 2: User profile migration v1 → v2
    //   Flat schema with prefixed names → grouped schema with semantic containers.
    //   All 8 changes are Renames (same values, different paths).
    //   Verifies that the agent can handle a pure-rename migration in one iteration.
    // ════════════════════════════════════════════════════════════════════════

    private const string UserProfileV1 = """
        {
          "user_id": "USR-441",
          "first_name": "Carlos",
          "last_name": "Mendes",
          "email": "carlos@example.com",
          "phone": "+1-555-0100",
          "account_type": "premium",
          "is_active": true,
          "registered_date": "2022-03-10"
        }
        """;

    private const string UserProfileV2 = """
        {
          "id": "USR-441",
          "name": {
            "first": "Carlos",
            "last": "Mendes"
          },
          "contact": {
            "email": "carlos@example.com",
            "phone": "+1-555-0100"
          },
          "account": {
            "type": "premium",
            "active": true
          },
          "meta": {
            "registered_at": "2022-03-10"
          }
        }
        """;

    [Test]
    public void Scenario2_UserProfileMigration_AnalysisIdentifiesAllRenames()
    {
        var result = Analyze(UserProfileV1, UserProfileV2,
            intent: "migrate user profile to v2: group name, contact, account, meta");

        PrintGap(result, "User profile v1 → v2");

        // All changes should be Rename type since values are identical, only paths differ
        var changeTypes = result.Changes.Select(c => c.ChangeType).ToHashSet();
        Console.WriteLine($"Change types: {string.Join(", ", changeTypes)}");

        Assert.That(result.Changes, Has.Count.GreaterThanOrEqualTo(8),
            "All 8 field moves should produce change items");

        // Every rename description must include copy and remove instructions
        foreach (var change in result.Changes.Where(c => c.ChangeType == "Rename"))
        {
            Assert.That(change.Description, Does.Contain("copy").IgnoreCase & Does.Contain("remove").IgnoreCase,
                $"Rename from {change.SourcePath} must include copy+remove instructions");
        }
    }

    [Test]
    public void Scenario2_UserProfileMigration_ConvergesInOneIteration()
    {
        var gap = Analyze(UserProfileV1, UserProfileV2);
        PrintGap(gap, "Initial gap");

        // Build the complete migration script from the gap report
        var script = BuildScriptFromGaps(gap.Changes);
        Console.WriteLine($"\nGenerated script:\n{JArray.Parse(script).ToString(Newtonsoft.Json.Formatting.Indented)}");

        var exec = Execute(UserProfileV1, script);
        PrintTrace(exec, "Migration execution");

        Assert.That(exec.Success, Is.True, "Migration must succeed");
        Assert.That(exec.Suggestions, Is.Empty,
            "No noop/failure suggestions expected — all paths must exist in source");

        var remaining = Analyze(exec.Output, UserProfileV2);
        PrintGap(remaining, "Post-migration gap");

        Assert.That(remaining.Changes, Is.Empty,
            $"Should converge in 1 iteration. Remaining: {remaining.Summary}");
    }

    // ════════════════════════════════════════════════════════════════════════
    // Scenario 3: Wrong path → noop → Suggestions guide correction
    //   Product catalog entry with deeply nested variant data.
    //   Agent's first attempt uses wrong flat paths → noops everywhere.
    //   Suggestions and refined gap report provide the correct nested paths.
    // ════════════════════════════════════════════════════════════════════════

    private const string ProductCatalogEntry = """
        {
          "sku": "SKU-7741",
          "title": "Wireless Headphones Pro",
          "brand": "Audivox",
          "categories": ["electronics", "audio"],
          "variant": {
            "color": "midnight black",
            "weight_grams": 285,
            "stock": 42,
            "price_usd": 89.99
          },
          "published": false
        }
        """;

    private const string ProductCatalogEntryUpdated = """
        {
          "sku": "SKU-7741",
          "title": "Wireless Headphones Pro",
          "brand": "Audivox",
          "categories": ["electronics", "audio"],
          "variant": {
            "color": "midnight black",
            "weight_grams": 285,
            "stock": 38,
            "price_usd": 79.99
          },
          "published": true,
          "last_updated": "2024-11-15T14:00:00Z"
        }
        """;

    [Test]
    public void Scenario3_ProductUpdate_WrongPathNoopThenSuggestionsGuideCorrection()
    {
        var gap = Analyze(ProductCatalogEntry, ProductCatalogEntryUpdated,
            intent: "update variant stock and price, publish product, add last_updated");
        PrintGap(gap, "Product update gap");

        // Iteration 1: agent uses wrong paths (forgets the 'variant.' prefix)
        const string wrongAttempt = """
            [
              {"command":"set","path":"$.stock","value":38},
              {"command":"set","path":"$.price_usd","value":79.99},
              {"command":"set","path":"$.published","value":true},
              {"command":"add","path":"$.last_updated","value":"2024-11-15T14:00:00Z"}
            ]
            """;

        var exec1 = Execute(ProductCatalogEntry, wrongAttempt);
        PrintTrace(exec1, "Iteration 1 — wrong nested paths");

        // stock and price_usd don't exist at root → noop
        var noops = exec1.Trace.Where(t => t.Outcome == "noop").ToList();
        Assert.That(noops.Count, Is.GreaterThanOrEqualTo(2),
            "stock and price_usd at root level must be noops");

        // Suggestions must expose ALL noop issues in one view
        Assert.That(exec1.Suggestions.Count, Is.GreaterThanOrEqualTo(2),
            "Each noop must produce a suggestion so agent can fix all issues at once");
        foreach (var suggestion in exec1.Suggestions.Where(s => s.StartsWith("[noop]") || s.StartsWith("[failure]")))
        {
            Assert.That(suggestion, Does.Contain("noop").IgnoreCase,
                "Each suggestion must identify noop outcome");
            Assert.That(suggestion, Does.Contain("tlio_analyze").Or.Contain("path"),
                "Each suggestion must direct the agent toward the correct path source");
        }

        // Gap report (refined with prior trace) shows the correct nested paths
        var traceJson1 = JsonSerializer.Serialize(exec1.Trace);
        var remaining1 = Analyze(exec1.Output, ProductCatalogEntryUpdated,
            intent: "variant fields need 'variant.' prefix; published and last_updated at root level",
            priorTraceJson: traceJson1);
        PrintGap(remaining1, "Refined gap — shows correct nested paths");

        // The gap must now clearly show the correct nested paths
        Assert.That(remaining1.Changes.Any(c => c.SourcePath.Contains("variant.stock")),
            Is.True, "Gap must show variant.stock as the correct path");
        Assert.That(remaining1.Changes.Any(c => c.SourcePath.Contains("variant.price_usd")),
            Is.True, "Gap must show variant.price_usd as the correct path");

        // Iteration 2: agent reads the refined gap and uses correct paths
        const string correctAttempt = """
            [
              {"command":"set","path":"$.variant.stock","value":38},
              {"command":"set","path":"$.variant.price_usd","value":79.99},
              {"command":"set","path":"$.published","value":true},
              {"command":"add","path":"$.last_updated","value":"2024-11-15T14:00:00Z"}
            ]
            """;

        var exec2 = Execute(ProductCatalogEntry, correctAttempt);
        PrintTrace(exec2, "Iteration 2 — correct nested paths");

        Assert.That(exec2.Success, Is.True, "Iteration 2 must succeed");
        Assert.That(exec2.Suggestions, Is.Empty, "No suggestions expected when all commands succeed");

        var final = Analyze(exec2.Output, ProductCatalogEntryUpdated);
        PrintGap(final, "Final gap");

        Assert.That(final.Changes, Is.Empty,
            $"Should converge in 2 iterations. Remaining: {final.Summary}");
    }

    // ════════════════════════════════════════════════════════════════════════
    // Scenario 4: Software project ticket schema evolution
    //   Ticket management system migrates from v1 to v2 format.
    //   Mixed changes: status enum, structural grouping, new required fields.
    //   Complexity: >10 changes, requires 2–3 iterations to fully resolve.
    // ════════════════════════════════════════════════════════════════════════

    private const string TicketV1 = """
        {
          "ticket_id": "TKT-5502",
          "summary": "Login page crashes on Safari 17",
          "type": "bug",
          "severity": "critical",
          "status": "open",
          "reporter": "bob@example.com",
          "assignee": "alice@example.com",
          "created_at": "2024-10-01T09:00:00Z",
          "updated_at": "2024-10-03T11:45:00Z",
          "labels": ["frontend", "safari", "auth"]
        }
        """;

    private const string TicketV2 = """
        {
          "id": "TKT-5502",
          "title": "Login page crashes on Safari 17",
          "classification": {
            "type": "bug",
            "severity": "critical"
          },
          "workflow": {
            "status": "in_progress",
            "assignee": "alice@example.com"
          },
          "people": {
            "reporter": "bob@example.com"
          },
          "timestamps": {
            "created": "2024-10-01T09:00:00Z",
            "updated": "2024-10-03T11:45:00Z"
          },
          "tags": ["frontend", "safari", "auth"],
          "priority": 1
        }
        """;

    [Test]
    public void Scenario4_TicketSchemaEvolution_AnalysisDetectsAllChanges()
    {
        var result = Analyze(TicketV1, TicketV2,
            intent: "evolve ticket schema: group classification, workflow, people, timestamps; change status to in_progress; add priority");

        PrintGap(result, "Ticket v1 → v2 gap");

        // status moves from $.status ("open") to $.workflow.status ("in_progress"):
        // different path AND different value → Remove old + Add new (not Rename or Mutate)
        Assert.That(result.Changes.Any(c => c.ChangeType == "Remove" && c.SourcePath == "$.status"),
            Is.True, "Should detect removal of old $.status field");
        Assert.That(result.Changes.Any(c => c.ChangeType == "Add" && c.TargetPath == "$.workflow.status"),
            Is.True, "Should detect addition of $.workflow.status with new value");

        // priority is a new field → Add
        Assert.That(result.Changes.Any(c => c.ChangeType == "Add" && c.TargetPath.Contains("priority")),
            Is.True, "Should detect priority Add");

        // Structural groupings → Rename heuristic fires for fields with same values
        Assert.That(result.Changes.Any(c => c.TargetPath.Contains("classification")),
            Is.True, "Should detect classification grouping");
        Assert.That(result.Changes.Any(c => c.TargetPath.Contains("timestamps")),
            Is.True, "Should detect timestamps grouping");

        // All Add descriptions must include json_value for scalar new fields
        foreach (var add in result.Changes.Where(c => c.ChangeType == "Add"))
        {
            if (!add.Description.Contains("{}") && !add.Description.Contains("[]"))
                Assert.That(add.Description, Does.Contain("json_value:"),
                    $"Add at {add.TargetPath} must specify json_value so agent can write the correct value");
        }

        // All Mutate descriptions must specify the exact target value
        foreach (var mutate in result.Changes.Where(c => c.ChangeType == "Mutate"))
            Assert.That(mutate.Description, Does.Contain("json_value:"),
                $"Mutate at {mutate.SourcePath} must specify json_value");
    }

    [Test]
    public void Scenario4_TicketSchemaEvolution_ConvergesWithinThreeIterations()
    {
        const int maxIterations = 3;
        var current = TicketV1;
        string? priorTraceJson = null;

        for (var iteration = 1; iteration <= maxIterations; iteration++)
        {
            var gap = Analyze(current, TicketV2,
                intent: "group classification/workflow/people/timestamps; change status; add priority",
                priorTraceJson: priorTraceJson);
            PrintGap(gap, $"Iteration {iteration} gap");

            if (gap.Changes.Count == 0)
            {
                Console.WriteLine($"\nConverged after {iteration - 1} iteration(s).");
                break;
            }

            var script = BuildScriptFromGaps(gap.Changes);
            Console.WriteLine($"\nScript:\n{JArray.Parse(script).ToString(Newtonsoft.Json.Formatting.Indented)}");

            var exec = Execute(current, script);
            PrintTrace(exec, $"Iteration {iteration} execution");

            Assert.That(exec.Success, Is.True, $"Iteration {iteration} must not fail");

            priorTraceJson = JsonSerializer.Serialize(exec.Trace);
            current = exec.Output;
        }

        var finalGap = Analyze(current, TicketV2);
        PrintGap(finalGap, "Final verification");

        Assert.That(finalGap.Changes, Is.Empty,
            $"Must converge within {maxIterations} iterations. Remaining: {finalGap.Summary}");
    }

    // ════════════════════════════════════════════════════════════════════════
    // Scenario 5: IoT device telemetry schema migration
    //   Sensor reading v1 (flat) → v2 (grouped measurement + metadata).
    //   Tests nested reorganization with numeric and boolean values.
    //   Verifies that boolean json_value handling is correct (true, not "true").
    // ════════════════════════════════════════════════════════════════════════

    private const string TelemetryV1 = """
        {
          "device_id": "SEN-0042",
          "firmware": "1.3.2",
          "event": "temperature_alert",
          "value": 87.4,
          "unit": "celsius",
          "threshold_exceeded": true,
          "battery_pct": 64,
          "recorded_at": "2024-11-20T06:15:00Z"
        }
        """;

    private const string TelemetryV2 = """
        {
          "device_id": "SEN-0042",
          "firmware": "1.3.2",
          "event_type": "temperature_alert",
          "measurement": {
            "value": 87.4,
            "unit": "celsius",
            "threshold_exceeded": true
          },
          "device_health": {
            "battery_pct": 64
          },
          "recorded_at": "2024-11-20T06:15:00Z"
        }
        """;

    [Test]
    public void Scenario5_TelemetryMigration_AnalysisHandlesBooleanAndNumericTypes()
    {
        var result = Analyze(TelemetryV1, TelemetryV2,
            intent: "group measurement fields and device_health; rename event to event_type");

        PrintGap(result, "Telemetry v1 → v2 gap");

        // Verify that boolean value is detected as canonical 'true' not "True"
        var boolChange = result.Changes.FirstOrDefault(c => c.SourcePath.Contains("threshold_exceeded"));
        Assert.That(boolChange, Is.Not.Null, "threshold_exceeded must appear in changes");
        if (boolChange!.ChangeType is "Rename" or "Add")
        {
            // If it's an Add (grouped under measurement), description must have json_value: true
            if (boolChange.ChangeType == "Add")
                Assert.That(boolChange.Description, Does.Contain("json_value: true"),
                    "Boolean json_value must be 'true' (lowercase), not 'True' or '\"true\"'");
        }

        // Numeric value preservation check
        var numChange = result.Changes.FirstOrDefault(c =>
            c.SourcePath.Contains("value") || c.TargetPath.Contains("measurement.value"));
        if (numChange is { ChangeType: "Add" })
            Assert.That(numChange.Description, Does.Contain("json_value: 87.4"),
                "Numeric json_value must be unquoted '87.4', not '\"87.4\"'");
    }

    [Test]
    public void Scenario5_TelemetryMigration_ConvergesInOneIteration()
    {
        var gap = Analyze(TelemetryV1, TelemetryV2);
        PrintGap(gap, "Initial gap");

        var script = BuildScriptFromGaps(gap.Changes);
        Console.WriteLine($"\nGenerated script:\n{JArray.Parse(script).ToString(Newtonsoft.Json.Formatting.Indented)}");

        var exec = Execute(TelemetryV1, script);
        PrintTrace(exec, "Migration execution");

        Assert.That(exec.Success, Is.True);

        var remaining = Analyze(exec.Output, TelemetryV2);
        PrintGap(remaining, "Post-migration gap");

        Assert.That(remaining.Changes, Is.Empty,
            $"Telemetry migration should converge in 1 iteration. Remaining: {remaining.Summary}");
    }

    // ════════════════════════════════════════════════════════════════════════
    // Scenario 6: Summary quality — all scalar descriptions contain json_value
    //   Validates that every Add and Mutate change in every scenario above
    //   provides an actionable json_value marker so the agent can build a
    //   type-correct script without guessing.
    // ════════════════════════════════════════════════════════════════════════

    [Test]
    public void AllScenarios_ScalarDescriptions_AlwaysContainJsonValue()
    {
        var scenarios = new (string Input, string Target, string Label)[]
        {
            (OrderLegacy,        OrderNormalized,          "order normalization"),
            (UserProfileV1,      UserProfileV2,            "user profile migration"),
            (ProductCatalogEntry, ProductCatalogEntryUpdated, "product catalog update"),
            (TicketV1,           TicketV2,                 "ticket schema evolution"),
            (TelemetryV1,        TelemetryV2,              "telemetry migration"),
        };

        var failures = new List<string>();

        foreach (var (input, target, label) in scenarios)
        {
            var result = Analyze(input, target);
            foreach (var change in result.Changes.Where(c => c.ChangeType is "Mutate" or "Add"))
            {
                // Skip container nodes (objects/arrays) — they don't carry scalar values
                if (change.Description.Contains("{}") || change.Description.Contains("[]"))
                    continue;
                if (!change.Description.Contains("json_value:"))
                    failures.Add($"[{label}] {change.ChangeType} at '{change.TargetPath ?? change.SourcePath}': no json_value in description — agent cannot infer correct type");
            }
        }

        if (failures.Count > 0)
        {
            Console.WriteLine("Missing json_value guidance:");
            failures.ForEach(f => Console.WriteLine($"  {f}"));
        }

        Assert.That(failures, Is.Empty,
            "Every scalar Add/Mutate description must include json_value: so agent writes type-correct scripts");
    }

    // ── Simulate AI agent: build a script from a gap report ──────────────────
    // Reads the json_value: marker from descriptions and constructs type-correct commands.
    // This mirrors the minimal reasoning an AI agent applies when given a gap report.

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

    // Parses the "json_value: <value>." marker embedded in descriptions.
    // JToken.Parse preserves correct type: true→bool, 42→int, "Bob"→string.
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
