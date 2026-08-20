using NUnit.Framework;

namespace TLio.Parity.Tests;

/// <summary>
/// The cross-format regression net: every fixture is written once and asserted against JSON,
/// XML and YAML, each driven through its own script notation.
///
/// <para>The JSON row is the reference — it is the format whose behaviour the commands were
/// written for. The XML and YAML rows assert that the same script, spelled in that format's
/// notation and run over the same document in that format's shape, produces the same document
/// back. A format that drifts fails here rather than in a suite that only ever compared it
/// against itself.</para>
///
/// <para>Where a format genuinely cannot produce the canonical answer — XML writes <c>null</c>,
/// <c>""</c>, <c>{}</c> and <c>[]</c> as the same empty element — the comparison absorbs it, and
/// where the answer legitimately differs (a <c>compare</c> report quotes paths in the format's
/// own path language) the fixture carries an explicit per-format expectation.
/// Nothing else is tolerated.</para>
/// </summary>
[TestFixture]
public class ParityTests
{
    // ── JSON — the reference behaviour ────────────────────────────────────────

    [TestCaseSource(nameof(JsonCases), new object[] { "Set" })]
    [TestCaseSource(nameof(JsonCases), new object[] { "Add" })]
    [TestCaseSource(nameof(JsonCases), new object[] { "Put" })]
    [TestCaseSource(nameof(JsonCases), new object[] { "Remove" })]
    [TestCaseSource(nameof(JsonCases), new object[] { "Copy" })]
    [TestCaseSource(nameof(JsonCases), new object[] { "Move" })]
    [TestCaseSource(nameof(JsonCases), new object[] { "Rename" })]
    [TestCaseSource(nameof(JsonCases), new object[] { "Merge" })]
    [TestCaseSource(nameof(JsonCases), new object[] { "Compare" })]
    [TestCaseSource(nameof(JsonCases), new object[] { "IfElse" })]
    [TestCaseSource(nameof(JsonCases), new object[] { "Arrays" })]
    [TestCaseSource(nameof(JsonCases), new object[] { "Structure" })]
    [TestCaseSource(nameof(JsonCases), new object[] { "Functions" })]
    public void Json(ParityFixture fixture) => Run(fixture, FormatRunners.RunJson, "JSON");

    // ── XML ───────────────────────────────────────────────────────────────────

    [TestCaseSource(nameof(XmlCases), new object[] { "Set" })]
    [TestCaseSource(nameof(XmlCases), new object[] { "Add" })]
    [TestCaseSource(nameof(XmlCases), new object[] { "Put" })]
    [TestCaseSource(nameof(XmlCases), new object[] { "Remove" })]
    [TestCaseSource(nameof(XmlCases), new object[] { "Copy" })]
    [TestCaseSource(nameof(XmlCases), new object[] { "Move" })]
    [TestCaseSource(nameof(XmlCases), new object[] { "Rename" })]
    [TestCaseSource(nameof(XmlCases), new object[] { "Merge" })]
    [TestCaseSource(nameof(XmlCases), new object[] { "Compare" })]
    [TestCaseSource(nameof(XmlCases), new object[] { "IfElse" })]
    [TestCaseSource(nameof(XmlCases), new object[] { "Arrays" })]
    [TestCaseSource(nameof(XmlCases), new object[] { "Structure" })]
    [TestCaseSource(nameof(XmlCases), new object[] { "Functions" })]
    public void Xml(ParityFixture fixture) => Run(fixture, FormatRunners.RunXml, "XML");

    // ── YAML ──────────────────────────────────────────────────────────────────

    [TestCaseSource(nameof(YamlCases), new object[] { "Set" })]
    [TestCaseSource(nameof(YamlCases), new object[] { "Add" })]
    [TestCaseSource(nameof(YamlCases), new object[] { "Put" })]
    [TestCaseSource(nameof(YamlCases), new object[] { "Remove" })]
    [TestCaseSource(nameof(YamlCases), new object[] { "Copy" })]
    [TestCaseSource(nameof(YamlCases), new object[] { "Move" })]
    [TestCaseSource(nameof(YamlCases), new object[] { "Rename" })]
    [TestCaseSource(nameof(YamlCases), new object[] { "Merge" })]
    [TestCaseSource(nameof(YamlCases), new object[] { "Compare" })]
    [TestCaseSource(nameof(YamlCases), new object[] { "IfElse" })]
    [TestCaseSource(nameof(YamlCases), new object[] { "Arrays" })]
    [TestCaseSource(nameof(YamlCases), new object[] { "Structure" })]
    [TestCaseSource(nameof(YamlCases), new object[] { "Functions" })]
    public void Yaml(ParityFixture fixture) => Run(fixture, FormatRunners.RunYaml, "YAML");

    // ── Sources ───────────────────────────────────────────────────────────────

    public static IEnumerable<TestCaseData> JsonCases(string group) => ParityFixtureLoader.Cases(group, "json");
    public static IEnumerable<TestCaseData> XmlCases(string group)  => ParityFixtureLoader.Cases(group, "xml");
    public static IEnumerable<TestCaseData> YamlCases(string group) => ParityFixtureLoader.Cases(group, "yaml");

    private static void Run(ParityFixture fixture, Func<ParityFixture, RunOutcome> run, string format)
    {
        var outcome = run(fixture);

        Assert.That(outcome.Success, Is.True,
            $"{format} engine reported failure for {fixture.Name}.\n  {string.Join("\n  ", outcome.Log)}");

        Assert.That(outcome.Matches, Is.True, outcome.Report(format));
    }
}
