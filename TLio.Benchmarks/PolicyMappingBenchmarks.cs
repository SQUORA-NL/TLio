using BenchmarkDotNet.Attributes;
using TLio.Client;
using TLio.Core.Models;
using TLio.Extensions.Looping;
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
/// The JSON → SOAP-XML mapping that motivated <c>with</c> and <c>resolve()</c>, in its two
/// spellings: the original, which repeats the nested policyholder / insured-person lookup once
/// per field (28 times), and the rewritten one that states it once per person with <c>with</c>.
/// <c>TLio.Parity.Tests/WithResolveEquivalenceTests</c> proves the two produce byte-identical XML,
/// so any difference measured here is the cost of writing it that way and nothing else.
///
/// Both are compiled once and run per iteration against the same input, single-threaded — the
/// claim under test is "the nested filter is evaluated 2 times instead of 28", not parallel
/// scaling (see <see cref="PortfolioThroughputBenchmarks"/> for that).
///
/// Run: <c>dotnet run -c Release --project TLio.Benchmarks -- --filter '*PolicyMapping*'</c>
/// </summary>
[MemoryDiagnoser]
[InProcess] // the pinned BenchmarkDotNet cannot generate a child project for net10.0
public class PolicyMappingBenchmarks
{
    private string _input = "";
    private CompiledMultiFormatScript _original = null!;
    private CompiledMultiFormatScript _rewritten = null!;

    [GlobalSetup]
    public void Setup()
    {
        string Read(string file) => File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "Equivalence", "policy-mapping", file));

        var converter = new Converter();
        converter.Register(new JsonFormatAdapter());
        converter.Register(new XmlFormatAdapter());
        converter.Register(new YamlFormatAdapter());

        var runner = new MultiFormatScriptRunner(converter);
        runner.RegisterExecutor(Section("json", JsonExecutionContext.CreateDefault));
        runner.RegisterExecutor(Section("xml", XmlExecutionContext.CreateWithNativeXPath));
        runner.RegisterExecutor(Section("yaml", YamlExecutionContext.CreateDefault));

        _input = Read("input.json");
        _original = runner.Compile("json", Read("script.original.json"));
        _rewritten = runner.Compile("json", Read("script.rewritten.json"));

        if (_original.Run(_input).Document != _rewritten.Run(_input).Document)
            throw new InvalidOperationException("the two forms no longer produce the same document");

        static ScriptEngineSectionExecutor<T> Section<T>(string id, Func<ExecutionContext<T>> context)
        {
            var o = ParseOptions<T>.CreateDefault();
            o.CommandsProvider.RegisterLooping<T>();
            return new ScriptEngineSectionExecutor<T>(id, new ScriptEngine<T>(o.CommandsProvider, o.FunctionsProvider), () => context());
        }
    }

    [Benchmark(Baseline = true, Description = "original: nested filter repeated per field")]
    public string Original() => _original.Run(_input).Document;

    [Benchmark(Description = "rewritten: with + resolve")]
    public string Rewritten() => _rewritten.Run(_input).Document;
}
