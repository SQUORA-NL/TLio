using System.Xml.Linq;
using Microsoft.Extensions.Logging;
using NUnit.Framework;
using TLio.Commands;
using TLio.Core.Contracts;
using TLio.Core.Models;

namespace TLio.Xml.Tests.Adapters;

/// <summary>
/// Whitespace-only text is formatting, not data. Parsing normally strips it, but a tree loaded
/// with <c>LoadOptions.PreserveWhitespace</c> (or built by hand) still carries it — and it must
/// not change what kind of node an element is: <c>&lt;customer&gt;\n&lt;/customer&gt;</c> is as
/// writable a container as <c>&lt;customer/&gt;</c>. Classifying it as a primitive is what
/// produced "add: cannot add property to a primitive node" for a visibly empty element.
///
/// Only classification ignores the whitespace. As a value the text is kept, so a deliberate
/// <c>" "</c> string written by a script survives the round trip.
/// </summary>
[TestFixture]
public class XmlWhitespaceClassificationTests
{
    private XmlNodeAdapter _adapter = null!;

    [SetUp]
    public void SetUp() => _adapter = new XmlNodeAdapter();

    private static XElement Preserved(string xml) =>
        XElement.Parse(xml, LoadOptions.PreserveWhitespace);

    private static IExecutionContext<XElement> Context(bool nativeXPath) => nativeXPath
        ? XmlExecutionContext.CreateWithNativeXPath()
        : XmlExecutionContext.CreateWithSlashPaths();

    // ── Classification ────────────────────────────────────────────────────────

    [TestCase("<customer>\n</customer>")]
    [TestCase("<customer>  </customer>")]
    [TestCase("<customer>\n\t  \n</customer>")]
    public void AWhitespaceOnlyElement_IsStillAContainer(string xml)
    {
        var el = Preserved(xml);
        Assert.That(el.Nodes().OfType<XText>().Any(), Is.True,
            "precondition: the whitespace text node must actually be preserved");

        Assert.That(_adapter.IsObject(el), Is.True,
            "a whitespace-only element is the same unfilled container as an empty one");
        Assert.That(_adapter.IsArray(el), Is.False);
    }

    [Test]
    public void AnElementWithRealText_IsNotAContainer()
    {
        var el = Preserved("<customer>x</customer>");

        Assert.That(_adapter.IsObject(el), Is.False);
        Assert.That(_adapter.IsPrimitive(el), Is.True);
    }

    // ── Value reads keep the whitespace ───────────────────────────────────────

    [Test]
    public void AWhitespaceOnlyElement_StillReadsAsItsText()
    {
        // The fix is deliberately classification-only: a script that wrote " " must read " "
        // back, or =concat($.a, ' ', $.b) loses its separator.
        var el = Preserved("<k> </k>");

        Assert.That(_adapter.TryGetString(el), Is.EqualTo(" "));
        Assert.That(_adapter.IsNull(el), Is.False);
    }

    // ── End to end: the reported case ─────────────────────────────────────────

    [TestCase(false)]
    [TestCase(true)]
    public void Add_IntoAWhitespaceOnlyDocumentElement_CreatesTheProperty(bool nativeXPath)
    {
        // <customer>\n</customer> — the adapter's own parse strips the whitespace.
        var context = Context(nativeXPath);
        var data = _adapter.Parse("<customer>\n</customer>");

        var result = new Add<XElement>("/customer/demo", new FixedValue<XElement>(new XElement("v", "3")))
            .Execute(data, context);

        Assert.That(result.Success, Is.True);
        Assert.That(data.Element("demo")?.Value, Is.EqualTo("3"));
        Assert.That(context.GetLogEntries().Any(e => e.Level == LogLevel.Warning), Is.False,
            "an empty element is a container — adding to it must not warn");
    }

    [TestCase(false)]
    [TestCase(true)]
    public void Add_IntoAWhitespacePreservedDocumentElement_CreatesTheProperty(bool nativeXPath)
    {
        // The same document with the whitespace text node still in the tree.
        var context = Context(nativeXPath);
        var data = XDocument.Parse("<customer>\n</customer>", LoadOptions.PreserveWhitespace).Root!;
        Assert.That(data.Nodes().OfType<XText>().Any(), Is.True,
            "precondition: the whitespace text node must actually be preserved");

        var result = new Add<XElement>("/customer/demo", new FixedValue<XElement>(new XElement("v", "3")))
            .Execute(data, context);

        Assert.That(result.Success, Is.True);
        Assert.That(data.Element("demo")?.Value, Is.EqualTo("3"));
        Assert.That(context.GetLogEntries().Any(e => e.Level == LogLevel.Warning), Is.False,
            "whitespace-only content must not turn the element into a primitive");
    }

    // ── The empty element as an array position ────────────────────────────────

    [TestCase(false)]
    [TestCase(true)]
    public void Add_AtPositionOneOfAnEmptyElement_StartsTheArray(bool nativeXPath)
    {
        // <a/> is [] as much as it is {} — XPath's item[1] names the one position that can be
        // created in it, so add starts the array rather than refusing "'/root/a' is not an
        // array". Matches JSON: {"a":null} + add $.a[0] creates {"a":[1]}.
        var context = Context(nativeXPath);
        var data = _adapter.Parse("<root><a/></root>");

        var result = new Add<XElement>("/root/a/item[1]", new FixedValue<XElement>(new XElement("v", "1")))
            .Execute(data, context);

        Assert.That(result.Success, Is.True);
        Assert.That(data.ToString(SaveOptions.DisableFormatting),
            Is.EqualTo("<root><a><item>1</item></a></root>"));
    }

    [TestCase(false)]
    [TestCase(true)]
    public void Add_AtALaterPositionOfAnEmptyElement_WarnsAndChangesNothing(bool nativeXPath)
    {
        // Only the position just past the end can be created — for an empty container that is
        // position one. Anything further out would land at an index the path did not name.
        var context = Context(nativeXPath);
        var data = _adapter.Parse("<root><a/></root>");

        var result = new Add<XElement>("/root/a/item[2]", new FixedValue<XElement>(new XElement("v", "1")))
            .Execute(data, context);

        Assert.That(result.Success, Is.True, "an unreachable position is a no-op, not a failure");
        Assert.That(data.Element("a"), Is.Not.Null);
        Assert.That(data.Element("a")!.HasElements, Is.False, "nothing may be added at position two");
        Assert.That(context.GetLogEntries().Any(e =>
            e.Level == LogLevel.Warning && e.Message.Contains("no nodes matched")), Is.True);
    }
}
