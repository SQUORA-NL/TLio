using System.Xml.Linq;
using NUnit.Framework;

namespace TLio.Xml.Tests.NativeXPath;

public static class XPathFixtureLoader
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
            // Deep-copy to detach from the fixture document — XPath `//` axes navigate to the
            // document root, so leaving the element attached causes `//city` to search the
            // entire fixture (including the <result> section) rather than just the input tree.
            XElement input    = new XElement(fixture.Element("input")!.Elements().First());
            string script     = fixture.Element("script")!.ToString(SaveOptions.DisableFormatting);
            XElement expected = new XElement(fixture.Element("result")!.Elements().First());

            yield return new TestCaseData(input, script, expected)
                .SetName(Path.GetFileName(dir));
        }
    }
}
