using System.Diagnostics;
using System.Text;
using System.Text.Json.Nodes;
using System.Xml.Linq;
using Newtonsoft.Json.Linq;
using NUnit.Framework;
using TLio.Client;
using TLio.Core.Models;
using TLio.FormatConverter;
using TLio.FormatConverter.Json;
using TLio.FormatConverter.Xml;
using TLio.FormatConverter.Yaml;
using TLio.Json;
using TLio.Json.SystemText;
using TLio.Xml;
using TLio.Yaml;
using Converter = global::TLio.FormatConverter.Core.FormatConverter;
using ConversionSettings = global::TLio.FormatConverter.Core.ConversionSettings;

namespace TLio.Parity.Tests;

/// <summary>
/// A document that carries a few very large string values — three base64-encoded files of
/// 250–350 KB each, an XML order converted to JSON and then edited — must not make the
/// <i>commands</i> slower. Two different costs hide behind "it got slow", and these tests keep
/// them apart:
/// <list type="bullet">
/// <item><b>Per-command cost</b> — what one command or function costs once the document is
/// parsed. It must not depend on how big the strings in the document are: a command touches the
/// nodes its path selects, never the whole tree. Asserted as a <i>ratio</i> (big document /
/// small document) so the check holds on any machine.</item>
/// <item><b>Per-execution cost</b> — parsing the document, converting it between formats and
/// serialising it back. That one <i>does</i> grow with the size of the document, linearly, and is
/// paid once per execution (once per <c>convert</c> section in a multi-format script), not once
/// per command. Asserted to stay linear, and to be paid once however many commands follow.</item>
/// </list>
/// Both facts together explain the symptom "everything is slower on a big document": a host that
/// runs one command per execution (or one per <c>convert</c> section) pays the per-execution cost
/// on every command, which dwarfs a command that costs microseconds.
/// <para>
/// The ratio and linearity checks are cheap and run on every build. The numbers table is
/// <c>[Explicit]</c>: <c>dotnet test TLio.Parity.Tests --filter "Name~Report"</c>.
/// </para>
/// </summary>
[TestFixture]
public class BigObjectPerformanceTests
{
    private const int BlobChars = 300_000;     // one base64 file, in characters
    private const int BlobCount = 3;

    // A big document may cost at most this many times a small one per command. Measured: ~1.
    // The headroom is for timer noise on a microsecond-sized measurement, not for real growth —
    // an O(document) step inside a command would be hundreds of times, not a few.
    private const double MaxPerCommandRatio = 4.0;

    // ── Document ──────────────────────────────────────────────────────────────

    private static string OrderXml(int blobChars, int blobCount = BlobCount)
    {
        var rnd = new Random(1);
        string Blob()
        {
            var bytes = new byte[blobChars / 4 * 3];
            rnd.NextBytes(bytes);
            return Convert.ToBase64String(bytes);
        }

        var sb = new StringBuilder("<order><id>42</id><customer><name>Ann</name></customer><items>");
        for (var i = 0; i < 5; i++)
            sb.Append($"<item><sku>S{i}</sku><qty>{i}</qty></item>");
        sb.Append("</items><files>");
        for (var i = 0; i < blobCount; i++)
            sb.Append($"<file><name>f{i}</name><content>{Blob()}</content></file>");
        return sb.Append("</files></order>").ToString();
    }

    private static Converter Converter3()
    {
        var converter = new Converter();
        converter.Register(new JsonFormatAdapter());
        converter.Register(new XmlFormatAdapter());
        converter.Register(new YamlFormatAdapter());
        return converter;
    }

    /// <summary>The XML order as the JSON a script would edit after <c>convert</c>.</summary>
    private static string OrderJson(int blobChars, int blobCount = BlobCount) =>
        Converter3().Convert("xml", OrderXml(blobChars, blobCount), "json", new ConversionSettings());

    // ── Scripts: each one a command or function that must have an effect ──────

    private sealed record Case(string Name, string JsonScript, string XmlScript, Func<string, bool> Applied);

