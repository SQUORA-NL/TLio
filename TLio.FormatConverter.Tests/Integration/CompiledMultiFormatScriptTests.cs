using Newtonsoft.Json.Linq;
using NUnit.Framework;
using TLio.Client;
using TLio.FormatConverter.Json;
using TLio.FormatConverter.Xml;
using TLio.FormatConverter.Yaml;
using TLio.Json;
using Converter = global::TLio.FormatConverter.Core.FormatConverter;

namespace TLio.FormatConverter.Tests.Integration;

[TestFixture]
public sealed class CompiledMultiFormatScriptTests
{
    private MultiFormatScriptRunner _runner = null!;

    [SetUp]
    public void SetUp()
    {
        var converter = new Converter();
        converter.Register(new JsonFormatAdapter());
        converter.Register(new XmlFormatAdapter());
        converter.Register(new YamlFormatAdapter());
        _runner = new MultiFormatScriptRunner(converter);

        var jsonOptions = ParseOptions<JToken>.CreateDefault();
        var jsonEngine = new ScriptEngine<JToken>(jsonOptions.CommandsProvider, jsonOptions.FunctionsProvider);
        _runner.RegisterExecutor(new ScriptEngineSectionExecutor<JToken>(
            "json", jsonEngine, JsonExecutionContext.CreateDefault));
    }

    [Test]
    public void Compile_ThenRun_MatchesRun_AcrossAFormatBoundary()
    {
        var script = """[{"command":"convert","to":"json"},{"command":"add","path":"$.flag","value":true}]""";
        var input = "<root><key>value</key></root>";

        var direct = _runner.Execute("xml", input, script);
        var compiled = _runner.Compile("xml", script).Run(input);

        Assert.That(compiled.Document, Is.EqualTo(direct));
        Assert.That(compiled.Success, Is.True);
    }

    [Test]
    public void Compile_ThenRun_Repeatedly_ProducesIndependentResultsPerInput()
    {
        var script = """[{"command":"add","path":"$.seen","value":true}]""";
        var compiled = _runner.Compile("json", script);

        var first = compiled.Run("""{"id":1}""");
        var second = compiled.Run("""{"id":2}""");

        Assert.That(first.Document, Does.Contain("\"id\":1"));
        Assert.That(second.Document, Does.Contain("\"id\":2"));
        Assert.That(first.Document, Does.Contain("\"seen\":true"));
        Assert.That(second.Document, Does.Contain("\"seen\":true"));
    }

    [Test]
    public void Compile_NoConvertCommands_ExecutesAsSingleSection()
    {
        var script = "[]";
        var input = "{\"key\":\"value\"}";

        var result = _runner.Compile("json", script).Run(input);

        Assert.That(result.Document.Replace(" ", "").Replace("\n", ""), Is.EqualTo(input));
    }

    [Test]
    public void Compile_SectionWithNoRegisteredExecutor_ThrowsAtCompileTime()
    {
        // "yaml" has a converter adapter but no registered ScriptEngineSectionExecutor in this
        // fixture, so a section with commands in it cannot be precompiled — and should fail at
        // Compile() rather than silently dropping the commands at Run() time.
        var script = "[{\"command\":\"convert\",\"to\":\"yaml\"},{\"command\":\"add\",\"path\":\"$.x\",\"value\":1}]";

        Assert.Throws<TLio.FormatConverter.Core.Exceptions.SectionExecutorNotRegisteredException>(
            () => _runner.Compile("xml", script));
    }
}
