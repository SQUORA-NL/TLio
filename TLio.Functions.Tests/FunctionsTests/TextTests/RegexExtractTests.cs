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
public class RegexExtractTests
{
    private JToken data = null!;
    private TLio.Core.Contracts.IExecutionContext<JToken> context = null!;

    [SetUp]
    public void Setup()
    {
        context = JsonExecutionContext.CreateDefault();
        data = JToken.Parse(@"{
            ""iban"": ""NL91ABNA0417164300"",
            ""postcode"": ""1234 AB"",
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
        var fn = new RegexExtract<JToken>();
        fn.SetArguments(Args(args));
        return fn.Execute(data, data, context);
    }

    [Test]
    public void RegexExtract_NoGroupArgument_ReturnsWholeMatch()
    {
        var result = Run(
            new PathValue<JToken>("$.iban"),
            new FixedValue<JToken>(new JValue(@"[A-Z]{4}")));
        Assert.That(result.Success, Is.True);
        Assert.That(result.Data.First!.Value<string>(), Is.EqualTo("ABNA"));
    }

    [Test]
    public void RegexExtract_GroupIndex_ReturnsThatCapture()
    {
        var result = Run(
            new PathValue<JToken>("$.postcode"),
            new FixedValue<JToken>(new JValue(@"^(\d{4})\s*([A-Z]{2})$")),
            new FixedValue<JToken>(new JValue(2)));
        Assert.That(result.Success, Is.True);
        Assert.That(result.Data.First!.Value<string>(), Is.EqualTo("AB"));
    }

    [Test]
    public void RegexExtract_GroupZero_IsTheWholeMatch()
    {
        var result = Run(
            new PathValue<JToken>("$.postcode"),
            new FixedValue<JToken>(new JValue(@"(\d{4})")),
            new FixedValue<JToken>(new JValue(0)));
        Assert.That(result.Success, Is.True);
        Assert.That(result.Data.First!.Value<string>(), Is.EqualTo("1234"));
    }

    [Test]
    public void RegexExtract_NoMatch_ReturnsEmptyStringAndSucceeds()
    {
        // Deliberate: an absent match is an answer, not a script-aborting failure.
        var result = Run(
            new PathValue<JToken>("$.postcode"),
            new FixedValue<JToken>(new JValue(@"^\d{9}$")));
        Assert.That(result.Success, Is.True);
        Assert.That(result.Data.First!.Value<string>(), Is.EqualTo(string.Empty));
        Assert.That(context.GetLogEntries().Any(e => e.Level == LogLevel.Error), Is.False);
    }

    [Test]
    public void RegexExtract_GroupThePatternDoesNotHave_FailsAndLogsError()
    {
        var result = Run(
            new PathValue<JToken>("$.postcode"),
            new FixedValue<JToken>(new JValue(@"^(\d{4})")),
            new FixedValue<JToken>(new JValue(5)));
        Assert.That(result.Success, Is.False);
        Assert.That(context.GetLogEntries().Any(e => e.Level == LogLevel.Error), Is.True);
    }

    [Test]
    public void RegexExtract_MissingGroup_FailsEvenWhenNothingMatches()
    {
        // The authoring error is reported against the pattern, not against the match.
        var result = Run(
            new PathValue<JToken>("$.postcode"),
            new FixedValue<JToken>(new JValue(@"^(zzz)$")),
            new FixedValue<JToken>(new JValue(9)));
        Assert.That(result.Success, Is.False);
    }

    [Test]
    public void RegexExtract_TooFewArguments_FailsAndWarns()
    {
        var result = Run(new PathValue<JToken>("$.iban"));
        Assert.That(result.Success, Is.False);
        Assert.That(context.GetLogEntries().Any(e => e.Level == LogLevel.Warning), Is.True);
    }

    [Test]
    public void RegexExtract_PathNotFound_Fails()
    {
        var result = Run(
            new PathValue<JToken>("$.missing"),
            new FixedValue<JToken>(new JValue(@"\d")));
        Assert.That(result.Success, Is.False);
    }

    [Test]
    public void RegexExtract_FoundButNull_IsEmptyString()
    {
        var result = Run(
            new PathValue<JToken>("$.nothing"),
            new FixedValue<JToken>(new JValue(@"\d")));
        Assert.That(result.Success, Is.True);
        Assert.That(result.Data.First!.Value<string>(), Is.EqualTo(string.Empty));
    }

    [Test]
    public void RegexExtract_ThroughAScript_QuotedPatternWithBracesSurvivesArgumentSplitting()
    {
        // '{2,4}' contains a comma: only the quoting keeps it inside one argument.
        var options = ParseOptions<JToken>.CreateDefault();
        options.FunctionsProvider.RegisterText<JToken>();
        var engine = new ScriptEngine<JToken>(options.CommandsProvider, options.FunctionsProvider);
        var document = JToken.Parse(@"{ ""iban"": ""NL91ABNA0417164300"" }");

        var result = engine.Execute(
            @"[{ ""command"": ""put"", ""path"": ""$.bank"",
                 ""value"": ""=regexExtract($.iban, '[A-Z]{2,4}(?=\\d{4})')"" }]",
            document, JsonExecutionContext.CreateDefault());

        Assert.That(result.Success, Is.True);
        Assert.That(result.Data!.SelectToken("$.bank")!.Value<string>(), Is.EqualTo("ABNA"));
    }

    [Test]
    public void RegexExtract_InvalidPattern_FailsAndLogsErrorNamingThePattern()
    {
        var result = Run(
            new PathValue<JToken>("$.iban"),
            new FixedValue<JToken>(new JValue("([unclosed")));
        Assert.That(result.Success, Is.False);
        Assert.That(
            context.GetLogEntries().Any(e => e.Level == LogLevel.Error && e.Message.Contains("([unclosed")),
            Is.True);
    }
}
