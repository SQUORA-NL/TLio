using System.Diagnostics;
using System.Runtime;
using System.Text;
using TLio.Client;
using TLio.Core.Models;
using TLio.FormatConverter;
using TLio.FormatConverter.Json;
using TLio.FormatConverter.Xml;
using TLio.FormatConverter.Yaml;
using TLio.Json;
using TLio.Xml;
using TLio.Yaml;
using Converter = global::TLio.FormatConverter.Core.FormatConverter;

namespace TLio.Benchmarks;

/// <summary>
/// A document that carries a few very large strings — three base64 files of ~300 KB each, an XML
/// order converted to JSON, edited and written back — run on every core at once, under
/// Workstation GC and under Server GC, so the effect of the GC mode on <i>this</i> workload is a
/// measured column, not a recommendation.
///
/// Why it is not a test and not a BenchmarkDotNet class: the GC mode is fixed when a process
/// starts, so one process cannot compare the two, and the pinned BenchmarkDotNet version cannot
/// target net10.0 for a job with its own settings. So the parent launches this same program once
/// per mode (<c>DOTNET_gcServer=0|1</c> in the child's environment) and prints what each reports.
///
/// Per-command cost does not depend on the size of the strings (see
/// <c>TLio.Parity.Tests/BigObjectPerformanceTests.cs</c>); what grows with them is parse, convert
/// and serialize work, which allocates large-object-heap strings. With many threads those strings
/// provoke full collections that pause every thread — the "gen2" and "paused" columns show it.
///
/// Run: <c>dotnet run -c Release --project TLio.Benchmarks -- --big-document</c>
/// Options: <c>--seconds N</c> per measurement (default 10), <c>--threads N</c> (default: cores),
/// <c>--big-share P</c> percent of runs that get the big document (default 100; 0 = only small,
/// 10 = a mixed workload), <c>--small-input file</c> the small document of a mixed workload
/// (default: the same order with tiny strings), <c>--script</c>/<c>--input</c>/<c>--from</c> your own.
/// </summary>
internal static class BigDocumentGcComparison
{
    private const int BlobChars = 300_000;
    private const int BlobCount = 3;
    private const string ChildMarker = "--big-document-child";

    private const string Script = """
        [
          { "command": "convert", "to": "json" },
          { "command": "put", "path": "$.order.status", "value": "processed" },
          { "command": "add", "path": "$.order.audit", "value": { "by": "tlio" } },
          { "command": "copy", "fromPath": "$.order.customer.name", "toPath": "$.order.audit.customer" },
          { "command": "remove", "path": "$.order.id" },
          { "command": "convert", "to": "xml" }
        ]
        """;

    public static bool Handles(string[] args) =>
        args.Contains("--big-document") || args.Contains(ChildMarker);

    public static int Run(string[] args)
    {
        var seconds = IntOption(args, "--seconds", 10);
        var threads = IntOption(args, "--threads", Environment.ProcessorCount);
        return args.Contains(ChildMarker) ? Child(args, seconds, threads) : Parent(args, seconds, threads);
    }

    // --script <file> --input <file> [--from json|xml|yaml] swaps the built-in order for your own
    // script and document; the parent hands the same arguments on to each child.
    private static string? StringOption(string[] args, string name)
    {
        var i = Array.IndexOf(args, name);
        return i >= 0 && i + 1 < args.Length ? args[i + 1] : null;
    }

    private static int Parent(string[] args, int seconds, int threads)
    {
        Console.WriteLine($"{IntOption(args, "--big-share", 100)}% big documents ({BlobCount} x {BlobChars:N0} base64 chars), {threads} threads, " +
                          $"{seconds}s per measurement, {Environment.ProcessorCount} cores");
        Console.WriteLine();
        Console.WriteLine($"{"GC mode",-12} {"threads",8} {"runs/s",9} {"gen2",6} {"paused",8} {"alloc/run",11} {"peak mem",10}");

        foreach (var (label, server) in new[] { ("Workstation", "0"), ("Server", "1") })
        {
            foreach (var t in new[] { 1, threads }.Distinct())
            {
                var info = new ProcessStartInfo(Environment.ProcessPath!)
                {
                    RedirectStandardOutput = true,
                    UseShellExecute = false,
                };
                // dotnet <dll> when launched through the host, the apphost itself otherwise.
                if (Path.GetFileNameWithoutExtension(Environment.ProcessPath) == "dotnet")
                    info.ArgumentList.Add(typeof(BigDocumentGcComparison).Assembly.Location);
                info.ArgumentList.Add(ChildMarker);
                info.ArgumentList.Add("--seconds"); info.ArgumentList.Add(seconds.ToString());
                info.ArgumentList.Add("--threads"); info.ArgumentList.Add(t.ToString());
                foreach (var name in new[] { "--script", "--input", "--small-input", "--from", "--big-share" })
                    if (StringOption(args, name) is { } value) { info.ArgumentList.Add(name); info.ArgumentList.Add(value); }
                info.Environment["DOTNET_gcServer"] = server;
                info.Environment["DOTNET_gcConcurrent"] = "1";

                using var child = Process.Start(info)!;
                var line = child.StandardOutput.ReadToEnd().Trim();
                child.WaitForExit();
                if (child.ExitCode != 0 || !line.StartsWith("RESULT "))
                {
                    Console.WriteLine($"{label,-12} child failed (exit {child.ExitCode}): {line}");
                    return 1;
                }
                var p = line["RESULT ".Length..].Split(' ');
                Console.WriteLine($"{label,-12} {t,8} {p[0],9} {p[1],6} {p[2],8} {p[3],11} {p[4],10}");
            }
        }
        return 0;
    }