    // JSON paths use the converted shape (items and files are arrays); XML paths are XPath.
    private static readonly Case[] Cases =
    [
        new("put", """[{"command":"put","path":"$.order.status","value":"x"}]""",
            """[{"command":"put","path":"/order/status","value":"x"}]""",
            o => o.Contains("status")),
        new("add", """[{"command":"add","path":"$.order.extra","value":1}]""",
            """[{"command":"put","path":"/order/extra","value":"1"}]""",
            o => o.Contains("extra")),
        new("copy", """[{"command":"copy","fromPath":"$.order.id","toPath":"$.order.id2"}]""",
            """[{"command":"copy","fromPath":"/order/id","toPath":"/order/id2"}]""",
            o => o.Contains("id2")),
        new("wildcard", """[{"command":"put","path":"$.order.items[*].flag","value":true}]""",
            """[{"command":"put","path":"/order/items/item/flag","value":"y"}]""",
            o => o.Contains("flag")),
        new("recursive", """[{"command":"put","path":"$..sku","value":"z"}]""",
            """[{"command":"put","path":"//sku","value":"z"}]""",
            o => o.Contains("\"z\"") || o.Contains(">z<")),
        new("fn-fetch", """[{"command":"put","path":"$.order.t","value":"=fetch($.order.customer.name)"}]""",
            "<script><put path=\"/order/t\"><value>=fetch(/order/id)</value></put></script>",
            o => o.Contains("\"t\"") || o.Contains("<t>42</t>")),
        new("fn-exists", """[{"command":"put","path":"$.order.u","value":"=exists($.order.customer.name)"}]""",
            "<script><put path=\"/order/u\"><value>=exists(/order/customer/name)</value></put></script>",
            o => o.Contains("\"u\"") || o.Contains("<u>true</u>")),
        new("ifElse",
            """[{"command":"ifElse","condition":"=exists($.order.id)","ifScript":[{"command":"put","path":"$.order.a","value":1}]}]""",
            """[{"command":"ifElse","condition":"=exists(/order/id)","ifScript":[{"command":"put","path":"/order/a","value":"1"}]}]""",
            o => o.Contains("\"a\"") || o.Contains("<a>")),
        // A command that targets one of the big strings itself reads one value, once.
        new("copy-blob", """[{"command":"copy","fromPath":"$.order.files[0].content","toPath":"$.order.copy"}]""",
            """[{"command":"copy","fromPath":"/order/files/file[1]/content","toPath":"/order/copy"}]""",
            o => o.Contains("copy")),
    ];

    // ── Measurement ───────────────────────────────────────────────────────────

    /// <summary>Best of <paramref name="rounds"/> rounds of <paramref name="iterations"/> calls, per call, in ms.</summary>
    private static double BestMs(Action action, int iterations, int rounds = 5)
    {
        for (var i = 0; i < 20; i++) action();                       // warm up, JIT included
        var best = double.MaxValue;
        for (var r = 0; r < rounds; r++)
        {
            GC.Collect();
            var sw = Stopwatch.StartNew();
            for (var i = 0; i < iterations; i++) action();
            best = Math.Min(best, sw.Elapsed.TotalMilliseconds / iterations);
        }
        return best;
    }

    private static void AssertFlat(string adapter, string name, double smallMs, double bigMs)
    {
        TestContext.WriteLine($"{adapter,-14} {name,-10} small {smallMs * 1000,8:F1} µs   big {bigMs * 1000,8:F1} µs   x{bigMs / smallMs:F2}");
        Assert.That(bigMs / smallMs, Is.LessThanOrEqualTo(MaxPerCommandRatio),
            $"{adapter} '{name}' costs {bigMs * 1000:F1} µs on a document with {BlobCount} x {BlobChars / 1000} KB " +
            $"strings versus {smallMs * 1000:F1} µs on one without — a command's cost must not depend on the size " +
            "of strings it does not touch.");
    }

    // ── Per-command cost does not grow with the size of the strings ───────────

