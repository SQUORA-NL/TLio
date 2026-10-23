using System.Text.RegularExpressions;
using Newtonsoft.Json.Linq;
using NUnit.Framework;
using TLio.Client;
using TLio.Core.Models;
using TLio.Extensions.Looping;
using TLio.FormatConverter;
using TLio.FormatConverter.Json;
using TLio.FormatConverter.Xml;
using TLio.FormatConverter.Yaml;
using TLio.Json;
using TLio.Xml;
using TLio.Yaml;
using Converter = global::TLio.FormatConverter.Core.FormatConverter;

namespace TLio.Parity.Tests;

/// <summary>
/// The test that shows <c>with</c> and <c>resolve()</c> change how a script is <i>written</i> and
/// nothing about what it <i>does</i>.
///
/// <c>Equivalence/policy-mapping</c> is a JSON → SOAP-style XML mapping of the shape that
/// motivated both features: 69 commands (4 move, 54 add, 2 ifElse, 8 remove, 1 convert) in
/// which the same nested lookup —
/// <c>party[?(@.refKey == policy[0].party[?(@.entityType=='policyHolder')].partyRef[0])]</c> —
/// is repeated once per field, 13 fields for the policyholder and 13 for the insured person. The
/// data is invented and carries no payload bodies. <c>script.rewritten.json</c> is the same
/// mapping written with one <c>with</c> block per person and <c>resolve()</c> for the one-off
/// lookups. Both run through <see cref="MultiFormatScriptRunner"/> (json → convert xml) and must
/// produce byte-identical XML.
/// </summary>
[TestFixture]
public class WithResolveEquivalenceTests
{
    private const string NestedFilter = "partyRef[0])]";

    private static string Read(string file) => File.ReadAllText(Path.Combine(
        TestContext.CurrentContext.TestDirectory, "Equivalence", "policy-mapping", file));

    private static MultiFormatScriptRunner Runner()
    {
        var converter = new Converter();
        converter.Register(new JsonFormatAdapter());
        converter.Register(new XmlFormatAdapter());
        converter.Register(new YamlFormatAdapter());

        var runner = new MultiFormatScriptRunner(converter);
        runner.RegisterExecutor(Section("json", JsonExecutionContext.CreateDefault));
        runner.RegisterExecutor(Section("xml", XmlExecutionContext.CreateWithNativeXPath));
        runner.RegisterExecutor(Section("yaml", YamlExecutionContext.CreateDefault));
        return runner;

        static ScriptEngineSectionExecutor<T> Section<T>(string id, Func<ExecutionContext<T>> context)
        {
            var options = ParseOptions<T>.CreateDefault();
            options.CommandsProvider.RegisterLooping<T>();
            return new ScriptEngineSectionExecutor<T>(
                id, new ScriptEngine<T>(options.CommandsProvider, options.FunctionsProvider), () => context());
        }
    }

    private static string Run(string script)
    {
        var result = Runner().Run("json", Read("input.json"), script);
        // XmlWriter uses the OS newline; compare with it normalised.
        return result.Document.Replace("\r\n", "\n");
    }

    [Test]
    public void TheOriginalScript_IsTheSixtyNineCommandMapping()
    {
        var commands = JArray.Parse(Read("script.original.json"))
            .GroupBy(c => (string)c["command"]!).ToDictionary(g => g.Key, g => g.Count());

        Assert.That(commands, Is.EqualTo(new Dictionary<string, int>
            { ["add"] = 54, ["move"] = 4, ["ifElse"] = 2, ["remove"] = 8, ["convert"] = 1 }));
        Assert.That(Regex.Matches(Read("script.original.json"), Regex.Escape(NestedFilter)).Count,
            Is.EqualTo(26 + 2), "the lookup is written out for each of the 26 fields and 2 conditions");
    }

    [Test]
    public void TheRewrittenScript_StatesEachLookupOncePerPerson()
    {
        Assert.That(Regex.Matches(Read("script.rewritten.json"), Regex.Escape("\"command\": \"with\"")).Count, Is.EqualTo(2));
        Assert.That(Regex.Matches(Read("script.rewritten.json"), Regex.Escape(NestedFilter)).Count, Is.EqualTo(2));
        Assert.That(Read("script.rewritten.json"), Does.Contain("=resolve("));
    }

    [Test]
    public void OriginalAndRewritten_ProduceByteIdenticalXml()
    {
        var original = Run(Read("script.original.json"));
        var rewritten = Run(Read("script.rewritten.json"));

        Assert.That(rewritten, Is.EqualTo(original));
    }

    [Test]
    public void TheXml_ActuallyHoldsTheLookedUpValues()
    {
        // Guards against both forms agreeing on an empty or half-built document.
        var xml = Run(Read("script.rewritten.json"));

        Assert.That(xml, Does.Contain("<firstName>Grace</firstName>"), "policyholder is k2");
        Assert.That(xml, Does.Contain("<firstName>Ada</firstName>"), "insured person is k1");
        Assert.That(xml, Does.Contain("<city>Utrecht</city>").And.Contain("<city>Amsterdam</city>"));
        Assert.That(xml, Does.Contain("<title>Mrs</title>"));
        Assert.That(xml, Does.Contain("<beneficiaryName>Turing</beneficiaryName>"));
        Assert.That(xml, Does.Contain("<payerName>Dijkstra</payerName>"));
        // arrayHandling "repeated": the two coverage entries are sibling elements.
        Assert.That(Regex.Matches(xml, "<coverage>").Count, Is.EqualTo(2));
        Assert.That(xml, Does.Not.Contain("masterAgreement").And.Not.Contain("attachments"));
    }
}
