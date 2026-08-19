using System.Xml.Linq;
using Microsoft.Extensions.Logging;
using NUnit.Framework;
using TLio.Commands;
using TLio.Core.Contracts;
using TLio.Core.Models;

namespace TLio.Xml.Tests.Commands;

/// <summary>
/// Paths are anchored on the document node, the way XPath defines it, so
/// <c>&lt;order&gt;&lt;customer&gt;Ada&lt;/customer&gt;&lt;/order&gt;</c> addresses its customer as
/// <c>/order/customer</c> — the document element is named in the path, never skipped.
///
/// A path that resolves to nothing warns and leaves the document alone; it is neither
/// scaffolded into existence nor a script failure.
/// </summary>
[TestFixture]
public class XmlSetMissingPathTests
{
    private static IExecutionContext<XElement> Context(bool nativeXPath) => nativeXPath
        ? XmlExecutionContext.CreateWithNativeXPath()
        : XmlExecutionContext.CreateWithSlashPaths();

    private static XElement Doc(string xml) => new XmlNodeAdapter().Parse(xml);

    private const string Order = "<order><customer>Ada</customer></order>";

    [TestCase(false)]
    [TestCase(true)]
    public void Set_OnTheFullPathIncludingTheDocumentElement_UpdatesTheElement(bool nativeXPath)
    {
        var context = Context(nativeXPath);
        var data = Doc(Order);

        var result = new Set<XElement>("/order/customer", new FixedValue<XElement>(new XElement("v", "Grace")))
            .Execute(data, context);

        Assert.That(result.Success, Is.True);
        Assert.That(data.ToString(SaveOptions.DisableFormatting),
            Is.EqualTo("<order><customer>Grace</customer></order>"));
    }

    [TestCase(false)]
    [TestCase(true)]
    public void Set_OnARecursiveDescentPath_UpdatesTheElementAtAnyDepth(bool nativeXPath)
    {
        var context = Context(nativeXPath);
        var data = Doc("<order><lines><line><customer>Ada</customer></line></lines></order>");

        var result = new Set<XElement>("//customer", new FixedValue<XElement>(new XElement("v", "Grace")))
            .Execute(data, context);

        Assert.That(result.Success, Is.True);
        Assert.That(data.ToString(SaveOptions.DisableFormatting),
            Is.EqualTo("<order><lines><line><customer>Grace</customer></line></lines></order>"));
    }

    [TestCase("/customer", false)]
    [TestCase("customer", false)]
    [TestCase("/customer", true)]
    [TestCase("customer", true)]
    public void Set_OnAPathThatSkipsTheDocumentElement_WarnsAndChangesNothing(string path, bool nativeXPath)
    {
        var context = Context(nativeXPath);
        var data = Doc(Order);

        var result = new Set<XElement>(path, new FixedValue<XElement>(new XElement("v", "Grace")))
            .Execute(data, context);

        Assert.That(result.Success, Is.True, "an unresolvable path is a no-op, not a failure");
        Assert.That(data.ToString(SaveOptions.DisableFormatting), Is.EqualTo(Order),
            "a bare step is a child of the document node — it must not reach into <order>");
        Assert.That(context.GetLogEntries().Any(e =>
                e.Level == LogLevel.Warning && e.Message.Contains("no nodes matched")), Is.True);
    }

    [TestCase(false)]
    [TestCase(true)]
    public void Set_OnAMissingPath_DoesNotScaffoldTheStructure(bool nativeXPath)
    {
        var context = Context(nativeXPath);
        var data = Doc(Order);

        var result = new Set<XElement>("/order/shipping/address",
            new FixedValue<XElement>(new XElement("v", "Grace"))).Execute(data, context);

        Assert.That(result.Success, Is.True);
        Assert.That(data.ToString(SaveOptions.DisableFormatting), Is.EqualTo(Order),
            "set never invents the path it could not find");
        Assert.That(context.GetLogEntries().Any(e =>
                e.Level == LogLevel.Warning && e.Message.Contains("no nodes matched")), Is.True);
    }

    [TestCase(false)]
    [TestCase(true)]
    public void Set_OnTheRootPath_WarnsInsteadOfThrowing(bool nativeXPath)
    {
        var context = Context(nativeXPath);
        var data = Doc(Order);

        var result = new Set<XElement>("/", new FixedValue<XElement>(new XElement("v", "Grace")))
            .Execute(data, context);

        Assert.That(result.Success, Is.True);
        Assert.That(data.ToString(SaveOptions.DisableFormatting), Is.EqualTo(Order));
        Assert.That(context.GetLogEntries().Any(e =>
                e.Level == LogLevel.Warning && e.Message.Contains("document root")), Is.True,
            "the document node has no property name to set — say so rather than crashing");
    }

    [Test]
    public void Set_OnAWildcardLeaf_WarnsInsteadOfThrowing()
    {
        var context = Context(nativeXPath: false);
        var data = Doc(Order);

        // '*' is not a usable element name; it used to reach XName.Get and throw an XmlException
        var result = new Set<XElement>("/order/*", new FixedValue<XElement>(new XElement("v", "Grace")))
            .Execute(data, context);

        Assert.That(result.Success, Is.True);
        Assert.That(context.GetLogEntries().Any(e => e.Level == LogLevel.Warning), Is.True);
    }

    [Test]
    public void MoveToRoot_ReplacesTheWholeDocumentContent()
    {
        var context = Context(nativeXPath: false);
        var data = Doc("<order><customer>Ada</customer><replacement><customer>Grace</customer></replacement></order>");

        var result = new Move<XElement>("/order/replacement", "/").Execute(data, context);

        Assert.That(result.Success, Is.True);
        Assert.That(data.ToString(SaveOptions.DisableFormatting),
            Is.EqualTo("<order><customer>Grace</customer></order>"),
            "move-to-root replaces the document body; the element's own name is a rename job");
    }
}
