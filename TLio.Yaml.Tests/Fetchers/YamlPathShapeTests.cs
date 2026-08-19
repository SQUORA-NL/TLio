using NUnit.Framework;
using TLio.Client;
using TLio.Commands;
using TLio.Core.Contracts;
using YamlDotNet.RepresentationModel;

namespace TLio.Yaml.Tests.Fetchers;

/// <summary>
/// Path forms the YAML fetcher used to answer differently from the JSON one, so a script that
/// worked against JSON silently changed nothing — or the wrong thing — against YAML.
/// </summary>
[TestFixture]
public class YamlPathShapeTests
{
    private IExecutionContext<YamlNode> _context = null!;

    [SetUp]
    public void SetUp() => _context = YamlExecutionContext.CreateDefault();

    private YamlNode Parse(string yaml) => _context.NodeAdapter.Parse(yaml);

    /// <summary>Serialised without YamlDotNet's trailing "..." document-end marker.</summary>
    private string Serialize(YamlNode node) =>
        _context.NodeAdapter.Serialize(node).Replace("...", "").Trim();

    // ── Recursive descent ─────────────────────────────────────────────────────

    [Test]
    public void RecursiveDescent_FindsAKeyAtAnyDepth()
    {
        // "$..key" parsed as the plain key "key" on the root, matched nothing, and the command
        // reported "not found" instead of touching every match.
        var data = Parse("a:\n  score: 1\nb:\n  score: 2\n");

        var found = _context.ItemsFetcher.SelectNodes("$..score", data);

        Assert.That(found.Count, Is.EqualTo(2));
    }

    [Test]
    public void RecursiveDescent_ReachesThroughSequences()
    {
        var data = Parse("items:\n  - n: 1\n  - n: 2\n");

        Assert.That(_context.ItemsFetcher.SelectNodes("$..n", data).Count, Is.EqualTo(2));
    }

    [Test]
    public void RecursiveDescent_CanBeAnchored()
    {
        var data = Parse("a:\n  score: 1\nb:\n  score: 2\n");

        Assert.That(_context.ItemsFetcher.SelectNodes("$.a..score", data).Count, Is.EqualTo(1));
    }

    [Test]
    public void SetOnARecursiveDescentPath_UpdatesEveryMatch()
    {
        var data = Parse("a:\n  score: 1\nb:\n  score: 2\n");

        new Set<YamlNode>("$..score", Value("0")).Execute(data, _context);

        Assert.That(Serialize(data), Is.EqualTo("a:\n  score: 0\nb:\n  score: 0"));
    }

    [Test]
    public void RemoveOnARecursiveDescentPath_RemovesEveryMatch()
    {
        var data = Parse("a:\n  temp: 1\n  keep: 1\nb:\n  temp: 2\n");

        new Remove<YamlNode>("$..temp").Execute(data, _context);

        Assert.That(Serialize(data), Is.EqualTo("a:\n  keep: 1\nb: {}"));
    }

    // ── Wildcards in the parent of a path ─────────────────────────────────────

    [Test]
    public void SplitParentAndLeaf_KeepsAWildcardSegment()
    {
        // A wildcard was written back out as a plain key, producing "$.items." — a path that
        // resolves to the sequence itself, so a write aimed at each element hit the array.
        var (parent, leaf) = _context.ItemsFetcher.SplitParentAndLeaf("$.items[*].n");

        Assert.That(parent, Is.EqualTo("$.items[*]"));
        Assert.That(leaf, Is.EqualTo("n"));
    }

    [Test]
    public void SplitParentAndLeaf_KeepsAnIndexSegment()
    {
        var (parent, leaf) = _context.ItemsFetcher.SplitParentAndLeaf("$.items[0].n");

        Assert.That(parent, Is.EqualTo("$.items[0]"));
        Assert.That(leaf, Is.EqualTo("n"));
    }

    [Test]
    public void SetThroughAWildcard_UpdatesEveryElement()
    {
        var data = Parse("items:\n  - n: 1\n  - n: 2\n");

        new Set<YamlNode>("$.items[*].n", Value("9")).Execute(data, _context);

        Assert.That(Serialize(data), Is.EqualTo("items:\n- n: 9\n- n: 9"));
    }

    // ── Path construction ─────────────────────────────────────────────────────

    [Test]
    public void EnsurePath_BuildsNothingAlongAWildcardOrDescent()
    {
        // Those paths describe nodes that already exist; inventing a key named "[*]" or the
        // descent marker would corrupt the document.
        var data = Parse("a: 1\n");

        _context.ItemsFetcher.EnsurePath("$.items[*].n", data, _context.NodeAdapter);
        _context.ItemsFetcher.EnsurePath("$..n", data, _context.NodeAdapter);

        Assert.That(Serialize(data), Is.EqualTo("a: 1"));
    }

    private IFunctionSupportedValue<YamlNode> Value(string text) =>
        new TLio.Core.Models.FixedValue<YamlNode>(_context.NodeAdapter.CreateString(text));
}
