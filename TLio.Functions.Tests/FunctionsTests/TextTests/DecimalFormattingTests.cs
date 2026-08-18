using System.Globalization;
using Newtonsoft.Json.Linq;
using NUnit.Framework;
using TLio.Client;
using TLio.Extensions.Text;
using TLio.Json;

namespace TLio.Functions.Tests.FunctionsTests.TextTests;

/// <summary>
/// Fixed-decimal output: =toFixed(value, decimals) and the numeric format specifiers
/// that =format(...) now supports.
/// </summary>
[TestFixture]
public class DecimalFormattingTests
{
    private ScriptEngine<JToken> _engine = null!;

    [SetUp]
    public void Setup()
    {
        var options = ParseOptions<JToken>.CreateDefault();
        options.FunctionsProvider.RegisterText<JToken>();
        _engine = new ScriptEngine<JToken>(options.CommandsProvider, options.FunctionsProvider);
    }

    private JToken? Eval(string expression, string json = @"{ ""price"": 14.5, ""qty"": 3, ""text"": ""abc"", ""id"": 42 }")
    {
        var script = $@"[{{ ""command"": ""add"", ""path"": ""$.out"", ""value"": ""{expression}"" }}]";
        return _engine.Execute(script, JToken.Parse(json), JsonExecutionContext.CreateDefault()).Data["out"];
    }

    [TestCase("=toFixed($.price, 2)", "14.50")]
    [TestCase("=toFixed($.price, 0)", "15")]          // half away from zero
    [TestCase("=toFixed($.price, 4)", "14.5000")]
    [TestCase("=toFixed($.qty, 2)", "3.00")]
    [TestCase("=toFixed(2.345, 2)", "2.35")]
    [TestCase("=toFixed(-2.345, 2)", "-2.35")]
    [TestCase("=toFixed($.price, 2, ',')", "14,50")]
    public void ToFixed_ProducesExactDecimals(string expression, string expected)
    {
        var node = Eval(expression);
        Assert.That(node!.Type, Is.EqualTo(JTokenType.String), "toFixed must return a string to keep trailing zeros");
        Assert.That(node.Value<string>(), Is.EqualTo(expected));
    }

    [Test]
    public void ToFixed_IsCultureIndependent()
    {
        var previous = CultureInfo.CurrentCulture;
        try
        {
            // A locale whose decimal separator is a comma must not change the default output.
            CultureInfo.CurrentCulture = new CultureInfo("nl-NL");
            Assert.That(Eval("=toFixed($.price, 2)")!.Value<string>(), Is.EqualTo("14.50"));
        }
        finally
        {
            CultureInfo.CurrentCulture = previous;
        }
    }

    [TestCase("=toFixed($.text, 2)")]
    [TestCase("=toFixed($.price, -1)")]
    [TestCase("=toFixed($.price)")]
    [TestCase("=toFixed($.missing, 2)")]
    public void ToFixed_InvalidInput_ProducesNoValue(string expression)
        => Assert.That(Eval(expression), Is.Null);

    // ── format with numeric specifiers ────────────────────────────────────────

    [TestCase("=format('{0:F2}', $.price)", "14.50")]
    // format passes the specifier straight to .NET, which rounds half to even here —
    // use toFixed when you want money-style half-away-from-zero rounding.
    [TestCase("=format('{0:F0}', $.price)", "14")]
    [TestCase("=format('{0:00000}', $.id)", "00042")]
    [TestCase("=format('{0} costs {1:F2}', $.text, $.price)", "abc costs 14.50")]
    [TestCase("=format('{0}', $.text)", "abc")]
    public void Format_AppliesNumericSpecifiers(string expression, string expected)
        => Assert.That(Eval(expression)!.Value<string>(), Is.EqualTo(expected));

    [Test]
    public void Format_IsCultureIndependent()
    {
        var previous = CultureInfo.CurrentCulture;
        try
        {
            CultureInfo.CurrentCulture = new CultureInfo("nl-NL");
            Assert.That(Eval("=format('{0:F2}', $.price)")!.Value<string>(), Is.EqualTo("14.50"));
        }
        finally
        {
            CultureInfo.CurrentCulture = previous;
        }
    }
}
