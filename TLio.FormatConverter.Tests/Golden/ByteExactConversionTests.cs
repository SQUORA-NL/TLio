using System.Security.Cryptography;
using System.Text;
using System.Text.Encodings.Web;
using System.Text.Json;
using NUnit.Framework;
using TLio.Client;
using TLio.FormatConverter.Core;
using TLio.FormatConverter.Json;
using TLio.FormatConverter.Xml;
using TLio.FormatConverter.Yaml;
using TLio.Json;
using TLio.Xml;
using TLio.Yaml;
using Converter = global::TLio.FormatConverter.Core.FormatConverter;

namespace TLio.FormatConverter.Tests.Golden;

/// <summary>
/// Converted output, byte for byte, against a recording of what the converter produced before.
/// </summary>
/// <remarks>
/// <para>
/// The other tests in this project ask whether converted output <i>means</i> the right thing —
/// they parse it, or compare structures. This one asks whether it is the <i>same text</i>. That
/// is the guard needed when the way output is produced changes (a streaming writer instead of a
/// DOM, one fewer copy of a large string) and the output is not allowed to: element order,
/// escaping, attribute order, namespace declarations, self-closing tags and CDATA all show up as
/// a byte that moved.
/// </para>
/// <para>
/// The corpus is every <c>input.*</c> fixture in this project, a set of hand-written hard cases,
/// and documents carrying 300 KB base64 strings — each converted into every other format under
/// each settings variant, plus a few whole-script runs through <see cref="MultiFormatScriptRunner"/>.
/// A combination that throws is recorded as the exception type and message: failing the same way
/// is part of behaving the same.
/// </para>
/// <para>
/// To change a recording on purpose, run <c>dotnet test TLio.FormatConverter.Tests -f net10.0
/// --filter "Name~Record"</c> and read the diff of <c>Golden/conversions.golden.json</c> before
/// committing it. Small outputs are recorded in full so the diff shows what moved; large ones by
/// length and SHA-256.
/// </para>
/// </remarks>
[TestFixture]
public sealed class ByteExactConversionTests
{
    private const int FullTextLimit = 4_000;
    private const string GoldenFile = "conversions.golden.json";

    // ── Corpus ────────────────────────────────────────────────────────────────

    private sealed record Input(string Name, string Format, string Text);

    private static readonly (string Name, ConversionSettings Settings)[] SettingsVariants =
    [
        ("default", new ConversionSettings()),
        ("repeated", new ConversionSettings { ArrayHandling = ArrayHandling.Repeated }),
        ("infer", new ConversionSettings { InferTypes = true }),
        ("names", new ConversionSettings { AttributePrefix = "_", TextProperty = "$t", NamespacePrefix = "ns:" }),
        ("xsi-nil", new ConversionSettings { NullRepresentation = NullRepresentation.XsiNil }),
        ("cdata-text", new ConversionSettings { CdataAsText = true }),
        ("item-row", new ConversionSettings { ArrayItemName = "row" }),
        ("sanitize-error", new ConversionSettings { NameSanitization = NameSanitization.Error }),
        ("sanitize-escape", new ConversionSettings { NameSanitization = NameSanitization.Escape }),
        ("no-flatten", new ConversionSettings { FlattenAnchors = false }),
    ];

    private static readonly string[] Formats = ["json", "xml", "yaml"];

    private static string FixturesDir => Path.Combine(TestContext.CurrentContext.TestDirectory, "Fixtures");

    private static IEnumerable<Input> FixtureInputs()
    {
        foreach (var path in Directory.EnumerateFiles(FixturesDir, "input.*", SearchOption.AllDirectories).Order())
        {
            var format = Path.GetExtension(path).TrimStart('.').ToLowerInvariant();
            if (!Formats.Contains(format)) continue;
            var name = "fixture/" + Path.GetRelativePath(FixturesDir, Path.GetDirectoryName(path)!).Replace('\\', '/');
            yield return new Input(name, format, File.ReadAllText(path));
        }
    }

