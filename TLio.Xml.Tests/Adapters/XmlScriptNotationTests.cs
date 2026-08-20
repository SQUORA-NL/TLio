using System.Xml.Linq;
using NUnit.Framework;
using TLio.Client;
using TLio.Commands;
using TLio.Commands.Advanced;
using TLio.Core.Contracts;

namespace TLio.Xml.Tests.Adapters;

/// <summary>
/// The XML script notation describes the same commands the JSON notation does. Until the
/// alignment only string properties were readable, so a command whose behaviour is carried by a
/// flag, an enum, a nested script or a settings object parsed into a command that quietly did
/// something else — copy ignored destinationAsArray, and ifElse had no condition at all.
/// </summary>
[TestFixture]
public class XmlScriptNotationTests
{
    private XmlScriptParser<XElement> _parser = null!;

    [SetUp]
    public void SetUp()
    {
        var options = ParseOptions<XElement>.CreateDefault();
        _parser = new XmlScriptParser<XElement>(
            options.CommandsProvider, options.FunctionsProvider, new XmlNodeAdapter());
    }

    private ICommand<XElement> First(string script) => _parser.ParseScript(script).First();

    [Test]
    public void ABooleanAttribute_ReachesTheCommand()
    {
        var copy = (Copy<XElement>)First(
            """<script><copy fromPath="/a" toPath="/b" destinationAsArray="true"/></script>""");

        Assert.That(copy.DestinationAsArray, Is.True);
    }

    [Test]
    public void AnEnumAttribute_ReachesTheCommand()
    {
        var merge = (Merge<XElement>)First(
            """<script><merge path="/s" targetPath="/t" arrayMergeMode="replace"/></script>""");

        Assert.That(merge.ArrayMergeMode, Is.EqualTo(ArrayMergeMode.Replace));
    }

    [Test]
    public void AConditionAttribute_BecomesAValueExpression()
    {
        var ifElse = (IfElse<XElement>)First(
            """<script><ifElse condition="true"><ifScript><put path="/a">x</put></ifScript></ifElse></script>""");

        Assert.That(ifElse.Condition, Is.Not.Null);
        Assert.That(ifElse.IfScript, Is.Not.Null);
        Assert.That(ifElse.IfScript!.Count, Is.EqualTo(1));
    }

    [Test]
    public void NestedScripts_AreParsedAsScripts()
    {
        var ifElse = (IfElse<XElement>)First(
            """
            <script>
              <ifElse condition="false">
                <ifScript><put path="/a">x</put></ifScript>
                <elseScript><put path="/a">y</put><put path="/b">z</put></elseScript>
              </ifElse>
            </script>
            """);

        Assert.That(ifElse.IfScript!.Count, Is.EqualTo(1));
        Assert.That(ifElse.ElseScript!.Count, Is.EqualTo(2));
    }

    [Test]
    public void ASettingsElement_IsReadWithTheSameFieldNamesAsJson()
    {
        var merge = (Merge<XElement>)First(
            """
            <script>
              <merge path="/s" targetPath="/t">
                <settings>
                  <arraySettings>
                    <item>
                      <arrayPath>/t/items</arrayPath>
                      <keyPaths><item>id</item></keyPaths>
                    </item>
                  </arraySettings>
                </settings>
              </merge>
            </script>
            """);

        Assert.That(merge.Settings, Is.Not.Null);
        Assert.That(merge.Settings!.ArraySettings, Has.Count.EqualTo(1));
        Assert.That(merge.Settings.ArraySettings[0].ArrayPath, Is.EqualTo("/t/items"));
        Assert.That(merge.Settings.ArraySettings[0].KeyPaths, Is.EqualTo(new[] { "id" }));
    }

    [Test]
    public void AStructuredValue_KeepsAllOfItsProperties()
    {
        // Only the first child element used to be read, so <value><x/><y/></value> silently
        // became just x.
        var root = new XmlNodeAdapter().Parse("<root><a>1</a></root>");
        var context = XmlExecutionContext.CreateWithSlashPaths();

        _parser.ParseScript(
            """<script><put path="/root/point"><value><x>1</x><y>2</y></value></put></script>""")
            .Execute(root, context);

        Assert.That(root.ToString(SaveOptions.DisableFormatting),
            Is.EqualTo("<root><a>1</a><point><x>1</x><y>2</y></point></root>"));
    }

    [Test]
    public void AnEmptyValueElement_IsNull()
    {
        var root = new XmlNodeAdapter().Parse("<root><a>1</a></root>");
        var context = XmlExecutionContext.CreateWithSlashPaths();

        _parser.ParseScript("""<script><set path="/root/a"><value/></set></script>""")
            .Execute(root, context);

        Assert.That(root.ToString(SaveOptions.DisableFormatting), Is.EqualTo("<root><a /></root>"));
    }

    [Test]
    public void TextContent_IsStillTheValue()
    {
        var root = new XmlNodeAdapter().Parse("<root><a>1</a></root>");
        var context = XmlExecutionContext.CreateWithSlashPaths();

        _parser.ParseScript("""<script><set path="/root/a">two</set></script>""")
            .Execute(root, context);

        Assert.That(root.Element("a")!.Value, Is.EqualTo("two"));
    }
}
