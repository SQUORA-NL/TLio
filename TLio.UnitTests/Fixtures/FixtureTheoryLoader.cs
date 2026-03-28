using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using NUnit.Framework;

namespace TLio.UnitTests.Fixtures;

/// <summary>
/// Loads (input.json, script.json, result.json) fixture triplets from named
/// sub-folders under the Fixtures directory and returns them as NUnit TestCaseData.
///
/// Usage:
/// <code>
///   [TestCaseSource(typeof(FixtureTheoryLoader), nameof(FixtureTheoryLoader.Load), new object[] { "Set" })]
///   public void RunFixture(JToken input, string script, JToken expected) { ... }
/// </code>
///
/// Each folder under Fixtures/<commandName>/ is one test case.
/// The folder name becomes the test case display name.
/// </summary>
public static class FixtureTheoryLoader
{
    /// <summary>
    /// Load all fixture triplets for the given command/function name.
    /// Returns (JToken input, string script, JToken expected) for Newtonsoft tests.
    /// </summary>
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
            var inputPath  = Path.Combine(dir, "input.json");
            var scriptPath = Path.Combine(dir, "script.json");
            var resultPath = Path.Combine(dir, "result.json");

            if (!File.Exists(inputPath) || !File.Exists(scriptPath) || !File.Exists(resultPath))
                continue;

            JToken input    = JToken.Parse(File.ReadAllText(inputPath));
            string script   = File.ReadAllText(scriptPath);
            JToken expected = JToken.Parse(File.ReadAllText(resultPath));

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

            // The engine expects a JSON string; serialise the array with no extra whitespace.
            string script = scriptTk.ToString(Formatting.None);

            yield return new TestCaseData(input, script, expected)
                .SetName(Path.GetFileNameWithoutExtension(file));
        }
    }

    /// <summary>
    /// Load all fixture triplets for the given command/function name as raw JSON strings.
    /// Returns (string inputJson, string script, string expectedJson) for adapter-agnostic tests.
    /// </summary>
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
            var inputPath  = Path.Combine(dir, "input.json");
            var scriptPath = Path.Combine(dir, "script.json");
            var resultPath = Path.Combine(dir, "result.json");

            if (!File.Exists(inputPath) || !File.Exists(scriptPath) || !File.Exists(resultPath))
                continue;

            string inputJson    = File.ReadAllText(inputPath);
            string script       = File.ReadAllText(scriptPath);
            string expectedJson = File.ReadAllText(resultPath);

            yield return new TestCaseData(inputJson, script, expectedJson)
                .SetName(Path.GetFileName(dir));
        }
    }
}
