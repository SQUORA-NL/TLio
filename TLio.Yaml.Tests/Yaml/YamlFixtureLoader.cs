using NUnit.Framework;
using YamlDotNet.RepresentationModel;

namespace TLio.Yaml.Tests.Yaml;

public static class YamlFixtureLoader
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
            var fixturePath = Path.Combine(dir, "fixture.yaml");
            if (!File.Exists(fixturePath))
                continue;

            var stream = new YamlStream();
            stream.Load(new StringReader(File.ReadAllText(fixturePath)));

            if (stream.Documents.Count < 3)
                continue;

            YamlNode input    = stream.Documents[0].RootNode;
            YamlNode expected = stream.Documents[2].RootNode;

            // Serialize script document back to YAML text for YamlScriptParser
            var scriptStream = new YamlStream(stream.Documents[1]);
            using var sw = new StringWriter();
            scriptStream.Save(sw, assignAnchors: false);
            string script = sw.ToString();

            yield return new TestCaseData(input, script, expected)
                .SetName(Path.GetFileName(dir));
        }
    }
}