    [Test]
    public void Newtonsoft_PerCommandCost_DoesNotGrowWithBlobSize()
    {
        var options = ParseOptions<JToken>.CreateDefault();
        var engine = new ScriptEngine<JToken>(options.CommandsProvider, options.FunctionsProvider);
        var small = JToken.Parse(OrderJson(16));
        var big = JToken.Parse(OrderJson(BlobChars));

        foreach (var c in Cases)
        {
            // The script has to do what it says, or the timing below measures an error path.
            var probe = engine.Execute(c.JsonScript, big.DeepClone(), JsonExecutionContext.CreateDefault());
            Assert.That(probe.Success, Is.True, c.Name);
            Assert.That(c.Applied(probe.Data.ToString(Newtonsoft.Json.Formatting.None)), Is.True, $"{c.Name} had no effect");

            double Run(JToken doc) => BestMs(
                () => engine.Execute(c.JsonScript, doc, JsonExecutionContext.CreateDefault()), 2_000);
            AssertFlat("Newtonsoft", c.Name, Run(small), Run(big));
        }
    }

    [Test]
    public void SystemText_PerCommandCost_DoesNotGrowWithBlobSize()
    {
        var options = ParseOptions<JsonNode>.CreateDefault();
        var engine = new ScriptEngine<JsonNode>(options.CommandsProvider, options.FunctionsProvider);
        var small = JsonNode.Parse(OrderJson(16))!;
        var big = JsonNode.Parse(OrderJson(BlobChars))!;

        foreach (var c in Cases)
        {
            var probe = engine.Execute(c.JsonScript, big.DeepClone(), SystemTextJsonExecutionContext.CreateDefault());
            Assert.That(probe.Success, Is.True, c.Name);
            Assert.That(c.Applied(probe.Data.ToJsonString()), Is.True, $"{c.Name} had no effect");

            double Run(JsonNode doc) => BestMs(
                () => engine.Execute(c.JsonScript, doc, SystemTextJsonExecutionContext.CreateDefault()), 2_000);
            AssertFlat("SystemText", c.Name, Run(small), Run(big));
        }
    }

    [TestCase(false, TestName = "Xml_PerCommandCost_DoesNotGrowWithBlobSize(slash paths)")]
    [TestCase(true, TestName = "Xml_PerCommandCost_DoesNotGrowWithBlobSize(native XPath)")]
    public void Xml_PerCommandCost_DoesNotGrowWithBlobSize(bool nativeXPath)
    {
        var options = ParseOptions<XElement>.CreateDefault();
        var engine = new ScriptEngine<XElement>(options.CommandsProvider, options.FunctionsProvider).UseXmlScripts();
        var adapter = new XmlNodeAdapter();
        var small = adapter.Parse(OrderXml(16));
        var big = adapter.Parse(OrderXml(BlobChars));
        ExecutionContext<XElement> Context() =>
            nativeXPath ? XmlExecutionContext.CreateWithNativeXPath() : XmlExecutionContext.CreateWithSlashPaths();

        // The XML copy-blob path is an XPath expression; the slash fetcher has no positional step.
        foreach (var c in Cases.Where(c => nativeXPath || c.Name != "copy-blob"))
        {
            var probe = engine.Execute(c.XmlScript, adapter.Parse(OrderXml(BlobChars)), Context());
            Assert.That(probe.Success, Is.True, c.Name);
            var xmlOut = probe.Data.ToString(SaveOptions.DisableFormatting);
            Assert.That(c.Applied(xmlOut), Is.True,
                $"{c.Name} had no effect. Output ends …{xmlOut[^120..]}");

            double Run(XElement doc) => BestMs(() => engine.Execute(c.XmlScript, doc, Context()), 2_000);
            AssertFlat(nativeXPath ? "Xml (native)" : "Xml (slash)", c.Name, Run(small), Run(big));
        }
    }

    // ── Per-execution cost: linear in the size of the document, paid once ─────

