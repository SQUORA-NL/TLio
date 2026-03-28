using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using NUnit.Framework;

namespace TLio.Functions.Tests.Fixtures;

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

            var input    = (doc["input"]  ?? throw new InvalidOperationException($"Missing 'input' in {file}")).DeepClone();
            var scriptTk =  doc["script"] ?? throw new InvalidOperationException($"Missing 'script' in {file}");
            var expected = (doc["result"] ?? throw new InvalidOperationException($"Missing 'result' in {file}")).DeepClone();

            string script = scriptTk.ToString(Formatting.None);

            yield return new TestCaseData(input, script, expected)
                .SetName(Path.GetFileNameWithoutExtension(file));
        }
    }
}
