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
            // Re-parse into its own document: paths are absolute from the document node, so
            // the input must be a document element, not an element still nested in <fixture>.
            XElement input    = Detach(fixture.Element("input")!.Elements().First());
            string script     = fixture.Element("script")!.ToString(SaveOptions.DisableFormatting);
            XElement expected = Detach(fixture.Element("result")!.Elements().First());

            yield return new TestCaseData(input, script, expected)
                .SetName(Path.GetFileName(dir));
        }
    }

    private static XElement Detach(XElement element) =>
        XDocument.Parse(element.ToString(SaveOptions.DisableFormatting)).Root!;
}
