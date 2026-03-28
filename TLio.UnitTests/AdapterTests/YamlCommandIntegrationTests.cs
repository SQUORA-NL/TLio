using NUnit.Framework;
using TLio.Commands;
using TLio.Core.Contracts;
using TLio.Core.Models;
using TLio.Yaml;
using YamlDotNet.RepresentationModel;

namespace TLio.UnitTests.AdapterTests;

/// <summary>
/// Integration tests for TLio commands executed against a real YAML document.
/// Verifies that Set, Add, Put, Remove, Copy, and Move work end-to-end through
/// the YAML adapter layer.
/// </summary>
[TestFixture]
public class YamlCommandIntegrationTests
{
    private YamlMappingNode _data = null!;
    private IExecutionContext<YamlNode> _context = null!;

    [SetUp]
    public void SetUp()
    {
        _context = YamlExecutionContext.CreateDefault();
        var stream = new YamlStream();
        stream.Load(new StringReader(
            """
            name: Alice
            age: 30
            address:
              city: Amsterdam
              country: NL
            scores:
              - 10
              - 20
              - 30
            """));
        _data = (YamlMappingNode)stream.Documents[0].RootNode;
    }

    private YamlScalarNode? GetScalar(string path)
        => _context.ItemsFetcher.SelectNode(path, _data) as YamlScalarNode;

    private bool HasKey(YamlMappingNode map, string key)
        => map.Children.ContainsKey(new YamlScalarNode(key));

    // ── Set ───────────────────────────────────────────────────────────────────

    [Test]
    public void Set_ExistingPath_UpdatesValue()
    {
        var result = new Set<YamlNode>("$.name",
            new FixedValue<YamlNode>(new YamlScalarNode("Bob")))
            .Execute(_data, _context);

        Assert.That(result.Success, Is.True);
        Assert.That(GetScalar("$.name")?.Value, Is.EqualTo("Bob"));
    }

    [Test]
    public void Set_NestedPath_UpdatesDeepValue()
    {
        var result = new Set<YamlNode>("$.address.city",
            new FixedValue<YamlNode>(new YamlScalarNode("Rotterdam")))
            .Execute(_data, _context);

        Assert.That(result.Success, Is.True);
        Assert.That(GetScalar("$.address.city")?.Value, Is.EqualTo("Rotterdam"));
    }

    // ── Add ───────────────────────────────────────────────────────────────────

    [Test]
    public void Add_NewProperty_AddsKey()
    {
        var result = new Add<YamlNode>("$.phone",
            new FixedValue<YamlNode>(new YamlScalarNode("+31612345678")))
            .Execute(_data, _context);

        Assert.That(result.Success, Is.True);
        Assert.That(GetScalar("$.phone")?.Value, Is.EqualTo("+31612345678"));
    }

    [Test]
    public void Add_ToArray_AppendsElement()
    {
        // Use new-syntax Add (Property set) so the target is the sequence itself
        var scoresBefore = (_context.ItemsFetcher.SelectNode("$.scores", _data) as YamlSequenceNode)!.Children.Count;

        var result = new Add<YamlNode> { Path = "$.scores", Property = "item",
            Value = new FixedValue<YamlNode>(new YamlScalarNode("40")) }
            .Execute(_data, _context);

        Assert.That(result.Success, Is.True);
        var scoresAfter = (_context.ItemsFetcher.SelectNode("$.scores", _data) as YamlSequenceNode)!.Children.Count;
        Assert.That(scoresAfter, Is.EqualTo(scoresBefore + 1));
    }

    // ── Put ───────────────────────────────────────────────────────────────────

    [Test]
    public void Put_ExistingPath_UpdatesValue()
    {
        var result = new Put<YamlNode>("$.age",
            new FixedValue<YamlNode>(new YamlScalarNode("31")))
            .Execute(_data, _context);

        Assert.That(result.Success, Is.True);
        Assert.That(GetScalar("$.age")?.Value, Is.EqualTo("31"));
    }

    [Test]
    public void Put_NonExistingPath_AddsValue()
    {
        var result = new Put<YamlNode>("$.nickname",
            new FixedValue<YamlNode>(new YamlScalarNode("Ali")))
            .Execute(_data, _context);

        Assert.That(result.Success, Is.True);
        Assert.That(GetScalar("$.nickname")?.Value, Is.EqualTo("Ali"));
    }

    // ── Remove ────────────────────────────────────────────────────────────────

    [Test]
    public void Remove_ExistingPath_RemovesKey()
    {
        var result = new Remove<YamlNode>("$.age")
            .Execute(_data, _context);

        Assert.That(result.Success, Is.True);
        Assert.That(_fetcher.SelectNode("$.age", _data), Is.Null);
    }

    [Test]
    public void Remove_NestedPath_RemovesDeepKey()
    {
        var result = new Remove<YamlNode>("$.address.country")
            .Execute(_data, _context);

        Assert.That(result.Success, Is.True);
        var address = _context.ItemsFetcher.SelectNode("$.address", _data) as YamlMappingNode;
        Assert.That(HasKey(address!, "country"), Is.False);
        Assert.That(HasKey(address!, "city"), Is.True);
    }

    // ── Copy ──────────────────────────────────────────────────────────────────

    [Test]
    public void Copy_ExistingPath_CopiesValueToDestination()
    {
        var result = new Copy<YamlNode>("$.name", "$.nameCopy")
            .Execute(_data, _context);

        Assert.That(result.Success, Is.True);
        Assert.That(GetScalar("$.nameCopy")?.Value, Is.EqualTo("Alice"));
        Assert.That(GetScalar("$.name")?.Value, Is.EqualTo("Alice")); // source preserved
    }

    // ── Move ──────────────────────────────────────────────────────────────────

    [Test]
    public void Move_ExistingPath_MovesValueToDestination()
    {
        var result = new Move<YamlNode>("$.name", "$.fullName")
            .Execute(_data, _context);

        Assert.That(result.Success, Is.True);
        Assert.That(GetScalar("$.fullName")?.Value, Is.EqualTo("Alice"));
        Assert.That(_context.ItemsFetcher.SelectNode("$.name", _data), Is.Null); // source removed
    }

    // ── Private helper ────────────────────────────────────────────────────────

    private YamlPathItemsFetcher _fetcher => (YamlPathItemsFetcher)_context.ItemsFetcher;
}
