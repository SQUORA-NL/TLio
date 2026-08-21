using Microsoft.Extensions.Logging;
using Newtonsoft.Json.Linq;
using NUnit.Framework;
using TLio.Client;
using TLio.Core.Contracts;
using TLio.Core.Models;
using TLio.Extensions.Math;
using TLio.Extensions.Text;
using TLio.Json;

namespace TLio.Functions.Tests.FunctionsTests.TextTests;

[TestFixture]
public class RightTests
{
    private JToken data = null!;
    private TLio.Core.Contracts.IExecutionContext<JToken> context = null!;

    [SetUp]
    public void Setup()
    {
        context = JsonExecutionContext.CreateDefault();
        data = JToken.Parse(@"{ ""str"": ""Hello World"", ""n"": 5, ""nothing"": null }");
    }

    private static Arguments<JToken> Args(params IFunctionSupportedValue<JToken>[] args)
    {
        var result = new Arguments<JToken>();
        foreach (var a in args) result.Add(a);
        return result;
    }

    private FunctionResult<JToken> Run(params IFunctionSupportedValue<JToken>[] args)
    {
        var fn = new Right<JToken>();
        fn.SetArguments(Args(args));
        return fn.Execute(data, data, context);
    }

    [Test]
    public void Right_ReturnsLastCharacters()
    {
        var result = Run(new PathValue<JToken>("$.str"), new PathValue<JToken>("$.n"));
        Assert.That(result.Success, Is.True);
        Assert.That(result.Data.First!.Value<string>(), Is.EqualTo("World"));
    }

    [Test]
    public void Right_CountAboveLength_ReturnsWholeString()
    {
        var result = Run(new PathValue<JToken>("$.str"), new FixedValue<JToken>(new JValue(999)));
        Assert.That(result.Success, Is.True);
        Assert.That(result.Data.First!.Value<string>(), Is.EqualTo("Hello World"));
    }

    [Test]
    public void Right_CountEqualToLength_ReturnsWholeString()
    {
        var result = Run(new PathValue<JToken>("$.str"), new FixedValue<JToken>(new JValue(11)));
        Assert.That(result.Success, Is.True);
        Assert.That(result.Data.First!.Value<string>(), Is.EqualTo("Hello World"));
    }

    [Test]
    public void Right_ZeroCount_ReturnsEmptyString()
    {
        var result = Run(new PathValue<JToken>("$.str"), new FixedValue<JToken>(new JValue(0)));
        Assert.That(result.Success, Is.True);
        Assert.That(result.Data.First!.Value<string>(), Is.EqualTo(string.Empty));
    }

    [Test]
    public void Right_NegativeCount_ReturnsEmptyString()
    {
        var result = Run(new PathValue<JToken>("$.str"), new FixedValue<JToken>(new JValue(-3)));
        Assert.That(result.Success, Is.True);
        Assert.That(result.Data.First!.Value<string>(), Is.EqualTo(string.Empty));
    }

    [Test]
    public void Right_NumericSource_IsReadAsTextAndKeepsLeadingZero()
    {
        var result = Run(new FixedValue<JToken>(new JValue(20260818)), new FixedValue<JToken>(new JValue(4)));
        Assert.That(result.Success, Is.True);
        Assert.That(result.Data.First!.Value<string>(), Is.EqualTo("0818"));
    }

    [Test]
    public void Right_ThroughAScript_MatchesTheSubstringArithmeticItReplaces()
    {
        var options = ParseOptions<JToken>.CreateDefault();
        options.FunctionsProvider.RegisterText<JToken>();
        options.FunctionsProvider.RegisterMath<JToken>();
        var engine = new ScriptEngine<JToken>(options.CommandsProvider, options.FunctionsProvider);
        var document = JToken.Parse(@"{ ""iban"": ""NL91ABNA0417164300"" }");

        var result = engine.Execute(
            @"[{ ""command"": ""put"", ""path"": ""$.a"", ""value"": ""=right($.iban, 4)"" },
               { ""command"": ""put"", ""path"": ""$.b"", ""value"": ""=substring($.iban, =subtract(=length($.iban), 4), 4)"" }]",
            document, JsonExecutionContext.CreateDefault());

        Assert.That(result.Success, Is.True);
        Assert.That(result.Data!.SelectToken("$.a")!.Value<string>(), Is.EqualTo("4300"));
        Assert.That(result.Data!.SelectToken("$.b")!.Value<string>(), Is.EqualTo("4300"));
    }

    [Test]
    public void Right_TooFewArguments_FailsAndWarns()
    {
        var result = Run(new PathValue<JToken>("$.str"));
        Assert.That(result.Success, Is.False);
        Assert.That(context.GetLogEntries().Any(e => e.Level == LogLevel.Warning), Is.True);
    }

    [Test]
    public void Right_PathNotFound_Fails()
    {
        var result = Run(new PathValue<JToken>("$.missing"), new FixedValue<JToken>(new JValue(2)));
        Assert.That(result.Success, Is.False);
    }

    [Test]
    public void Right_FoundButNull_IsEmptyString()
    {
        var result = Run(new PathValue<JToken>("$.nothing"), new FixedValue<JToken>(new JValue(2)));
        Assert.That(result.Success, Is.True);
        Assert.That(result.Data.First!.Value<string>(), Is.EqualTo(string.Empty));
    }
}
