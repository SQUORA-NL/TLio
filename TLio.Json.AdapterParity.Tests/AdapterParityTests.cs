using System.Text.Json.Nodes;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using NUnit.Framework;
using TLio.Client;
using TLio.Extensions.ETL;
using TLio.Extensions.Looping;
using TLio.Extensions.Math;
using TLio.Extensions.Text;
using TLio.Extensions.TimeDate;
using TLio.Json;
using TLio.Json.SystemText;
using TLio.JsonPath;

namespace TLio.Json.AdapterParity.Tests;

/// <summary>
/// Every (input, script, result) fixture the repository keeps for TLio.Json, run through BOTH JSON
/// adapters. The output documents — canonical JSON — must be identical, and so must whether the run
/// succeeded or threw. The expected <c>result</c> in the fixture is not consulted: the other test
/// projects already hold each adapter to it; this holds the adapters to each other, on all of it.
/// </summary>
[TestFixture]
public class AdapterParityTests
{
    private static ScriptEngine<JToken> NewtonsoftEngine()
    {
        var options = ParseOptions<JToken>.CreateDefault();
        Register(options);
        return new ScriptEngine<JToken>(options.CommandsProvider, options.FunctionsProvider);
    }

    private static ScriptEngine<JsonNode> SystemTextEngine()
    {
        var options = ParseOptions<JsonNode>.CreateDefault();
        Register(options);
        return new ScriptEngine<JsonNode>(options.CommandsProvider, options.FunctionsProvider);
    }

    private static void Register<TNode>(ParseOptions<TNode> options)
    {
        options.FunctionsProvider.RegisterText<TNode>();
        options.FunctionsProvider.RegisterMath<TNode>();
        options.FunctionsProvider.RegisterTimeDate<TNode>();
        options.CommandsProvider.RegisterETL<TNode>();
        options.CommandsProvider.RegisterLooping<TNode>();
    }

    public sealed record Fixture(string Name, string InputJson, string ScriptJson)
    {
        public override string ToString() => Name;
    }

    public static IEnumerable<TestCaseData> Fixtures()
    {
        var root = Path.Combine(TestContext.CurrentContext.TestDirectory, "E2e");
        foreach (var file in Directory.EnumerateFiles(root, "*.json", SearchOption.AllDirectories).OrderBy(f => f, StringComparer.Ordinal))
        {
            if (file.EndsWith("sweep.json", StringComparison.Ordinal)) continue;

            using var doc = System.Text.Json.JsonDocument.Parse(File.ReadAllText(file));
            if (doc.RootElement.ValueKind != System.Text.Json.JsonValueKind.Object
                || !doc.RootElement.TryGetProperty("input", out var input)
                || !doc.RootElement.TryGetProperty("script", out var script))
                continue;

            var name = Path.GetRelativePath(root, file).Replace('\\', '/');
            yield return new TestCaseData(new Fixture(name, input.GetRawText(), script.GetRawText())).SetName(name);
        }
    }

    internal sealed record Outcome(bool Threw, bool Success, string? Json, string Detail);

    /// <summary>
    /// The repository's own fixture loaders read documents with <c>DateParseHandling.None</c> — date-looking
    /// strings stay strings, as they are in System.Text.Json. Doing the same here keeps this a comparison of
    /// the two adapters' JSONPath and command behaviour; whether a document <em>parser</em> types dates is a
    /// different matter (see the README's note on <see cref="JsonPathOptions.EmulateNewtonsoftDates"/>).
    /// </summary>
    private static JToken ParseWithoutDates(string json)
    {
        using var reader = new JsonTextReader(new StringReader(json)) { DateParseHandling = DateParseHandling.None };
        return JToken.Load(reader);
    }

    /// <summary>The System.Text.Json context, with the engine told the document has no typed dates to emulate.</summary>
    private static TLio.Core.Models.ExecutionContext<JsonNode> SystemTextContext() => new()
    {
        ItemsFetcher = new SystemTextJsonPathItemsFetcher(new JsonPathEngine(new JsonPathOptions { EmulateNewtonsoftDates = false })),
        NodeAdapter = new SystemTextJsonNodeAdapter(),
        Logger = new TLio.Core.Models.ExecutionLogger(),
    };

    internal static Outcome RunNewtonsoftPublic(string i, string s) => RunNewtonsoft(i, s);
    internal static Outcome RunSystemTextPublic(string i, string s) => RunSystemText(i, s);
    internal static string CanonicalPublic(string j) => Canonical(j);