    private static IEnumerable<Input> HardCases()
    {
        yield return new Input("hard/json-escapes", "json",
            """{"text":"a \"quote\", a \\ backslash, </tag> & ampersand, tab\t, newline\n, unicode é 日本 😀, plus + slash /","empty":"","sp":"  padded  "}""");
        yield return new Input("hard/json-numbers-and-literals", "json",
            """{"i":1,"neg":-5,"f":1.50,"e":1e3,"big":12345678901234567890,"t":true,"f2":false,"n":null,"s":"007"}""");
        yield return new Input("hard/json-empty-containers", "json",
            """{"o":{},"a":[],"nested":{"o":{},"a":[[],[{}]]}}""");
        yield return new Input("hard/json-nested-arrays", "json",
            """{"m":[[1,2],[3,[4,5]],[]],"objs":[{"a":1},{"a":2,"b":[{"c":3}]}]}""");
        yield return new Input("hard/json-names-needing-sanitizing", "json",
            """{"first name":"a","1st":"b","a:b":"c","x-y.z":"d","":"e","@attr":"f","#text":"g"}""");
        yield return new Input("hard/json-attributes-and-text", "json",
            """{"price":{"@currency":"EUR","#text":"9.99"},"item":{"@id":"1","@lang":"nl","name":"x"}}""");
        yield return new Input("hard/json-array-root", "json", """[1,"two",{"three":3},[4]]""");
        yield return new Input("hard/json-scalar-root", "json", "\"just a string\"");
        yield return new Input("hard/json-soap-envelope", "json", """
            {"soapenv:Envelope":{"xmlns:soapenv":"http://schemas.xmlsoap.org/soap/envelope/","xmlns:xsi":"http://www.w3.org/2001/XMLSchema-instance",
             "soapenv:Body":{"response":{"policyRequest":{"Policy":{"Product":"x","Date":"2024-01-01"},"FileList":{"File":[{"FileId":"a","Data":"QUJD","CompressionType":null},{"FileId":"b","Data":"REVG","CompressionType":null}]}}}}}}
            """);
        yield return new Input("hard/xml-attributes-text-cdata", "xml",
            "<r a=\"1\" b=\"x &amp; y\"><p c=\"2\">text</p><q><![CDATA[<raw> & stuff]]></q><e/><n xsi:nil=\"true\" xmlns:xsi=\"http://www.w3.org/2001/XMLSchema-instance\"/></r>");
        yield return new Input("hard/xml-namespaces", "xml",
            "<a:root xmlns:a=\"urn:a\" xmlns=\"urn:default\"><a:x a:attr=\"1\">v</a:x><y>w</y></a:root>");
        yield return new Input("hard/xml-mixed-and-whitespace", "xml",
            "<p>Hello <b>world</b> and <i>more</i> end</p>");
        yield return new Input("hard/xml-repeated-and-wrapped-arrays", "xml",
            "<r><items><item>1</item><item>2</item></items><v>a</v><v>b</v><one><item>x</item></one></r>");
        yield return new Input("hard/xml-entities-and-unicode", "xml",
            "<r><t>a &lt; b &gt; c &amp; d &quot;e&quot; &apos;f&apos; é 日本 😀</t><u>   </u></r>");
        yield return new Input("hard/xml-infer-candidates", "xml",
            "<r><a>1</a><b>1.5</b><c>true</c><d>null</d><e>007</e><f>1e3</f><g> 5 </g></r>");
        yield return new Input("hard/yaml-anchors-and-types", "yaml",
            "base: &b\n  x: 1\n  y: [1, 2]\nuse:\n  <<: *b\n  z: \"007\"\nflag: yes\nnothing: ~\ndate: 2024-01-01\n");
        yield return new Input("hard/yaml-multiline-strings", "yaml",
            "a: |\n  line one\n  line two\nb: >\n  folded\n  text\nc: 'single ''quoted'''\n");
    }

