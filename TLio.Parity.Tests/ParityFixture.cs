using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using NUnit.Framework;

namespace TLio.Parity.Tests;

/// <summary>
/// One parity case, written once in JSON and run against all three formats.
///
/// A fixture file is <c>{ "input": …, "script": [ … ], "result": … }</c>, optionally with:
/// <list type="bullet">
///   <item><c>"formats"</c> — the formats this case applies to, default all three. Use it for
///     notation that only one format has (JSON's <c>$$</c> escapes, XPath predicates).</item>
///   <item><c>"resultXml"</c> / <c>"resultYaml"</c> — a per-format expected result, for the
///     cases where the output legitimately differs because it contains paths written in the
///     format's own path language (the <c>compare</c> report). Everything else must match the
///     canonical result, translated.</item>
///   <item><c>"why"</c> — free text explaining a per-format expectation, so a deliberate
///     divergence never reads as an oversight.</item>
/// </list>
/// </summary>
public sealed class ParityFixture
{
    public required string Name { get; init; }
    public required JToken Input { get; init; }
    public required string Script { get; init; }
    public required JToken Result { get; init; }
    public JToken? ResultXml { get; init; }
    public JToken? ResultYaml { get; init; }
    public required IReadOnlyList<string> Formats { get; init; }

    public bool Covers(string format) =>
        Formats.Contains(format, StringComparer.OrdinalIgnoreCase);

    /// <summary>The expected result for one format — the override when there is one.</summary>
    public JToken Expected(string format) => format.ToLowerInvariant() switch
    {
        "xml"  => ResultXml  ?? Result,
        "yaml" => ResultYaml ?? Result,
        _      => Result
    };

    public override string ToString() => Name;
}

public static class ParityFixtureLoader
{
    private static readonly string[] AllFormats = { "json", "xml", "yaml" };

    /// <summary>Every fixture in a group, in folder order.</summary>
    public static IEnumerable<ParityFixture> Group(string group)
    {
        var root = Path.Combine(TestContext.CurrentContext.TestDirectory, "Fixtures", group);
        if (!Directory.Exists(root)) yield break;

        foreach (var dir in Directory.EnumerateDirectories(root).OrderBy(d => d, StringComparer.Ordinal))
        {
            var path = Path.Combine(dir, "fixture.json");
            if (!File.Exists(path)) continue;
            yield return Read(path, $"{group}/{Path.GetFileName(dir)}");
        }
    }

    /// <summary>Test cases for one group and one format, skipping the ones it does not cover.</summary>
    public static IEnumerable<TestCaseData> Cases(string group, string format) =>
        Group(group)
            .Where(f => f.Covers(format))
            .Select(f => new TestCaseData(f).SetName(f.Name.Replace('/', '_')));

    private static ParityFixture Read(string path, string name)
    {
        // DateParseHandling.None keeps date-shaped strings as strings; without it Newtonsoft
        // rewrites them to a normalised form and the fixture stops describing its own input.
        JObject doc;
        using (var reader = new JsonTextReader(new StringReader(File.ReadAllText(path)))
               { DateParseHandling = DateParseHandling.None })
            doc = JObject.Load(reader);

        JToken Required(string key) =>
            doc[key]?.DeepClone() ?? throw new InvalidOperationException($"Missing '{key}' in {path}");

        return new ParityFixture
        {
            Name       = name,
            Input      = Required("input"),
            Script     = Required("script").ToString(Formatting.None),
            Result     = Required("result"),
            ResultXml  = doc["resultXml"]?.DeepClone(),
            ResultYaml = doc["resultYaml"]?.DeepClone(),
            Formats    = doc["formats"]?.Select(f => f.ToString()).ToArray() ?? AllFormats
        };
    }
}