    [Test]
    public void Conversion_CostGrowsLinearlyWithBlobSize()
    {
        var converter = Converter3();
        var settings = new ConversionSettings();
        var quarter = OrderXml(BlobChars / 4);
        var full = OrderXml(BlobChars);

        foreach (var target in new[] { "json", "yaml" })
        {
            var tQuarter = BestMs(() => converter.Convert("xml", quarter, target, settings), 20);
            var tFull = BestMs(() => converter.Convert("xml", full, target, settings), 20);
            TestContext.WriteLine($"xml->{target}: quarter {tQuarter:F2} ms   full {tFull:F2} ms   x{tFull / tQuarter:F2} for 4x the data");

            // 4x the data is 4x the work. Superlinear growth (quadratic string handling, say)
            // would show here as well above 4; 10 leaves room for cache effects and noise.
            Assert.That(tFull / tQuarter, Is.LessThan(10), $"xml->{target} grew faster than the document did");
        }
    }

    [Test]
    public void MultiFormatRun_PaysTheBigDocumentOnce_NotPerCommand()
    {
        var runner = MultiFormatRunner();
        var xml = OrderXml(BlobChars);

        string Script(int commands)
        {
            var sb = new StringBuilder("""[{"command":"convert","to":"json"}""");
            for (var i = 0; i < commands; i++)
                sb.Append($$""",{"command":"put","path":"$.order.p{{i}}","value":"=fetch($.order.customer.name)"}""");
            return sb.Append(']').ToString();
        }

        var one = Script(1);
        var many = Script(200);
        Assert.That(runner.Run("xml", xml, many).Success, Is.True);

        var tOne = BestMs(() => runner.Run("xml", xml, one), 5);
        var tMany = BestMs(() => runner.Run("xml", xml, many), 5);
        TestContext.WriteLine($"convert + 1 command: {tOne:F2} ms   convert + 200 commands: {tMany:F2} ms   " +
                              $"({(tMany - tOne) / 199 * 1000:F1} µs per extra command)");

        // The document is parsed, converted and serialised once; the 199 extra commands are
        // microseconds each. Were a command to pay for the document, 200 of them would cost
        // 200x the single one — here they must cost under twice as much.
        // Measured 1.2-2.0 (CI runners pay 5-35 µs per command). A command that paid for the
        // document would cost ~5 ms, making 200 of them ~200x the single one, so 5 still catches it.
        Assert.That(tMany / tOne, Is.LessThan(5.0),
            "extra commands in the same section must not each pay for the size of the document");
    }

    /// <summary>
    /// Time is not the only way a big document slows everything down: a 300 KB string is a
    /// 600 KB object on the large object heap, and every copy of it makes a full (gen2)
    /// collection more likely — which pauses every thread in the process, not just this script.
    /// So the 62 commands after the first must not allocate anything the size of the document.
    /// </summary>
    [TestCase(false, TestName = "ExtraCommands_AllocateNothingProportionalToTheDocument(Newtonsoft)")]
    [TestCase(true, TestName = "ExtraCommands_AllocateNothingProportionalToTheDocument(SystemText)")]
    public void ExtraCommands_AllocateNothingProportionalToTheDocument(bool systemText)
    {
        var runner = MultiFormatRunner(systemText);
        var json = OrderJson(BlobChars);
        var documentBytes = json.Length * 2L;

        string Script(int commands) => "[" + string.Join(",", Enumerable.Range(0, commands).Select(i =>
            $$"""{"command":"put","path":"$.order.p{{i}}","value":"=fetch($.order.customer.name)"}""")) + "]";

        long Allocated(string script)
        {
            for (var i = 0; i < 5; i++) runner.Run("json", json, script);   // warm up: JIT and tiering allocate too
            var least = long.MaxValue;
            for (var i = 0; i < 5; i++)
            {
                var before = GC.GetAllocatedBytesForCurrentThread();
                runner.Run("json", json, script);
                least = Math.Min(least, GC.GetAllocatedBytesForCurrentThread() - before);
            }
            return least;
        }

        var one = Allocated(Script(1));
        var sixtyThree = Allocated(Script(63));
        TestContext.WriteLine($"document {documentBytes / 1048576.0:F1} MB; 1 command allocates {one / 1048576.0:F1} MB, " +
                              $"63 commands {sixtyThree / 1048576.0:F1} MB (+{(sixtyThree - one) / 1024.0:F0} KB for 62 more)");

        Assert.That(sixtyThree - one, Is.LessThan(documentBytes / 2),
            "62 more commands cost ~6 KB each (script text, log lines). One copy of the document per " +
            "command would be 62 x its size; even a single copy among them is over this bound.");
    }