    /// <summary>Documents like the ones that prompted this: a few base64 strings of 250-350 KB, built deterministically.</summary>
    private static IEnumerable<Input> BigCases()
    {
        string Blob(int seed, int chars)
        {
            var bytes = new byte[chars / 4 * 3];
            new Random(seed).NextBytes(bytes);
            return Convert.ToBase64String(bytes);
        }

        var files = new[] { (247_000, 1), (66_000, 2), (33_000, 3) };

        yield return new Input("big/json-documents", "json",
            "{\"hasDocuments\":true,\"Documents\":[" +
            string.Join(",", files.Select(f => $$"""{"fileName":"f{{f.Item2}}.pdf","contentEncoding":"base64","content":"{{Blob(f.Item2, f.Item1)}}","refKey":"k{{f.Item2}}"}""")) +
            "],\"after\":{\"n\":1}}");

        yield return new Input("big/xml-documents", "xml",
            "<order><id>42</id><Documents>" +
            string.Concat(files.Select(f => $"<Document><fileName>f{f.Item2}.pdf</fileName><content>{Blob(f.Item2, f.Item1)}</content></Document>")) +
            "</Documents><after><n>1</n></after></order>");

        yield return new Input("big/yaml-documents", "yaml",
            "hasDocuments: true\nDocuments:\n" +
            string.Concat(files.Select(f => $"  - fileName: f{f.Item2}.pdf\n    content: {Blob(f.Item2, f.Item1)}\n")) +
            "after:\n  n: 1\n");

        // One huge value that is not a single line, and a value full of characters that need escaping.
        yield return new Input("big/json-escape-heavy", "json",
            "{\"text\":\"" + string.Concat(Enumerable.Repeat("<a href=\\\"x\\\">é & + / \\n</a>", 20_000)) + "\"}");
    }

    private static IEnumerable<Input> AllInputs() => FixtureInputs().Concat(HardCases()).Concat(BigCases());

    private static Converter NewConverter()
    {
        var converter = new Converter();
        converter.Register(new JsonFormatAdapter());
        converter.Register(new XmlFormatAdapter());
        converter.Register(new YamlFormatAdapter());
        return converter;
    }

    // ── Running a case ────────────────────────────────────────────────────────

    private static string Outcome(Func<string> run)
    {
        try { return run(); }
        catch (Exception ex) { return $"THREW {ex.GetType().Name}: {ex.Message}"; }
    }

    private static IEnumerable<(string Key, Func<string> Run)> Cases()
    {
        var converter = NewConverter();
        foreach (var input in AllInputs())
        foreach (var target in Formats.Where(f => f != input.Format))
        foreach (var (settingsName, settings) in SettingsVariants)
            yield return ($"{input.Name} | {input.Format}->{target} | {settingsName}",
                () => Outcome(() => converter.Convert(input.Format, input.Text, target, settings)));

        // Whole scripts: the engine section, the serialisation between sections, and the convert.
        foreach (var input in AllInputs().Where(i => i.Name.StartsWith("big/") && i.Format == "json" || i.Name == "hard/json-soap-envelope"))
        {
            yield return ($"{input.Name} | script:put-then-convert-xml", () => Outcome(() =>
                PipelineRunner().Run("json", input.Text,
                    """[{"command":"put","path":"$.added.flag","value":true},{"command":"convert","to":"xml","settings":{"arrayHandling":"repeated"}}]""").Document));
            yield return ($"{input.Name} | script:convert-yaml-then-put", () => Outcome(() =>
                PipelineRunner().Run("json", input.Text,
                    """[{"command":"convert","to":"yaml"},{"command":"put","path":"$.added.flag","value":true}]""").Document));
        }
        foreach (var input in AllInputs().Where(i => i.Name.StartsWith("big/") && i.Format == "xml"))
        {
            yield return ($"{input.Name} | script:convert-json-then-put", () => Outcome(() =>
                PipelineRunner().Run("xml", input.Text,
                    """[{"command":"convert","to":"json"},{"command":"put","path":"$.added.flag","value":true},{"command":"remove","path":"$.order.id"}]""").Document));
        }
    }

    private static MultiFormatScriptRunner PipelineRunner()
    {
        var runner = new MultiFormatScriptRunner(NewConverter());
        runner.RegisterExecutor(Section("json", JsonExecutionContext.CreateDefault));
        runner.RegisterExecutor(Section("xml", XmlExecutionContext.CreateWithNativeXPath));
        runner.RegisterExecutor(Section("yaml", YamlExecutionContext.CreateDefault));
        return runner;

        static ScriptEngineSectionExecutor<T> Section<T>(string id, Func<global::TLio.Core.Models.ExecutionContext<T>> context)
        {
            var o = ParseOptions<T>.CreateDefault();
            return new ScriptEngineSectionExecutor<T>(id, new ScriptEngine<T>(o.CommandsProvider, o.FunctionsProvider), () => context());
        }
    }

