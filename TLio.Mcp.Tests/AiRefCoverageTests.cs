using Microsoft.Extensions.Options;
using Newtonsoft.Json.Linq;
using NUnit.Framework;
using TLio.Client;
using TLio.Extensions.Math;
using TLio.Extensions.Text;
using TLio.Extensions.TimeDate;
using TLio.Mcp.Configuration;
using TLio.Mcp.Services;

namespace TLio.Mcp.Tests;

/// <summary>
/// The ai-ref documents are how an agent discovers what TLio can do — the equivalent of
/// IntelliSense for tlio_list_functions / tlio_describe. A function that is registered but
/// undocumented is invisible there, so this fixture keeps the two in step.
///
/// It reads the repository's real docs/ai-ref, not a test fixture directory.
/// </summary>
[TestFixture]
public sealed class AiRefCoverageTests
{
    private AiRefReader _reader = null!;

    [SetUp]
    public void SetUp()
    {
        // No AiRefRoot configured → the reader walks up to the repository's docs/ai-ref.
        _reader = new AiRefReader(Options.Create(new McpConfiguration()));
    }

    private static IEnumerable<string> BuiltInFunctionNames() =>
        ParseOptions<JToken>.CreateDefault().FunctionsProvider.GetRegisteredFunctionNames();

    [Test]
    public void EveryBuiltInFunction_HasAnAiRefDocument()
    {
        var undocumented = BuiltInFunctionNames()
            .Where(name => _reader.GetContent(name, "function") is null)
            .OrderBy(n => n)
            .ToList();

        Assert.That(undocumented, Is.Empty,
            $"registered but undocumented in docs/ai-ref/functions: {string.Join(", ", undocumented)}");
    }

    [Test]
    public void PredicateDocuments_DescribeTheirIntent()
    {
        // The first "> …" line is what tlio_list_functions shows as the one-line intent.
        foreach (var name in new[]
                 {
                     "equals", "notEquals", "greaterThan", "greaterOrEqual", "lessThan", "lessOrEqual",
                     "and", "or", "not", "exists", "isNull", "isString", "isNumber", "isBoolean",
                     "isArray", "isObject", "in", "matches"
                 })
        {
            var content = _reader.GetContent(name, "function");
            Assert.That(content, Is.Not.Null, $"no document for '{name}'");
            Assert.That(content, Does.Contain("\n> "), $"'{name}' has no intent line");
            Assert.That(content, Does.Contain("## Syntax"), $"'{name}' has no syntax section");
        }
    }

    [Test]
    public void TextPackAdditions_AreDocumented()
    {
        var options = ParseOptions<JToken>.CreateDefault();
        options.FunctionsProvider.RegisterText<JToken>();

        Assert.That(options.FunctionsProvider.GetFunction("toFixed"), Is.Not.Null);
        Assert.That(_reader.GetContent("toFixed", "function"), Is.Not.Null);
    }

    /// <summary>
    /// The core-only check above (<see cref="EveryBuiltInFunction_HasAnAiRefDocument"/>) cannot
    /// see the Text/Math/TimeDate packs — they are not part of <c>CreateDefault()</c> and need
    /// their own registrar call. That gap is exactly how `subtract`, `calculate`, `sumifs`,
    /// `countifs`, `averageif`, `averageifs`, `minifs` and `maxifs` went undocumented for years:
    /// registered in the Math pack, invisible to the built-in-only coverage test.
    /// </summary>
    [Test]
    public void ExtensionPackFunctions_HaveAnAiRefDocument()
    {
        var options = ParseOptions<JToken>.CreateDefault();
        options.FunctionsProvider.RegisterText<JToken>();
        options.FunctionsProvider.RegisterMath<JToken>();
        options.FunctionsProvider.RegisterTimeDate<JToken>();

        var undocumented = options.FunctionsProvider.GetRegisteredFunctionNames()
            .Where(name => _reader.GetContent(name, "function") is null)
            .OrderBy(n => n)
            .ToList();

        Assert.That(undocumented, Is.Empty,
            $"registered but undocumented in docs/ai-ref/functions: {string.Join(", ", undocumented)}");
    }

    [Test]
    public void ListFunctions_IncludesThePredicates()
    {
        var listed = _reader.ListFunctions().Select(f => f.Name).ToList();

        Assert.That(listed, Does.Contain("Equals"));
        Assert.That(listed, Does.Contain("Matches"));
        Assert.That(listed, Does.Contain("ToFixed"));
    }
}
