using NUnit.Framework;
using TLio.Client;
using TLio.Commands.Advanced;
using TLio.Core.Contracts;
using YamlDotNet.RepresentationModel;

namespace TLio.Yaml.Tests.Yaml;

/// <summary>
/// The YAML script notation describes the same commands the JSON notation does. Enums and
/// settings objects had no conversion, so a merge that declared array key paths parsed into a
/// merge with none, and a null value became the four-letter string that spells it.
/// </summary>
[TestFixture]
public class YamlScriptNotationTests
{
    private YamlScriptParser<YamlNode> _parser = null!;
    private IExecutionContext<YamlNode> _context = null!;

    [SetUp]
    public void SetUp()
    {
        var options = ParseOptions<YamlNode>.CreateDefault();
        _context = YamlExecutionContext.CreateDefault();
        _parser  = new YamlScriptParser<YamlNode>(
            options.CommandsProvider, options.FunctionsProvider, _context.NodeAdapter);
    }

    private ICommand<YamlNode> First(string script) => _parser.ParseScript(script).First();

    [Test]
    public void AnEnumValue_ReachesTheCommand()
    {
        var merge = (Merge<YamlNode>)First(
            "- command: merge\n  path: $.s\n  targetPath: $.t\n  arrayMergeMode: replace\n");

        Assert.That(merge.ArrayMergeMode, Is.EqualTo(ArrayMergeMode.Replace));
    }

    [Test]
    public void ASettingsMapping_IsReadWithTheSameFieldNamesAsJson()
    {
        var merge = (Merge<YamlNode>)First(
            """
            - command: merge
              path: $.source
              targetPath: $.target
              settings:
                arraySettings:
                  - arrayPath: $.target.items
                    keyPaths:
                      - id
            """);

        Assert.That(merge.Settings, Is.Not.Null);
        Assert.That(merge.Settings!.ArraySettings, Has.Count.EqualTo(1));
        Assert.That(merge.Settings.ArraySettings[0].ArrayPath, Is.EqualTo("$.target.items"));
        Assert.That(merge.Settings.ArraySettings[0].KeyPaths, Is.EqualTo(new[] { "id" }));
    }

    [Test]
    public void APlainNullValue_IsNull()
    {
        var data = _context.NodeAdapter.Parse("a: 1\n");

        _parser.ParseScript("- command: set\n  path: $.a\n  value: null\n").Execute(data, _context);

        var written = _context.ItemsFetcher.SelectNode("$.a", data)!;
        Assert.That(_context.NodeAdapter.IsNull(written), Is.True);
    }

    [Test]
    public void AQuotedNullValue_IsParsedAsTheString()
    {
        // Quoting is how YAML says "the text, not the null", and the parser honours it: the
        // value written is the four characters rather than a null.
        //
        // It stops there, though. YamlNodeAdapter.IsNull tests the text, not the scalar style,
        // so a property holding the literal string "null" still reads as null to every function
        // that asks. Telling the two apart end to end needs a styled scalar, which is a change
        // to the adapter's value model rather than to the notation — recorded in
        // docs/behaviour-decisions.md.
        var data = _context.NodeAdapter.Parse("a: 1\n");

        _parser.ParseScript("- command: set\n  path: $.a\n  value: 'null'\n").Execute(data, _context);

        var written = (YamlScalarNode)_context.ItemsFetcher.SelectNode("$.a", data)!;
        Assert.That(written.Value, Is.EqualTo("null"));
    }
}
