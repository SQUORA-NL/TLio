using Microsoft.Extensions.Logging;
using Newtonsoft.Json.Linq;
using NUnit.Framework;
using TLio.Client;
using TLio.Core.Contracts;
using TLio.Core.Models;
using TLio.Extensions.Text;
using TLio.Json;

namespace TLio.Functions.Tests.FunctionsTests.TextTests;

[TestFixture]
public class RegexReplaceTests
{
    private JToken data = null!;
    private TLio.Core.Contracts.IExecutionContext<JToken> context = null!;

    [SetUp]
    public void Setup()
    {
        context = JsonExecutionContext.CreateDefault();
        data = JToken.Parse(@"{
            ""postcode"": ""1234ab"",
            ""plate"": ""XX-99-YY"",
            ""repl"": ""#"",
            ""nothing"": null
        }");
    }

    private static Arguments<JToken> Args(params IFunctionSupportedValue<JToken>[] args)
    {
        var result = new Arguments<JToken>();
        foreach (var a in args) result.Add(a);
        return result;
    }

    private FunctionResult<JToken> Run(params IFunctionSupportedValue<JToken>[] args)
    {
        var fn = new RegexReplace<JToken>();
        fn.SetArguments(Args(args));
        return fn.Execute(data, data, context);
    }

    [Test]
    public void RegexReplace_ReplacesEveryMatch()
    {
        var result = Run(
            new PathValue<JToken>("$.plate"),
            new FixedValue<JToken>(new JValue("-")),
            new FixedValue<JToken>(new JValue("")));
        Assert.That(result.Success, Is.True);
        Assert.That(result.Data.First!.Value<string>(), Is.EqualTo("XX99YY"));
    }

    [Test]
    public void RegexReplace_BackreferenceInReplacement_IsExpanded()
    {
        var result = Run(
            new PathValue<JToken>("$.postcode"),
            new FixedValue<JToken>(new JValue(@"^(\d{4})([a-zA-Z]{2})$")),
            new FixedValue<JToken>(new JValue("$1 $2")));
        Assert.That(result.Success, Is.True);
        Assert.That(result.Data.First!.Value<string>(), Is.EqualTo("1234 ab"));
    }

    [Test]
    public void RegexReplace_NoMatch_ReturnsInputUnchanged()
    {
        var result = Run(
            new PathValue<JToken>("$.postcode"),
            new FixedValue<JToken>(new JValue(@"^\d{6}$")),
            new FixedValue<JToken>(new JValue("X")));
        Assert.That(result.Success, Is.True);
        Assert.That(result.Data.First!.Value<string>(), Is.EqualTo("1234ab"));
    }

    [Test]
    public void RegexReplace_TooFewArguments_FailsAndWarns()
    {
        var result = Run(
            new PathValue<JToken>("$.postcode"),
            new FixedValue<JToken>(new JValue(@"\d")));
        Assert.That(result.Success, Is.False);
        Assert.That(context.GetLogEntries().Any(e => e.Level == LogLevel.Warning), Is.True);
    }

    [Test]
    public void RegexReplace_PathNotFound_Fails()
    {
        var result = Run(
            new PathValue<JToken>("$.missing"),
            new FixedValue<JToken>(new JValue(@"\d")),
            new FixedValue<JToken>(new JValue("X")));
        Assert.That(result.Success, Is.False);
    }

    [Test]
    public void RegexReplace_FoundButNull_IsEmptyString()
    {
        var result = Run(
            new PathValue<JToken>("$.nothing"),
            new FixedValue<JToken>(new JValue(@"\d")),
            new FixedValue<JToken>(new JValue("X")));
        Assert.That(result.Success, Is.True);
        Assert.That(result.Data.First!.Value<string>(), Is.EqualTo(string.Empty));
    }

    [Test]
    public void RegexReplace_InvalidPattern_FailsAndLogsErrorNamingThePattern()
    {
        var result = Run(
            new PathValue<JToken>("$.postcode"),
            new FixedValue<JToken>(new JValue("([unclosed")),
            new FixedValue<JToken>(new JValue("X")));
        Assert.That(result.Success, Is.False);
        Assert.That(
            context.GetLogEntries().Any(e => e.Level == LogLevel.Error && e.Message.Contains("([unclosed")),
            Is.True);
    }

    [Test]
    public void RegexReplace_ReplacementGivenAsAPath_StillResolves()
    {
        // The '$1' guard must not stop a genuine '$.' path argument from resolving.
        var result = Run(
            new PathValue<JToken>("$.plate"),
            new FixedValue<JToken>(new JValue("-")),
            new FixedValue<JToken>(new JValue("$.repl")));
        Assert.That(result.Success, Is.True);
        Assert.That(result.Data.First!.Value<string>(), Is.EqualTo("XX#99#YY"));
    }

    [Test]
    public void RegexReplace_ThroughAScript_BackreferencesSurviveTheParser()
    {
        // The whole round trip: quoted pattern, quoted '$1 $2' replacement, script notation.
        var options = ParseOptions<JToken>.CreateDefault();
        options.FunctionsProvider.RegisterText<JToken>();
        var engine = new ScriptEngine<JToken>(options.CommandsProvider, options.FunctionsProvider);
        var document = JToken.Parse(@"{ ""postcode"": ""1234ab"" }");

        var result = engine.Execute(
            @"[{ ""command"": ""put"", ""path"": ""$.formatted"",
                 ""value"": ""=regexReplace($.postcode, '^(\\d{4})([a-zA-Z]{2})$', '$1 $2')"" }]",
            document, JsonExecutionContext.CreateDefault());

        Assert.That(result.Success, Is.True);
        Assert.That(result.Data!.SelectToken("$.formatted")!.Value<string>(), Is.EqualTo("1234 ab"));
    }

    [Test]
    public void RegexReplace_UnanchoredPattern_MatchesAnywhere()
    {
        // The trap: without ^ and $ the pattern rewrites every occurrence inside the value.
        var result = Run(
            new FixedValue<JToken>(new JValue("ab-ab")),
            new FixedValue<JToken>(new JValue("ab")),
            new FixedValue<JToken>(new JValue("Z")));
        Assert.That(result.Success, Is.True);
        Assert.That(result.Data.First!.Value<string>(), Is.EqualTo("Z-Z"));
    }
}
