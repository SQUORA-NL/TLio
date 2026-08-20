using System.Text.RegularExpressions;
using Newtonsoft.Json.Linq;
using NUnit.Framework;

namespace TLio.Parity.Tests;

/// <summary>
/// One script that touches every registered command and every registered function, run against
/// each format from an empty document.
///
/// <para>The fixture suite proves that individual behaviours agree. This proves something the
/// fixtures cannot: that the whole surface is reachable in every format. A function that is
/// never called in XML cannot be known to work there, and until this existed 53 of the 76
/// functions were exercised only against JSON.</para>
///
/// <para>Three things are asserted, and the first two are what make it a test rather than a
/// demo:</para>
/// <list type="number">
///   <item>Every command in the script is <b>applied</b> — no no-ops, no failures. A function
///     that silently returns its own argument, or a path form a format does not understand,
///     shows up here rather than as a subtly wrong value nobody reads.</item>
///   <item>The resulting document matches the one recorded for that format.</item>
///   <item>The recorded documents differ from each other <b>only</b> where a format genuinely
///     cannot agree. That set is small, closed, and named in <see cref="DocumentedDivergences"/>;
///     anything else drifting apart fails.</item>
/// </list>
/// </summary>
[TestFixture]
public class SweepTests
{
    private static readonly string[] Formats = { "JSON", "XML", "YAML" };

    /// <summary>
    /// The leaves where the formats are allowed to disagree, and why. Everything else must be
    /// identical. Both are inherent to the format, not gaps in the alignment:
    /// see docs/ai-ref/adapters/document-shape.md.
    /// </summary>
    private static readonly Dictionary<string, string> DocumentedDivergences = new()
    {
        [".check.isBoolean"] =
            "JSON carries the type in the document, so the string \"true\" is a string. XML and " +
            "YAML scalars are untyped and report the value's apparent type, so it reads as a boolean.",

        [".ref"] =
            "A path held as a value is written in the format's own path language — $.name in " +
            "JSON and YAML, /root/name in XML.",

        [".core.scriptPath"] =
            "=scriptPath() reports where it was called from, in the format's own path language.",

        [".core.path"] =
            "=path() is the same function under its other registered name.",
    };

    /// <summary>
    /// The sweep as written for a format — not a translation of another format's script.
    ///
    /// JSON and YAML share one file: they use the same path language, and JSON is a subset of
    /// YAML, so the YAML parser reads the .json script unchanged. XML has its own, written in
    /// XPath. Two files, because there are two path languages here — the third format happens
    /// to share one.
    /// </summary>
    private static string Script(string format) => File.ReadAllText(ScriptPath(format));

    private static string ScriptPath(string format) => Path.Combine(
        TestContext.CurrentContext.TestDirectory, "Sweep",
        format == "XML" ? "sweep.xml" : "sweep.json");

    private static string ExpectedPath(string format) => Path.Combine(
        TestContext.CurrentContext.TestDirectory, "Sweep", $"expected.{format.ToLowerInvariant()}.json");

    // ── The sweep itself ──────────────────────────────────────────────────────

    [TestCase("JSON")]
    [TestCase("XML")]
    [TestCase("YAML")]
    public void EveryCommandInTheSweepIsApplied(string format)
    {
        var run = SweepRunner.Run(format, Script(format));

        Assert.That(run.Success, Is.True,
            $"{format} sweep reported failure.\n  {string.Join("\n  ", run.Warnings)}");

        Assert.That(run.NotApplied, Is.Empty,
            $"{format}: some commands did not apply — a function or path form that does not work "
            + $"in this format.\n{run.Report()}");
    }

    [TestCase("JSON")]
    [TestCase("XML")]
    [TestCase("YAML")]
    public void TheSweepProducesTheRecordedDocument(string format)
    {
        var run = SweepRunner.Run(format, Script(format));
        var expected = File.ReadAllText(ExpectedPath(format)).Trim();

        Assert.That(run.Document, Is.EqualTo(expected),
            $"{format} sweep result changed. If the change is intended, regenerate "
            + $"Sweep/expected.{format.ToLowerInvariant()}.json.");
    }

    [Test]
    public void TheFormatsDifferOnlyWhereTheyMust()
    {
        var leaves = Formats.ToDictionary(f => f, f => Leaves(File.ReadAllText(ExpectedPath(f))));
        var baseline = leaves["JSON"];

        var unexplained = new List<string>();

        foreach (var format in Formats.Skip(1))
        {
            foreach (var key in baseline.Keys.Union(leaves[format].Keys))
            {
                var a = baseline.GetValueOrDefault(key, "<missing>");
                var b = leaves[format].GetValueOrDefault(key, "<missing>");
                if (a == b || DocumentedDivergences.ContainsKey(key)) continue;

                unexplained.Add($"{format} {key}: JSON={a} {format}={b}");
            }
        }

        Assert.That(unexplained, Is.Empty,
            "The formats disagree somewhere that is not a documented, inherent difference:\n  "
            + string.Join("\n  ", unexplained));
    }

    // ── Coverage: the sweep must actually reach everything ────────────────────

    [TestCase("JSON")]
    [TestCase("XML")]
    public void EveryRegisteredCommandIsExercised(string script)
    {
        var text = Script(script);
        var registered = FormatRunners.Options<JToken>().CommandsProvider.GetRegisteredCommandNames();

        var missing = registered
            .Where(name => !Regex.IsMatch(text, CommandPattern(script, name), RegexOptions.IgnoreCase))
            .OrderBy(n => n)
            .ToList();

        Assert.That(missing, Is.Empty,
            $"These commands are registered but the {script} sweep never runs them, so nothing "
            + "proves they work there: " + string.Join(", ", missing));
    }

    [TestCase("JSON")]
    [TestCase("XML")]
    public void EveryRegisteredFunctionIsExercised(string script)
    {
        var text = Script(script);
        var registered = FormatRunners.Options<JToken>().FunctionsProvider.GetRegisteredFunctionNames();

        var missing = registered
            .Where(name => !Regex.IsMatch(text, $@"[=(,\s']{Regex.Escape(name)}\s*\(",
                                          RegexOptions.IgnoreCase))
            .OrderBy(n => n)
            .ToList();

        Assert.That(missing, Is.Empty,
            $"These functions are registered but the {script} sweep never calls them, so nothing "
            + "proves they work there: " + string.Join(", ", missing));
    }

    /// <summary>A command is a "command" field in the JSON notation and an element in the XML one.</summary>
    private static string CommandPattern(string script, string name) =>
        script == "XML"
            ? $@"<{Regex.Escape(name)}[\s/>]"
            : $@"""command""\s*:\s*""{Regex.Escape(name)}""";

    // ── Helper ────────────────────────────────────────────────────────────────

    /// <summary>Every scalar in the document, keyed by its path, so a diff names what moved.</summary>
    private static Dictionary<string, string> Leaves(string json)
    {
        var result = new Dictionary<string, string>();
        Walk(JToken.Parse(json), "", result);
        return result;
    }

    private static void Walk(JToken token, string path, IDictionary<string, string> into)
    {
        switch (token)
        {
            case JObject obj:
                foreach (var property in obj.Properties())
                    Walk(property.Value, $"{path}.{property.Name}", into);
                break;
            case JArray array:
                for (var i = 0; i < array.Count; i++)
                    Walk(array[i], $"{path}[{i}]", into);
                break;
            default:
                into[path] = token.Type == JTokenType.Null ? "null" : token.ToString();
                break;
        }
    }
}