    // ── The recording ─────────────────────────────────────────────────────────

    private sealed record Recorded(int Length, string Sha256, string? Text);

    // XmlWriter writes a line break inside text as Environment.NewLine (\r\n on Windows) — that is
    // the platform's own choice, not the converter's, and it was identical before this test
    // existed. Compared as \n so one recording holds on every OS; every other byte is exact.
    private static Recorded Record(string output)
    {
        output = output.Replace("\r\n", "\n");
        return new(
            output.Length,
            Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(output))).ToLowerInvariant(),
            output.Length <= FullTextLimit ? output : null);
    }

    private static readonly JsonSerializerOptions Pretty = new()
    {
        WriteIndented = true,
        Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
    };

    private static Dictionary<string, Recorded> LoadGolden()
    {
        var path = Path.Combine(TestContext.CurrentContext.TestDirectory, "Golden", GoldenFile);
        Assert.That(File.Exists(path), Is.True, $"{GoldenFile} is missing — record it with the Record test");
        return JsonSerializer.Deserialize<Dictionary<string, Recorded>>(File.ReadAllText(path), Pretty)!;
    }

    // Built once per run: the corpus is a few hundred conversions of mostly small documents.
    private static readonly Lazy<Dictionary<string, Recorded>> Golden = new(LoadGolden);

    public static IEnumerable<TestCaseData> CaseData() =>
        Cases().Select(c => new TestCaseData(c.Key, c.Run).SetArgDisplayNames(c.Key));

    [TestCaseSource(nameof(CaseData))]
    public void Output_IsByteForByteWhatWasRecorded(string key, Func<string> run)
    {
        Assert.That(Golden.Value.TryGetValue(key, out var expected), Is.True,
            $"no recording for '{key}' — a case was added; run the Record test");

        var actual = Record(run());

        if (expected!.Text is not null && actual.Text is not null)
            Assert.That(actual.Text, Is.EqualTo(expected.Text), $"output of '{key}' changed");

        Assert.That(actual.Length, Is.EqualTo(expected.Length), $"length of '{key}' changed");
        Assert.That(actual.Sha256, Is.EqualTo(expected.Sha256), $"bytes of '{key}' changed");
    }

    [Test]
    public void TheRecording_HasNoCasesThatNoLongerExist()
    {
        var keys = Cases().Select(c => c.Key).ToHashSet();
        Assert.That(Golden.Value.Keys.Where(k => !keys.Contains(k)), Is.Empty,
            "recorded cases that are no longer in the corpus — run the Record test");
    }

    [Test]
    public void TheCorpus_ActuallyConverts()
    {
        // A recording of nothing but exceptions would pass forever. Most cases must succeed.
        var outcomes = Cases().Select(c => c.Run()).ToList();
        var threw = outcomes.Count(o => o.StartsWith("THREW "));
        TestContext.WriteLine($"{outcomes.Count} cases, {threw} record an exception");
        Assert.That(threw, Is.LessThan(outcomes.Count / 4), "too many cases throw to be a useful guard");
        Assert.That(outcomes.Count, Is.GreaterThan(300));
    }

    /// <summary>Rewrites <c>Golden/conversions.golden.json</c> in the source tree. Read the diff before committing.</summary>
    [Test, Explicit("Rewrites the recording. Only after an intended change to converter output.")]
    public void Record()
    {
        var recorded = Cases().OrderBy(c => c.Key, StringComparer.Ordinal)
            .ToDictionary(c => c.Key, c => Record(c.Run()));

        var sourceDir = Path.GetFullPath(Path.Combine(TestContext.CurrentContext.TestDirectory, "..", "..", "..", "Golden"));
        var path = Path.Combine(sourceDir, GoldenFile);
        File.WriteAllText(path, JsonSerializer.Serialize(recorded, Pretty) + "\n");
        TestContext.WriteLine($"RECORDED {recorded.Count} cases to {path}");
    }
}