    /// <summary>
    /// What it costs, in memory, to bring a big document in: convert it and parse it. Each stage
    /// has to copy the strings at least once; it must not copy them over and over. Before the
    /// converter and parser stopped making intermediate copies this was about 7.5x (convert) and
    /// 6x (Newtonsoft parse) the document's size, nearly all of it on the large object heap.
    /// </summary>
    [Test]
    public void BringingInABigDocument_AllocatesABoundedMultipleOfItsSize()
    {
        var converter = Converter3();
        var settings = new ConversionSettings();
        var xml = OrderXml(BlobChars);
        var json = converter.Convert("xml", xml, "json", settings);
        var documentBytes = xml.Length * 2L;

        long Allocated(Action action)
        {
            action();                                                   // warm up
            var before = GC.GetAllocatedBytesForCurrentThread();
            action();
            return GC.GetAllocatedBytesForCurrentThread() - before;
        }

        var stages = new (string Name, long Bytes, double MaxTimesDocument)[]
        {
            ("xml -> json",              Allocated(() => converter.Convert("xml", xml, "json", settings)),     6),   // measured 4.4
            ("Newtonsoft parse",         Allocated(() => new JsonNodeAdapter().Parse(json)),                    2),   // 1.0
            ("System.Text.Json parse",   Allocated(() => new SystemTextJsonNodeAdapter().Parse(json)),          2),   // 0.5
        };

        foreach (var s in stages)
            TestContext.WriteLine($"{s.Name,-24} {s.Bytes / 1048576.0,5:F1} MB = {s.Bytes / (double)documentBytes:F1}x the document");
        foreach (var s in stages)
            Assert.That(s.Bytes / (double)documentBytes, Is.LessThanOrEqualTo(s.MaxTimesDocument), s.Name);
    }

    // ── Every command and function, one step at a time ────────────────────────

