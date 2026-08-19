using System.Xml.Linq;
using Microsoft.Extensions.Logging;
using NUnit.Framework;
using TLio.Commands;
using TLio.Core.Contracts;

namespace TLio.Xml.Tests.Commands;

/// <summary>
/// An XML element carries its own name, so rename is a true in-place operation: attributes,
/// children and document position all survive, and no parent is needed. That is what lets
/// the document element be renamed — the one thing copy+remove could never reach.
/// </summary>
[TestFixture]
public class XmlRenameTests
{
    private static IExecutionContext<XElement> Context(bool nativeXPath) => nativeXPath
        ? XmlExecutionContext.CreateWithNativeXPath()
        : XmlExecutionContext.CreateWithSlashPaths();

    private static XElement Doc(string xml) => new XmlNodeAdapter().Parse(xml);

    [TestCase(false)]
    [TestCase(true)]
    public void Rename_TheDocumentElement_RenamesTheRoot(bool nativeXPath)
    {
        var context = Context(nativeXPath);
        var data = Doc("<order><customer>Ada</customer></order>");

        var result = new Rename<XElement>("/order", "opdracht").Execute(data, context);

        Assert.That(result.Success, Is.True);
        Assert.That(data.ToString(SaveOptions.DisableFormatting),
            Is.EqualTo("<opdracht><customer>Ada</customer></opdracht>"));
    }

    [Test]
    public void Rename_TheDocumentElement_KeepsAttributesAndChildren()
    {
        var context = Context(nativeXPath: true);
        var data = Doc("""<order id="7" region="NL"><customer>Ada</customer><total>10</total></order>""");

        new Rename<XElement>("/order", "opdracht").Execute(data, context);

        Assert.That(data.ToString(SaveOptions.DisableFormatting),
            Is.EqualTo("""<opdracht id="7" region="NL"><customer>Ada</customer><total>10</total></opdracht>"""));
    }

    [Test]
    public void Rename_TheDocumentElement_LeavesTheCallersReferenceLive()
    {
        var context = Context(nativeXPath: true);
        var data = Doc("<order><customer>Ada</customer></order>");

        var result = new Rename<XElement>("/order", "opdracht").Execute(data, context);

        Assert.That(ReferenceEquals(result.Data, data), Is.True,
            "renaming in place is what keeps the engine's data context the live document");
        Assert.That(result.Data.Document, Is.Not.Null);
    }

    [Test]
    public void Rename_AChildElement_KeepsItsPositionAmongSiblings()
    {
        var context = Context(nativeXPath: true);
        var data = Doc("<order><customer>Ada</customer><total>10</total></order>");

        new Rename<XElement>("/order/customer", "client").Execute(data, context);

        Assert.That(data.ToString(SaveOptions.DisableFormatting),
            Is.EqualTo("<order><client>Ada</client><total>10</total></order>"),
            "rename preserves order, which copy+remove would not");
    }

    [Test]
    public void Rename_RecursiveDescent_RenamesEveryMatchAtAnyDepth()
    {
        var context = Context(nativeXPath: true);
        var data = Doc("<order><item>a</item><lines><item>b</item></lines></order>");

        var result = new Rename<XElement>("//item", "line").Execute(data, context);

        Assert.That(result.Success, Is.True);
        Assert.That(data.ToString(SaveOptions.DisableFormatting),
            Is.EqualTo("<order><line>a</line><lines><line>b</line></lines></order>"));
    }

    [Test]
    public void Rename_MissingPath_WarnsAndContinues()
    {
        var context = Context(nativeXPath: true);
        var data = Doc("<order><customer>Ada</customer></order>");

        var result = new Rename<XElement>("/order/missing", "whatever").Execute(data, context);

        Assert.That(result.Success, Is.True, "a missing path is a no-op, not a failure");
        Assert.That(data.ToString(SaveOptions.DisableFormatting),
            Is.EqualTo("<order><customer>Ada</customer></order>"));
        Assert.That(context.GetLogEntries().Any(e =>
            e.Level == LogLevel.Warning && e.Message.Contains("no nodes matched")), Is.True);
    }

    [Test]
    public void Rename_ToAnUnusableElementName_WarnsAndChangesNothing()
    {
        var context = Context(nativeXPath: true);
        var data = Doc("<order><customer>Ada</customer></order>");

        var result = new Rename<XElement>("/order/customer", "not a name").Execute(data, context);

        Assert.That(result.Success, Is.True);
        Assert.That(data.ToString(SaveOptions.DisableFormatting),
            Is.EqualTo("<order><customer>Ada</customer></order>"));
        Assert.That(context.GetLogEntries().Any(e => e.Level == LogLevel.Warning), Is.True);
    }

    [Test]
    public void Rename_ThroughTheScriptEngine_UsingTheRegisteredCommandName()
    {
        var context = Context(nativeXPath: true);
        var options = TLio.Client.ParseOptions<XElement>.CreateDefault();
        var engine = new TLio.Client.ScriptEngine<XElement>(
            options.CommandsProvider, options.FunctionsProvider);

        var data = context.NodeAdapter.Parse("<order><customer>Ada</customer></order>");
        var result = engine.Execute(
            """[{"command":"rename","path":"/order","name":"opdracht"}]""", data, context);

        Assert.That(result.Success, Is.True);
        Assert.That(context.NodeAdapter.Serialize(result.Data),
            Is.EqualTo("<opdracht><customer>Ada</customer></opdracht>"));
    }
}