    private static int Child(string[] args, int seconds, int threads)
    {
        var scriptFile = StringOption(args, "--script");
        var inputFile = StringOption(args, "--input");
        var from = StringOption(args, "--from") ?? (inputFile is null ? "xml" : "json");
        var input = inputFile is null ? OrderXml(BlobChars) : File.ReadAllText(inputFile);
        // The small document of a mixed workload: your own file, or the same order with tiny strings.
        var smallFile = StringOption(args, "--small-input");
        var small = smallFile is not null ? File.ReadAllText(smallFile) : inputFile is null ? OrderXml(16) : input;
        var bigShare = IntOption(args, "--big-share", 100);
        var runner = BuildRunner();
        var compiled = runner.Compile(from, scriptFile is null ? Script : File.ReadAllText(scriptFile));
        for (var i = 0; i < 20; i++) { compiled.Run(input); compiled.Run(small); } // JIT and tiering settle before anything is timed

        GC.Collect();
        var gen2 = GC.CollectionCount(2);
        var paused = GC.GetTotalPauseDuration();
        var allocated = GC.GetTotalAllocatedBytes(precise: true);
        long runs = 0;
        var clock = Stopwatch.StartNew();
        var until = TimeSpan.FromSeconds(seconds);

        // Process.PeakWorkingSet64 reads 0 on some platforms (macOS), so memory is sampled: the
        // largest working set and the largest amount the GC had committed, whichever is bigger.
        long peakBytes = 0;
        var sampling = true;
        var sampler = new Thread(() =>
        {
            using var self = Process.GetCurrentProcess();
            while (Volatile.Read(ref sampling))
            {
                self.Refresh();
                var now = Math.Max(self.WorkingSet64, GC.GetGCMemoryInfo().TotalCommittedBytes);
                if (now > peakBytes) peakBytes = now;
                Thread.Sleep(50);
            }
        }) { IsBackground = true };
        sampler.Start();

        var workers = Enumerable.Range(0, threads).Select(index => new Thread(() =>
        {
            long mine = 0;
            while (clock.Elapsed < until)
            {
                // A fixed, evenly spread pattern: bigShare of every 100 runs get the big document.
                compiled.Run((mine * 37 + index * 11) % 100 < bigShare ? input : small);
                mine++;
            }
            Interlocked.Add(ref runs, mine);
        })).ToArray();
        foreach (var w in workers) w.Start();
        foreach (var w in workers) w.Join();
        clock.Stop();
        Volatile.Write(ref sampling, false);
        sampler.Join();

        var wall = clock.Elapsed;
        var pausedShare = (GC.GetTotalPauseDuration() - paused).TotalMilliseconds / wall.TotalMilliseconds;
        var perRun = (GC.GetTotalAllocatedBytes(precise: true) - allocated) / Math.Max(1, runs) / 1_048_576.0;
        Console.WriteLine($"RESULT {runs / wall.TotalSeconds:F0} {GC.CollectionCount(2) - gen2} {pausedShare:P0} {perRun:F1}MB {peakBytes / 1_048_576}MB");

        // The numbers are only worth printing if the child really ran in the mode it was asked for.
        return GCSettings.IsServerGC == (Environment.GetEnvironmentVariable("DOTNET_gcServer") == "1") ? 0 : 2;
    }

    private static MultiFormatScriptRunner BuildRunner()
    {
        var converter = new Converter();
        converter.Register(new JsonFormatAdapter());
        converter.Register(new XmlFormatAdapter());
        converter.Register(new YamlFormatAdapter());
        var runner = new MultiFormatScriptRunner(converter);
        runner.RegisterExecutor(Section("json", JsonExecutionContext.CreateDefault));
        runner.RegisterExecutor(Section("xml", XmlExecutionContext.CreateWithNativeXPath));
        runner.RegisterExecutor(Section("yaml", YamlExecutionContext.CreateDefault));
        return runner;
    }

    private static string OrderXml(int blobChars)
    {
        var rnd = new Random(1);
        var sb = new StringBuilder("<order><id>42</id><customer><name>Ann</name></customer><files>");
        for (var i = 0; i < BlobCount; i++)
        {
            var bytes = new byte[blobChars / 4 * 3];
            rnd.NextBytes(bytes);
            sb.Append($"<file><name>f{i}</name><content>{Convert.ToBase64String(bytes)}</content></file>");
        }
        return sb.Append("</files></order>").ToString();
    }

    private static ScriptEngineSectionExecutor<T> Section<T>(string id, Func<ExecutionContext<T>> context)
    {
        var o = ParseOptions<T>.CreateDefault();
        return new ScriptEngineSectionExecutor<T>(id, new ScriptEngine<T>(o.CommandsProvider, o.FunctionsProvider), () => context());
    }

    private static int IntOption(string[] args, string name, int fallback)
    {
        var i = Array.IndexOf(args, name);
        return i >= 0 && i + 1 < args.Length && int.TryParse(args[i + 1], out var v) ? v : fallback;
    }
}
