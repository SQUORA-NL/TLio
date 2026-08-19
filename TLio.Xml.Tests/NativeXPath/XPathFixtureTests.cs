using System.Xml.Linq;
using NUnit.Framework;
using TLio.Client;
using TLio.Xml;

namespace TLio.Xml.Tests.NativeXPath;

[TestFixture]
public class XPathFixtureTests
{
    private XmlScriptParser<XElement> _parser = null!;

    [SetUp]
    public void SetUp()
    {
        var options = ParseOptions<XElement>.CreateDefault();
        _parser = new XmlScriptParser<XElement>(
            options.CommandsProvider,
            options.FunctionsProvider,
            new XmlNodeAdapter());
    }

    [TestCaseSource(typeof(XPathFixtureLoader), nameof(XPathFixtureLoader.Load), new object[] { "XPathSet" })]
    public void Set(XElement input, string script, XElement expected) => Run(input, script, expected);

    [TestCaseSource(typeof(XPathFixtureLoader), nameof(XPathFixtureLoader.Load), new object[] { "XPathAdd" })]
    public void Add(XElement input, string script, XElement expected) => Run(input, script, expected);

    [TestCaseSource(typeof(XPathFixtureLoader), nameof(XPathFixtureLoader.Load), new object[] { "XPathPut" })]
    public void Put(XElement input, string script, XElement expected) => Run(input, script, expected);

    [TestCaseSource(typeof(XPathFixtureLoader), nameof(XPathFixtureLoader.Load), new object[] { "XPathRemove" })]
    public void Remove(XElement input, string script, XElement expected) => Run(input, script, expected);

    [TestCaseSource(typeof(XPathFixtureLoader), nameof(XPathFixtureLoader.Load), new object[] { "XPathRename" })]
    public void Rename(XElement input, string script, XElement expected) => Run(input, script, expected);

    [TestCaseSource(typeof(XPathFixtureLoader), nameof(XPathFixtureLoader.Load), new object[] { "XPathCopy" })]
    public void Copy(XElement input, string script, XElement expected) => Run(input, script, expected);

    [TestCaseSource(typeof(XPathFixtureLoader), nameof(XPathFixtureLoader.Load), new object[] { "XPathMove" })]
    public void Move(XElement input, string script, XElement expected) => Run(input, script, expected);

    private void Run(XElement input, string script, XElement expected)
    {
        var context = XmlExecutionContext.CreateWithNativeXPath();
        var tLioScript = _parser.ParseScript(script);
        var result = tLioScript.Execute(input, context);

        Assert.That(result.Success, Is.True,
            $"Engine reported failure. Log:\n{string.Join("\n", context.GetLogEntries().Select(e => $"  [{e.Level}] {e.Group}: {e.Message}"))}");

        Assert.That(XNode.DeepEquals(result.Data, expected), Is.True,
            $"Result mismatch.\n  Expected: {expected}\n  Actual:   {result.Data}");
    }
}
