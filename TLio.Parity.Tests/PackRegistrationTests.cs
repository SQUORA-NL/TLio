using System.Xml.Linq;
using Newtonsoft.Json.Linq;
using NUnit.Framework;
using TLio.Client;
using TLio.Extensions.ETL;
using TLio.Extensions.Looping;
using TLio.Extensions.Math;
using TLio.Extensions.Text;
using TLio.Extensions.TimeDate;
using TLio.FormatConverter;
using TLio.FormatConverter.Json;
using TLio.FormatConverter.Xml;
using TLio.FormatConverter.Yaml;
using TLio.Json;
using TLio.Xml;
using TLio.Yaml;
using YamlDotNet.RepresentationModel;
using Converter = global::TLio.FormatConverter.Core.FormatConverter;

namespace TLio.Parity.Tests;

/// <summary>
/// One call per pack: <c>WithMath()</c>, <c>WithText()</c>, <c>WithTimeDate()</c>, <c>WithEtl()</c>,
/// <c>WithLooping()</c> and <c>WithFormatConversion(converter, id)</c> register exactly what the
/// older <c>Register*</c> methods register. The expected names are read from a provider the old
/// methods filled, never written down here, so the two cannot drift apart.
/// <para>
/// Origin: a host that registered every pack but <c>FormatConversion</c> ran
/// <c>convertValue</c> as an unknown command — a warning, and a "successful" run that changed
/// nothing.
/// </para>
/// </summary>
[TestFixture]
public class PackRegistrationTests
{
    private static Converter NewConverter()
    {
        var converter = new Converter();
        converter.Register(new JsonFormatAdapter());
        converter.Register(new XmlFormatAdapter());
        converter.Register(new YamlFormatAdapter());
        return converter;
    }

    private static (string[] Commands, string[] Functions) Names(ParseOptions<JToken> o) =>
        (o.CommandsProvider.GetRegisteredCommandNames().OrderBy(n => n).ToArray(),
         o.FunctionsProvider.GetRegisteredFunctionNames().OrderBy(n => n).ToArray());

    private static void AssertSame(ParseOptions<JToken> viaWith, ParseOptions<JToken> viaRegister, string pack)
    {
        var actual = Names(viaWith);
        var expected = Names(viaRegister);
        Assert.That(actual.Commands, Is.EqualTo(expected.Commands), $"{pack}: commands");
        Assert.That(actual.Functions, Is.EqualTo(expected.Functions), $"{pack}: functions");
    }

    private static ParseOptions<JToken> Baseline() => ParseOptions<JToken>.CreateDefault();

    [Test]
    public void WithMath_RegistersWhatRegisterMathRegisters()
    {
        var old = Baseline();
        old.FunctionsProvider.RegisterMath<JToken>();
        AssertSame(Baseline().WithMath(), old, "Math");
        Assert.That(Names(old).Functions, Has.Member("sum"));
    }

    [Test]
    public void WithText_RegistersWhatRegisterTextRegisters()
    {
        var old = Baseline();
        old.FunctionsProvider.RegisterText<JToken>();
        AssertSame(Baseline().WithText(), old, "Text");
    }

    [Test]
    public void WithTimeDate_RegistersWhatRegisterTimeDateRegisters()
    {
        var old = Baseline();
        old.FunctionsProvider.RegisterTimeDate<JToken>();
        AssertSame(Baseline().WithTimeDate(), old, "TimeDate");
    }

    [Test]
    public void WithEtl_RegistersWhatRegisterEtlRegisters()
    {
        var old = Baseline();
        old.CommandsProvider.RegisterETL<JToken>();
        AssertSame(Baseline().WithEtl(), old, "ETL");
    }

    [Test]
    public void WithLooping_RegistersWhatRegisterLoopingRegisters()
    {
        var old = Baseline();
        old.CommandsProvider.RegisterLooping<JToken>();
        AssertSame(Baseline().WithLooping(), old, "Looping");
    }

    [Test]
    public void WithFormatConversion_RegistersWhatRegisterFormatConversionRegisters()
    {
        var converter = NewConverter();
        var old = Baseline();
        old.CommandsProvider.RegisterFormatConversion<JToken>(converter, "json");
        AssertSame(Baseline().WithFormatConversion(converter, "json"), old, "FormatConversion");
        Assert.That(Names(old).Commands, Has.Member("convertValue"));
    }

