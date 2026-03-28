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
