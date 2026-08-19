using System.Xml.Linq;
using NUnit.Framework;
using TLio.Client;
using TLio.Xml;

namespace TLio.Xml.Tests.SlashPath;

/// <summary>
/// Data-driven XML fixture tests.
/// Each fixture folder under Fixtures/Xml&lt;Command&gt;/ contains:
///   input.xml   — the XML document to transform
///   script.xml  — the TLio script in XML format
///   result.xml  — the expected result after execution
///
/// The fixture runner uses XmlExecutionContext + XmlScriptParser.
/// </summary>
[TestFixture]
public class XmlFixtureTests
{
    private XmlScriptParser<XElement> _parser = null!;
    private ParseOptions<XElement> _options = null!;

    [SetUp]
    public void SetUp()
    {
        _options = ParseOptions<XElement>.CreateDefault();
        _parser  = new XmlScriptParser<XElement>(
            _options.CommandsProvider,
            _options.FunctionsProvider,
            new XmlNodeAdapter());
    }

    [TestCaseSource(typeof(XmlFixtureLoader), nameof(XmlFixtureLoader.Load), new object[] { "XmlSet" })]
    public void Set(XElement input, string script, XElement expected) => Run(input, script, expected);

    [TestCaseSource(typeof(XmlFixtureLoader), nameof(XmlFixtureLoader.Load), new object[] { "XmlAdd" })]
    public void Add(XElement input, string script, XElement expected) => Run(input, script, expected);

    [TestCaseSource(typeof(XmlFixtureLoader), nameof(XmlFixtureLoader.Load), new object[] { "XmlPut" })]
    public void Put(XElement input, string script, XElement expected) => Run(input, script, expected);

    [TestCaseSource(typeof(XmlFixtureLoader), nameof(XmlFixtureLoader.Load), new object[] { "XmlRemove" })]
    public void Remove(XElement input, string script, XElement expected) => Run(input, script, expected);

    [TestCaseSource(typeof(XmlFixtureLoader), nameof(XmlFixtureLoader.Load), new object[] { "XmlRename" })]
    public void Rename(XElement input, string script, XElement expected) => Run(input, script, expected);

    [TestCaseSource(typeof(XmlFixtureLoader), nameof(XmlFixtureLoader.Load), new object[] { "XmlCopy" })]
    public void Copy(XElement input, string script, XElement expected) => Run(input, script, expected);

    [TestCaseSource(typeof(XmlFixtureLoader), nameof(XmlFixtureLoader.Load), new object[] { "XmlMove" })]
    public void Move(XElement input, string script, XElement expected) => Run(input, script, expected);

    private void Run(XElement input, string script, XElement expected)
    {
        var context = XmlExecutionContext.CreateWithSlashPaths();
        var tLioScript = _parser.ParseScript(script);
        var result = tLioScript.Execute(input, context);

        Assert.That(result.Success, Is.True,
            $"Engine reported failure. Log:\n{string.Join("\n", context.GetLogEntries().Select(e => $"  [{e.Level}] {e.Group}: {e.Message}"))}");

        Assert.That(XNode.DeepEquals(result.Data, expected), Is.True,
            $"Result mismatch.\n  Expected: {expected}\n  Actual:   {result.Data}");
    }
}