    [Test]
    public void EveryPack_AddsSomethingNew()
    {
        // Guards the guard: if a With* silently registered nothing, the equivalence tests above
        // would still pass (both sides empty).
        var bare = Names(Baseline());
        var converter = NewConverter();
        Assert.That(Names(Baseline().WithMath()).Functions.Length, Is.GreaterThan(bare.Functions.Length));
        Assert.That(Names(Baseline().WithText()).Functions.Length, Is.GreaterThan(bare.Functions.Length));
        Assert.That(Names(Baseline().WithTimeDate()).Functions.Length, Is.GreaterThan(bare.Functions.Length));
        Assert.That(Names(Baseline().WithEtl()).Commands.Length, Is.GreaterThan(bare.Commands.Length));
        Assert.That(Names(Baseline().WithLooping()).Commands.Length, Is.GreaterThan(bare.Commands.Length));
        Assert.That(Names(Baseline().WithFormatConversion(converter, "json")).Commands.Length,
            Is.GreaterThan(bare.Commands.Length));
    }

    [Test]
    public void Calls_Chain_AndKeepTheConcreteType()
    {
        ParseOptions<JToken> options = ParseOptions<JToken>.CreateDefault()
            .WithMath().WithText().WithTimeDate().WithEtl().WithLooping()
            .WithFormatConversion(NewConverter(), "json");
        Assert.That(options.FunctionsProvider.GetFunction("sum"), Is.Not.Null);
        Assert.That(options.CommandsProvider.GetCommand("forEach"), Is.Not.Null);
    }

    [Test]
    public void WithFormatConversion_RequiresAConverterAndAFormatId()
    {
        Assert.Throws<ArgumentNullException>(() => Baseline().WithFormatConversion(null!, "json"));
        Assert.Throws<ArgumentException>(() => Baseline().WithFormatConversion(NewConverter(), ""));
        Assert.Throws<ArgumentNullException>(() => Baseline().WithFormatConversion(NewConverter(), null!));
    }

    private const string ConvertValueScript = """[{"command":"convertValue","path":"$.input","to":"xml"}]""";

    [Test]
    public void Json_ConvertValue_RunsOnAnEngineBuiltWithTheNewCall()
    {
        var options = ParseOptions<JToken>.CreateDefault().WithFormatConversion(NewConverter(), "json");
        var engine = new ScriptEngine<JToken>(options.CommandsProvider, options.FunctionsProvider);
        var result = engine.Execute(ConvertValueScript, JToken.Parse("""{"input":{"field":1}}"""),
            JsonExecutionContext.CreateDefault());
        Assert.That(result.Success, Is.True);
        Assert.That(result.Data!.ToString(Newtonsoft.Json.Formatting.None),
            Is.EqualTo("""{"input":"<input><field>1</field></input>"}"""));
    }

    [Test]
    public void Xml_ConvertValue_RunsOnAnEngineBuiltWithTheNewCall()
    {
        var options = ParseOptions<XElement>.CreateDefault().WithFormatConversion(NewConverter(), "xml");
        var engine = new ScriptEngine<XElement>(options.CommandsProvider, options.FunctionsProvider);
        var doc = new XmlNodeAdapter().Parse("<root><input><field>1</field></input></root>");
        var script = """[{"command":"convertValue","path":"/root/input","to":"json"}]""";
        var result = engine.Execute(script, doc, XmlExecutionContext.CreateWithSlashPaths());
        Assert.That(result.Success, Is.True);
        Assert.That(result.Data!.Element("input")!.Value, Does.Contain("field"));
    }

    [Test]
    public void Yaml_ConvertValue_RunsOnAnEngineBuiltWithTheNewCall()
    {
        var options = ParseOptions<YamlNode>.CreateDefault().WithFormatConversion(NewConverter(), "yaml");
        var engine = new ScriptEngine<YamlNode>(options.CommandsProvider, options.FunctionsProvider);
        var context = YamlExecutionContext.CreateDefault();
        var doc = context.NodeAdapter.Parse("input:\n  field: 1\n");
        var result = engine.Execute(ConvertValueScript, doc, context);
        Assert.That(result.Success, Is.True);
        var text = FormatRunners.RenderYamlForComparison(result.Data!);
        Assert.That(text, Does.Contain("<input>").And.Contain("<field>1</field>"));
    }
}
