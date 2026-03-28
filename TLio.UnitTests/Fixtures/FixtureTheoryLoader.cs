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
