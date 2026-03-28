using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using NUnit.Framework;

namespace TLio.UnitTests.Fixtures;

public static class FixtureTheoryLoader
{
    public static IEnumerable<TestCaseData> Load(string commandName)
    {
        var fixturesRoot = Path.Combine(
            TestContext.CurrentContext.TestDirectory,
            "Fixtures",
            commandName);

        if (!Directory.Exists(fixturesRoot))
            yield break;

        foreach (var dir in Directory.EnumerateDirectories(fixturesRoot).OrderBy(d => d))
        {
            var fixturePath = Path.Combine(dir, "fixture.json");
            if (!File.Exists(fixturePath))
                continue;

            var fixture = JObject.Parse(File.ReadAllText(fixturePath));
            // Re-parse input and result so they are detached root tokens (Path == "$")
            // rather than children of the fixture object (Path == "$.input" etc.)
            JToken input    = JToken.Parse(fixture["input"]!.ToString(Formatting.None));
            string script   = fixture["script"]!.ToString(Formatting.None);
            JToken expected = JToken.Parse(fixture["result"]!.ToString(Formatting.None));

            yield return new TestCaseData(input, script, expected)
                .SetName(Path.GetFileName(dir));
        }
    }

    /// <summary>
    /// Load single-file fixtures from Fixtures/&lt;subPath&gt;/.
    /// Each .json file must be: { "input": {...}, "script": [...], "result": {...} }
    /// Returns (JToken input, string script, JToken expected) for Newtonsoft tests.
    /// Dates are parsed with DateParseHandling.None so date strings stay as strings.
    /// </summary>
    public static IEnumerable<TestCaseData> LoadSingle(string subPath)
    {
        var fixturesRoot = Path.Combine(
            TestContext.CurrentContext.TestDirectory,
            "Fixtures",
            subPath.Replace('/', Path.DirectorySeparatorChar));

        if (!Directory.Exists(fixturesRoot))
            yield break;

        foreach (var file in Directory.EnumerateFiles(fixturesRoot, "*.json").OrderBy(f => f))
        {
            JObject doc;
            using (var reader = new JsonTextReader(new StringReader(File.ReadAllText(file)))
                       { DateParseHandling = DateParseHandling.None })
            {
                doc = JObject.Load(reader);
            }

            // DeepClone to detach from the parent document — Newtonsoft.Json's SelectTokens
            // navigates from the true document root when a token has a parent, so an uncloned
            // "input" child would cause all $-paths to resolve against the fixture wrapper object.
            var input    = (doc["input"]  ?? throw new InvalidOperationException($"Missing 'input' in {file}")).DeepClone();
            var scriptTk =  doc["script"] ?? throw new InvalidOperationException($"Missing 'script' in {file}");
            var expected = (doc["result"] ?? throw new InvalidOperationException($"Missing 'result' in {file}")).DeepClone();

            string script = scriptTk.ToString(Formatting.None);

            yield return new TestCaseData(input, script, expected)
                .SetName(Path.GetFileNameWithoutExtension(file));
        }
    }

    public static IEnumerable<TestCaseData> LoadRaw(string commandName)
    {
        var fixturesRoot = Path.Combine(
            TestContext.CurrentContext.TestDirectory,
            "Fixtures",
            commandName);

        if (!Directory.Exists(fixturesRoot))
            yield break;

        foreach (var dir in Directory.EnumerateDirectories(fixturesRoot).OrderBy(d => d))
        {
            var fixturePath = Path.Combine(dir, "fixture.json");
            if (!File.Exists(fixturePath))
                continue;

            var fixture = JObject.Parse(File.ReadAllText(fixturePath));
            string inputJson    = fixture["input"]!.ToString(Formatting.None);
            string script       = fixture["script"]!.ToString(Formatting.None);
            string expectedJson = fixture["result"]!.ToString(Formatting.None);

            yield return new TestCaseData(inputJson, script, expectedJson)
                .SetName(Path.GetFileName(dir));
        }
    }
}
