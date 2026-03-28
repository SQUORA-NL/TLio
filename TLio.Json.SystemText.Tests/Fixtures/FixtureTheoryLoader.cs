using System.Text.Json;
using NUnit.Framework;

namespace TLio.Json.SystemText.Tests.Fixtures;

public static class FixtureTheoryLoader
{
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

            using var doc = JsonDocument.Parse(File.ReadAllText(fixturePath));
            var root = doc.RootElement;

            string inputJson    = root.GetProperty("input").GetRawText();
            string script       = root.GetProperty("script").GetRawText();
            string expectedJson = root.GetProperty("result").GetRawText();

            yield return new TestCaseData(inputJson, script, expectedJson)
                .SetName(Path.GetFileName(dir));
        }
    }
}
