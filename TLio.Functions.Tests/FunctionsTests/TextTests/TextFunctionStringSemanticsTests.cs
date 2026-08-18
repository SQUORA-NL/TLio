using Newtonsoft.Json.Linq;
using NUnit.Framework;
using TLio.Client;
using TLio.Extensions.Text;
using TLio.Json;

namespace TLio.Functions.Tests.FunctionsTests.TextTests;

/// <summary>
/// Text functions are string in / string out: a numeric or boolean input is read as its
/// text form, and the result is always a string node — a substring of a number is text,
/// never a number. Only the positional arguments (indexes, lengths) are numeric.
/// </summary>
[TestFixture]
public class TextFunctionStringSemanticsTests
{
    private ScriptEngine<JToken> _engine = null!;

    [SetUp]
    public void Setup()
    {
        var options = ParseOptions<JToken>.CreateDefault();
        options.FunctionsProvider.RegisterText<JToken>();
        _engine = new ScriptEngine<JToken>(options.CommandsProvider, options.FunctionsProvider);
    }

    private JToken Eval(string expression)
    {
        const string json = @"{ ""text"": ""Sanne"", ""number"": 20260818, ""price"": 14.5, ""flag"": true }";
        var script = $@"[{{ ""command"": ""add"", ""path"": ""$.out"", ""value"": ""{expression}"" }}]";
        var node = _engine.Execute(script, JToken.Parse(json), JsonExecutionContext.CreateDefault()).Data["out"];
        Assert.That(node, Is.Not.Null, $"'{expression}' produced no value");
        return node!;
    }

    [TestCase("=substring($.text, 0, 3)", "San")]
    [TestCase("=substring($.number, 0, 4)", "2026")]     // numeric input read as text
    [TestCase("=substring($.number, 4, 2)", "08")]       // and keeps its leading zero
    [TestCase("=substring($.price, 0, 2)", "14")]
    [TestCase("=substring($.flag, 0, 1)", "T")]
    public void Substring_ReadsInputAsTextAndReturnsText(string expression, string expected)
    {
        var node = Eval(expression);
        Assert.That(node.Type, Is.EqualTo(JTokenType.String), "a substring is text, even of a number");
        Assert.That(node.Value<string>(), Is.EqualTo(expected));
    }

    [Test]
    public void Substring_ResultThatLooksNumeric_IsStillAString()
    {
        var node = Eval("=substring($.number, 0, 4)");
        Assert.That(node.Type, Is.EqualTo(JTokenType.String));
        Assert.That(node.Type, Is.Not.EqualTo(JTokenType.Integer));
    }

    [TestCase("=concat($.number, '')")]
    [TestCase("=toUpper($.text)")]
    [TestCase("=trim($.number)")]
    [TestCase("=replace($.number, '2026', '2027')")]
    [TestCase("=padLeft($.number, 12, '0')")]
    [TestCase("=toString($.price)")]
    [TestCase("=toFixed($.price, 2)")]
    public void TextFunctions_AlwaysReturnAStringNode(string expression)
        => Assert.That(Eval(expression).Type, Is.EqualTo(JTokenType.String));

    [Test]
    public void Length_ReturnsANumber_NotAString()
    {
        // The exception that proves the rule: length answers "how many", not "what text".
        Assert.That(Eval("=length($.text)").Type, Is.EqualTo(JTokenType.Integer));
        Assert.That(Eval("=length($.text)").Value<int>(), Is.EqualTo(5));
    }

    [Test]
    public void IndexOf_ReturnsANumber_NotAString()
        => Assert.That(Eval("=indexOf($.text, 'n')").Type, Is.EqualTo(JTokenType.Integer));

    [TestCase("=substring($.text, '0', '3')", "San")]
    public void Substring_AcceptsQuotedNumericIndexes(string expression, string expected)
        => Assert.That(Eval(expression).Value<string>(), Is.EqualTo(expected));
}