    private static Outcome RunNewtonsoft(string inputJson, string scriptJson)
    {
        try
        {
            var context = JsonExecutionContext.CreateDefault();
            var result = NewtonsoftEngine().Execute(scriptJson, ParseWithoutDates(inputJson), context);
            return new Outcome(false, result.Success, result.Data?.ToString(Formatting.None), Log(context.GetLogEntries()));
        }
        catch (Exception ex)
        {
            return new Outcome(true, false, null, ex.GetType().Name + ": " + ex.Message);
        }
    }

    private static Outcome RunSystemText(string inputJson, string scriptJson)
    {
        try
        {
            var context = SystemTextContext();
            var result = SystemTextEngine().Execute(scriptJson, JsonNode.Parse(inputJson)!, context);
            return new Outcome(false, result.Success, result.Data?.ToJsonString(), Log(context.GetLogEntries()));
        }
        catch (Exception ex)
        {
            return new Outcome(true, false, null, ex.GetType().Name + ": " + ex.Message);
        }
    }

    private static string Log(IEnumerable<TLio.Core.Models.Logging.LogEntry> entries) =>
        string.Join("\n", entries.Select(e => $"[{e.Level}] {e.Group}: {e.Message}"));

    /// <summary>
    /// Canonical form: the document with object members in ordinal key order and numbers by value — the
    /// comparison JSON itself defines. Member <em>order</em> is checked separately, below.
    /// </summary>
    private static string Canonical(string json)
    {
        var node = JsonNode.Parse(json);
        return Canonicalize(node)?.ToJsonString() ?? "null";
    }

    private static JsonNode? Canonicalize(JsonNode? node)
    {
        switch (node)
        {
            case JsonObject o:
                var sorted = new JsonObject();
                foreach (var kv in o.OrderBy(kv => kv.Key, StringComparer.Ordinal)) sorted[kv.Key] = Canonicalize(kv.Value);
                return sorted;
            case JsonArray a:
                var arr = new JsonArray();
                foreach (var item in a) arr.Add(Canonicalize(item));
                return arr;
            case JsonValue v when v.TryGetValue<decimal>(out var d):
                // 1, 1.0 and 1e0 are the same JSON number.
                return JsonValue.Create(d);
            default:
                return node?.DeepClone();
        }
    }

    [TestCaseSource(nameof(Fixtures))]
    public void Both_adapters_produce_the_same_document(Fixture fixture)
    {
        var newtonsoft = RunNewtonsoft(fixture.InputJson, fixture.ScriptJson);
        var systemText = RunSystemText(fixture.InputJson, fixture.ScriptJson);

        Assert.That(systemText.Threw, Is.EqualTo(newtonsoft.Threw),
            $"one adapter threw and the other did not.\n  Newtonsoft:  {newtonsoft.Detail}\n  SystemText:  {systemText.Detail}");
        Assert.That(systemText.Success, Is.EqualTo(newtonsoft.Success),
            $"the adapters disagree about success.\n  Newtonsoft log:\n{newtonsoft.Detail}\n  SystemText log:\n{systemText.Detail}");

        if (newtonsoft.Json == null) return;
        Assert.That(Canonical(systemText.Json!), Is.EqualTo(Canonical(newtonsoft.Json)),
            $"output documents differ.\n  Newtonsoft: {newtonsoft.Json}\n  SystemText: {systemText.Json}");
    }

    [Test]
    public void The_sweep_of_every_registered_command_and_function_gives_the_same_document_on_both_adapters()
    {
        var script = File.ReadAllText(Path.Combine(TestContext.CurrentContext.TestDirectory, "E2e", "Sweep", "sweep.json"));
        var newtonsoft = RunNewtonsoft("{}", script);
        var systemText = RunSystemText("{}", script);

        Assert.That(systemText.Threw, Is.EqualTo(newtonsoft.Threw), $"Newtonsoft: {newtonsoft.Detail}\nSystemText: {systemText.Detail}");
        Assert.That(newtonsoft.Json, Is.Not.Null.And.Not.Empty);
        Assert.That(Canonical(systemText.Json!), Is.EqualTo(Canonical(newtonsoft.Json!)));
    }

    [Test]
    public void The_fixture_set_is_large_enough_to_mean_something()
    {
        var count = Fixtures().Count();
        TestContext.Out.WriteLine($"{count} fixtures run through both adapters");
        Assert.That(count, Is.GreaterThan(150));
    }
}
