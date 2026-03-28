using System.Xml.Linq;
using NUnit.Framework;

namespace TLio.Xml.Tests.SlashPath;

public static class XmlFixtureLoader
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
            var fixturePath = Path.Combine(dir, "fixture.xml");
            if (!File.Exists(fixturePath))
                continue;

            var fixture  = XElement.Parse(File.ReadAllText(fixturePath));
            XElement input    = fixture.Element("input")!.Elements().First();
            string script     = fixture.Element("script")!.ToString(SaveOptions.DisableFormatting);
            XElement expected = fixture.Element("result")!.Elements().First();

            yield return new TestCaseData(input, script, expected)
                .SetName(Path.GetFileName(dir));
        }
    }
}
