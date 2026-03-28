using System.Xml.Linq;
using NUnit.Framework;
using TLio.Commands;
using TLio.Core.Contracts;
using TLio.Core.Models;
using TLio.Xml;

namespace TLio.UnitTests.AdapterTests;

/// <summary>
/// Integration tests for TLio commands executed against a real XElement document.
/// Verifies that Set, Add, Put, Remove, Copy, and Move work end-to-end through
/// the XML adapter layer.
/// </summary>
[TestFixture]
public class XmlCommandIntegrationTests
{
    private XElement _data = null!;
    private IExecutionContext<XElement> _context = null!;

    [SetUp]
    public void SetUp()
    {
        _context = XmlExecutionContext.CreateDefault();
        _data = XElement.Parse(
            """
            <root>
              <name>Alice</name>
              <age>30</age>
              <address>
                <city>Amsterdam</city>
                <country>NL</country>
              </address>
              <scores>
                <score>10</score>
                <score>20</score>
                <score>30</score>
              </scores>
            </root>
            """);
    }

    // ── Set ───────────────────────────────────────────────────────────────────

    [Test]
    public void Set_ExistingPath_UpdatesValue()
    {
        var result = new Set<XElement>("/name",
            new FixedValue<XElement>(XElement.Parse("<value>Bob</value>")))
            .Execute(_data, _context);

        Assert.That(result.Success, Is.True);
        Assert.That(_data.Element("name")!.Value, Is.EqualTo("Bob"));
    }

    [Test]
    public void Set_NestedPath_UpdatesDeepValue()
    {
        var result = new Set<XElement>("/address/city",
            new FixedValue<XElement>(XElement.Parse("<value>Rotterdam</value>")))
            .Execute(_data, _context);

        Assert.That(result.Success, Is.True);
        Assert.That(_data.Element("address")!.Element("city")!.Value, Is.EqualTo("Rotterdam"));
    }

    // ── Add ───────────────────────────────────────────────────────────────────

    [Test]
    public void Add_NewProperty_AddsChildElement()
    {
        var result = new Add<XElement>("/phone",
            new FixedValue<XElement>(XElement.Parse("<value>+31612345678</value>")))
            .Execute(_data, _context);

        Assert.That(result.Success, Is.True);
        Assert.That(_data.Element("phone")!.Value, Is.EqualTo("+31612345678"));
    }

    [Test]
    public void Add_ChildToExistingParent_AddsElement()
    {
        // /address exists; /address/email does not — Add should create it with the given value
        var result = new Add<XElement>("/address/email",
            new FixedValue<XElement>(XElement.Parse("<value>alice@example.com</value>")))
            .Execute(_data, _context);

        Assert.That(result.Success, Is.True);
        Assert.That(_data.Element("address")!.Element("email")!.Value, Is.EqualTo("alice@example.com"));
    }

    // ── Put ───────────────────────────────────────────────────────────────────

    [Test]
    public void Put_ExistingPath_UpdatesValue()
    {
        var result = new Put<XElement>("/age",
            new FixedValue<XElement>(XElement.Parse("<value>31</value>")))
            .Execute(_data, _context);

        Assert.That(result.Success, Is.True);
        Assert.That(_data.Element("age")!.Value, Is.EqualTo("31"));
    }

    [Test]
    public void Put_NonExistingPath_AddsValue()
    {
        var result = new Put<XElement>("/nickname",
            new FixedValue<XElement>(XElement.Parse("<value>Ali</value>")))
            .Execute(_data, _context);

        Assert.That(result.Success, Is.True);
        Assert.That(_data.Element("nickname")!.Value, Is.EqualTo("Ali"));
    }

    // ── Remove ────────────────────────────────────────────────────────────────

    [Test]
    public void Remove_ExistingPath_RemovesElement()
    {
        var result = new Remove<XElement>("/age")
            .Execute(_data, _context);

        Assert.That(result.Success, Is.True);
        Assert.That(_data.Element("age"), Is.Null);
    }

    [Test]
    public void Remove_NestedPath_RemovesDeepElement()
    {
        var result = new Remove<XElement>("/address/country")
            .Execute(_data, _context);

        Assert.That(result.Success, Is.True);
        Assert.That(_data.Element("address")!.Element("country"), Is.Null);
        Assert.That(_data.Element("address")!.Element("city"), Is.Not.Null);
    }

    // ── Copy ──────────────────────────────────────────────────────────────────

    [Test]
    public void Copy_ExistingPath_CopiesValueToDestination()
    {
        var result = new Copy<XElement>("/name", "/nameCopy")
            .Execute(_data, _context);

        Assert.That(result.Success, Is.True);
        Assert.That(_data.Element("nameCopy")!.Value, Is.EqualTo("Alice"));
        Assert.That(_data.Element("name"), Is.Not.Null); // source preserved
    }

    // ── Move ──────────────────────────────────────────────────────────────────

    [Test]
    public void Move_ExistingPath_MovesValueToDestination()
    {
        var result = new Move<XElement>("/name", "/fullName")
            .Execute(_data, _context);

        Assert.That(result.Success, Is.True);
        Assert.That(_data.Element("fullName")!.Value, Is.EqualTo("Alice"));
        Assert.That(_data.Element("name"), Is.Null); // source removed
    }
}
