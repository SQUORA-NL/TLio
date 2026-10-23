using System.Xml.Linq;
using NUnit.Framework;
using TLio.Core.Contracts;
using TLio.Xml;

namespace TLio.Parity.Tests;

/// <summary>
/// <c>resolve()</c> and <c>with</c> on XML, written the way an XML author writes them, against
/// both path fetchers. The parity fixtures run the slash-path fetcher through a path
/// translation; this holds what that translation cannot say — the native XPath fetcher, and a
/// <c>returnField</c> spelled in XML's own path language.
/// </summary>
[TestFixture]
public class XmlLookupTests
{
    private const string Doc = """
        <root>
          <policy><holder>p2</holder><plan>gold</plan></policy>
          <party>
            <item><refKey>p1</refKey><firstName>Ada</firstName>
              <contact><mail>ada@x.nl</mail></contact>
              <address><item><city>Utrecht</city></item><item><city>Delft</city></item></address></item>
            <item><refKey>p2</refKey><firstName>Grace</firstName>
              <contact><mail>grace@x.nl</mail></contact>
              <address><item><city>Leiden</city></item></address></item>
          </party>
        </root>
        """;

    private static IExecutionContext<XElement> Context(bool nativeXPath) => nativeXPath
        ? XmlExecutionContext.CreateWithNativeXPath()
        : XmlExecutionContext.CreateWithSlashPaths();

    private static XElement Run(string script, bool nativeXPath, out IExecutionContext<XElement> context)
    {
        var options = FormatRunners.Options<XElement>();
        var adapter = new XmlNodeAdapter();
        var parser = new XmlScriptParser<XElement>(options.CommandsProvider, options.FunctionsProvider, adapter);
        context = Context(nativeXPath);

        var data = adapter.Parse(Doc);
        var result = parser.ParseScript(script).Execute(data, context);
        Assert.That(result.Success, Is.True,
            string.Join("\n", context.GetLogEntries().Select(e => $"[{e.Level}] {e.Message}")));
        return result.Data;
    }

    [TestCase(false)]
    [TestCase(true)]
    public void Resolve_ReturnsAChildElementByKey(bool nativeXPath)
    {
        var data = Run("""
            <script>
              <put path="/root/name"><value>=resolve(/root/party/item, 'refKey', =fetch(/root/policy/holder), 'firstName')</value></put>
            </script>
            """, nativeXPath, out _);

        Assert.That(data.Element("name")!.Value, Is.EqualTo("Grace"));
    }

    [TestCase(false)]
    [TestCase(true)]
    public void Resolve_ReturnField_NestedAndSubscriptedInXPathSpelling(bool nativeXPath)
    {
        var data = Run("""
            <script>
              <put path="/root/mail"><value>=resolve(/root/party/item, 'refKey', 'p1', 'contact/mail')</value></put>
              <put path="/root/city"><value>=resolve(/root/party/item, 'refKey', 'p1', 'address/item[2]/city')</value></put>
            </script>
            """, nativeXPath, out _);

        Assert.That(data.Element("mail")!.Value, Is.EqualTo("ada@x.nl"));
        Assert.That(data.Element("city")!.Value, Is.EqualTo("Delft"));
    }

    [TestCase(false)]
    [TestCase(true)]
    public void Resolve_PicksTheSecondElement_NotTheFirstWithTheSameName(bool nativeXPath)
    {
        // Both <item>s share a name; the slash fetcher cannot tell them apart by path, which is
        // why a plain returnField is walked through the adapter instead.
        var data = Run("""
            <script>
              <put path="/root/mail"><value>=resolve(/root/party/item, 'refKey', 'p2', 'contact/mail')</value></put>
            </script>
            """, nativeXPath, out _);

        Assert.That(data.Element("mail")!.Value, Is.EqualTo("grace@x.nl"));
    }

    [TestCase(false)]
    [TestCase(true)]
    public void With_AddressesTheSelectedElement(bool nativeXPath)
    {
        var data = Run("""
            <script>
              <with path="/root/policy" title="the policy">
                <commands>
                  <put path="/root/out/plan"><value>=fetch(./plan)</value></put>
                  <put path="/root/out/holder"><value>=resolve(/root/party/item, 'refKey', =fetch(./holder), 'firstName')</value></put>
                </commands>
              </with>
            </script>
            """, nativeXPath, out var context);

        Assert.That(data.Element("out")!.Element("plan")!.Value, Is.EqualTo("gold"));
        Assert.That(data.Element("out")!.Element("holder")!.Value, Is.EqualTo("Grace"));
        Assert.That(context.CurrentNode, Is.Null);
    }
}
