using NUnit.Framework;
using TLio.Client;
using TLio.Yaml;
using YamlDotNet.RepresentationModel;

namespace TLio.Yaml.Tests.Yaml;

/// <summary>
/// Data-driven YAML fixture tests.
/// Each fixture folder under Fixtures/Yaml&lt;Command&gt;/ contains:
///   input.yaml   — the YAML document to transform
///   script.yaml  — the TLio script in YAML format
///   result.yaml  — the expected result after execution
/// </summary>
[TestFixture]
public class YamlFixtureTests
{
    private YamlScriptParser<YamlNode> _parser = null!;
    private ParseOptions<YamlNode> _options = null!;

    [SetUp]
    public void SetUp()
    {
        var tracker = new YamlParentTracker();
        _options = ParseOptions<YamlNode>.CreateDefault();
        _parser  = new YamlScriptParser<YamlNode>(
            _options.CommandsProvider,
            _options.FunctionsProvider,
            new YamlNodeAdapter(tracker));
    }

    [TestCaseSource(typeof(YamlFixtureLoader), nameof(YamlFixtureLoader.Load), new object[] { "YamlSet" })]
    public void Set(YamlNode input, string script, YamlNode expected) => Run(input, script, expected);

    [TestCaseSource(typeof(YamlFixtureLoader), nameof(YamlFixtureLoader.Load), new object[] { "YamlAdd" })]
    public void Add(YamlNode input, string script, YamlNode expected) => Run(input, script, expected);

    [TestCaseSource(typeof(YamlFixtureLoader), nameof(YamlFixtureLoader.Load), new object[] { "YamlPut" })]
    public void Put(YamlNode input, string script, YamlNode expected) => Run(input, script, expected);

    [TestCaseSource(typeof(YamlFixtureLoader), nameof(YamlFixtureLoader.Load), new object[] { "YamlRemove" })]
    public void Remove(YamlNode input, string script, YamlNode expected) => Run(input, script, expected);

    [TestCaseSource(typeof(YamlFixtureLoader), nameof(YamlFixtureLoader.Load), new object[] { "YamlCopy" })]
    public void Copy(YamlNode input, string script, YamlNode expected) => Run(input, script, expected);

    [TestCaseSource(typeof(YamlFixtureLoader), nameof(YamlFixtureLoader.Load), new object[] { "YamlMove" })]
    public void Move(YamlNode input, string script, YamlNode expected) => Run(input, script, expected);

    private void Run(YamlNode input, string script, YamlNode expected)
    {
        var context = YamlExecutionContext.CreateDefault();
        var tLioScript = _parser.ParseScript(script);
        var result = tLioScript.Execute(input, context);

        Assert.That(result.Success, Is.True,
            $"Engine reported failure. Log:\n{string.Join("\n", context.GetLogEntries().Select(e => $"  [{e.Level}] {e.Group}: {e.Message}"))}");

        var adapter = new YamlNodeAdapter(new YamlParentTracker());
        Assert.That(adapter.DeepEquals(result.Data, expected), Is.True,
            $"Result mismatch.\n  Expected: {expected}\n  Actual:   {result.Data}");
    }
}