    /// <summary>
    /// The sweep script touches every registered command and function. Run step by step on a
    /// document with and without the big strings, no step may cost more because of them. The
    /// sweep reads none of the blobs, so any difference is a step doing work on the whole tree.
    /// </summary>
    [TestCase(false, TestName = "Sweep_StepsCostTheSameWithBigStrings(Newtonsoft)")]
    [TestCase(true, TestName = "Sweep_StepsCostTheSameWithBigStrings(SystemText)")]
    public void Sweep_StepsCostTheSameWithBigStrings(bool systemText)
    {
        var script = File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "Sweep", "sweep.json"));
        double[] Steps(bool big) => systemText
            ? SweepStepMs<JsonNode>(script, SweepDoc(big), SystemTextJsonExecutionContext.CreateDefault)
            : SweepStepMs<JToken>(script, SweepDoc(big), JsonExecutionContext.CreateDefault);
        var small = Steps(false);
        var big = Steps(true);

        var worst = Enumerable.Range(0, small.Length).OrderByDescending(i => big[i] - small[i]).First();
        TestContext.WriteLine($"{(systemText ? "SystemText" : "Newtonsoft")}: {small.Length} steps, total small {small.Sum():F2} ms, " +
                              $"big {big.Sum():F2} ms; worst step #{worst}: {small[worst]:F3} -> {big[worst]:F3} ms");

        // Best of several rounds per step, so a stray GC pause does not count. Measured: equal.
        Assert.That(big.Sum(), Is.LessThan(small.Sum() * 3 + 1.0), "the whole sweep got slower with big strings in the document");
        Assert.That(big[worst] - small[worst], Is.LessThan(0.5),
            $"step #{worst} costs {(big[worst] - small[worst]) * 1000:F0} µs more with big strings in the document");
    }

    private static string SweepDoc(bool big)
    {
        string Blob(int seed)
        {
            var bytes = new byte[BlobChars / 4 * 3];
            new Random(seed).NextBytes(bytes);
            return Convert.ToBase64String(bytes);
        }

        var files = big
            ? string.Join(",", Enumerable.Range(0, BlobCount).Select(i => $$"""{"name":"f{{i}}","content":"{{Blob(i)}}"}"""))
            : "";
        return $$"""{"files":[{{files}}]}""";
    }

    private static double[] SweepStepMs<TNode>(string script, string text, Func<ExecutionContext<TNode>> context)
    {
        var options = FormatRunners.Options<TNode>();
        var engine = new ScriptEngine<TNode>(options.CommandsProvider, options.FunctionsProvider);
        var adapter = context().NodeAdapter;
        var steps = engine.Parse(script, adapter);
        var best = Enumerable.Repeat(double.MaxValue, steps.Count).ToArray();

        for (var round = 0; round < 8; round++)             // the first rounds are the warm-up
        {
            var ctx = context();
            var data = adapter.Parse(text);
            for (var i = 0; i < steps.Count; i++)
            {
                var t = Stopwatch.GetTimestamp();
                data = steps[i].Execute(data, ctx).Data;
                if (round >= 3) best[i] = Math.Min(best[i], Stopwatch.GetElapsedTime(t).TotalMilliseconds);
            }
        }
        return best;
    }

    private static MultiFormatScriptRunner MultiFormatRunner(bool systemTextJson = false)
    {
        var runner = new MultiFormatScriptRunner(Converter3());
        if (systemTextJson) runner.RegisterExecutor(Section("json", SystemTextJsonExecutionContext.CreateDefault));
        else runner.RegisterExecutor(Section("json", JsonExecutionContext.CreateDefault));
        runner.RegisterExecutor(Section("xml", XmlExecutionContext.CreateWithNativeXPath));
        runner.RegisterExecutor(Section("yaml", YamlExecutionContext.CreateDefault));
        return runner;

        static ScriptEngineSectionExecutor<T> Section<T>(string id, Func<ExecutionContext<T>> context)
        {
            var o = ParseOptions<T>.CreateDefault();
            return new ScriptEngineSectionExecutor<T>(id, new ScriptEngine<T>(o.CommandsProvider, o.FunctionsProvider), () => context());
        }
    }

    // ── Numbers ───────────────────────────────────────────────────────────────

    /// <summary>
    /// Where a big document's time goes, stage by stage and by size, on this machine.
    /// <c>dotnet test TLio.Parity.Tests --filter "Name~Report" --logger "console;verbosity=detailed"</c>
    /// </summary>
    [Test, Explicit("Prints a table; asserts nothing.")]
    public void Report_WhereTheTimeGoes()
    {
        var converter = Converter3();
        var settings = new ConversionSettings();
        var o = ParseOptions<JToken>.CreateDefault();
        var engine = new ScriptEngine<JToken>(o.CommandsProvider, o.FunctionsProvider);
        var nj = new JsonNodeAdapter();
        var st = new SystemTextJsonNodeAdapter();
        const string put = """[{"command":"put","path":"$.order.status","value":"x"}]""";

        TestContext.WriteLine("ms, best of 5; size = characters per base64 file, x3 files");
        TestContext.WriteLine($"{"size",9} | {"xml->json",9} | {"xml->yaml",9} | {"NJ parse",9} | {"NJ serial",9} | {"STJ parse",9} | {"STJ serial",9} | {"1 put",8}");
        foreach (var size in new[] { 16, 50_000, 100_000, 300_000, 1_000_000 })
        {
            var xml = OrderXml(size);
            var json = converter.Convert("xml", xml, "json", settings);
            var njDoc = nj.Parse(json);
            var stDoc = st.Parse(json);
            TestContext.WriteLine(
                $"{size,9} | {BestMs(() => converter.Convert("xml", xml, "json", settings), 10),9:F2} | " +
                $"{BestMs(() => converter.Convert("xml", xml, "yaml", settings), 5),9:F2} | " +
                $"{BestMs(() => nj.Parse(json), 10),9:F2} | {BestMs(() => nj.Serialize(njDoc), 10),9:F2} | " +
                $"{BestMs(() => st.Parse(json), 10),9:F2} | {BestMs(() => st.Serialize(stDoc), 10),9:F2} | " +
                $"{BestMs(() => engine.Execute(put, njDoc, JsonExecutionContext.CreateDefault()), 1_000),8:F4}");
        }
    }
}
