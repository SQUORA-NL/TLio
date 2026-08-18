using Newtonsoft.Json.Linq;
using NUnit.Framework;
using TLio.Client;
using TLio.Extensions.Text;
using TLio.Json;

namespace TLio.Functions.Tests.FunctionsTests;

/// <summary>
/// Engine-level coverage for the inner-function notation rule: only the outermost
/// expression must start with "=", the "=" on nested calls is optional.
/// </summary>
[TestFixture]
public class InnerFunctionNotationTests
{
    private ScriptEngine<JToken> _engine = null!;

    [SetUp]
    public void Setup()
    {
        var options = ParseOptions<JToken>.CreateDefault();
        options.FunctionsProvider.RegisterText<JToken>();
        _engine = new ScriptEngine<JToken>(options.CommandsProvider, options.FunctionsProvider);
    }

    private JToken Run(string value)
    {
        var data = JToken.Parse(
            @"{ ""first"": ""Sanne"", ""last"": ""Berg"", ""number"": 152, ""blank"": """" }");
        var script = $@"[{{ ""command"": ""add"", ""path"": ""$.result"", ""value"": ""{value}"" }}]";
        var context = JsonExecutionContext.CreateDefault();
        var result = _engine.Execute(script, data, context);
        Assert.That(result.Success, Is.True);
        return result.Data["result"]!;
    }

    [Test]
    public void InnerCall_WithoutEqualsPrefix_IsEvaluated()
        => Assert.That(Run("=concat(fetch($.first), ' ', fetch($.last))").Value<string>(),
            Is.EqualTo("Sanne Berg"));

    [Test]
    public void InnerCall_WithEqualsPrefix_StillWorks()
        => Assert.That(Run("=concat(=fetch($.first), ' ', =fetch($.last))").Value<string>(),
            Is.EqualTo("Sanne Berg"));

    [Test]
    public void InnerCall_MixedPrefixing_IsEquivalent()
        => Assert.That(Run("=concat(fetch($.first), ' ', =fetch($.last))").Value<string>(),
            Is.EqualTo("Sanne Berg"));

    [Test]
    public void InnerCall_NestedTwoLevelsDeep_IsEvaluated()
        => Assert.That(Run("=concat(toUpper(fetch($.first)), '-', toLower($.last))").Value<string>(),
            Is.EqualTo("SANNE-berg"));

    [Test]
    public void InnerCall_QuotedArgWithPath_IsEvaluated()
        => Assert.That(Run("=concat(fetch('$.first'), '!')").Value<string>(),
            Is.EqualTo("Sanne!"));

    [Test]
    public void InnerCall_OnNumericNode_IsEvaluated()
        => Assert.That(Run("=concat('nr ', toString(fetch($.number)))").Value<string>(),
            Is.EqualTo("nr 152"));

    // ── Things that must NOT be reinterpreted as calls ────────────────────────

    [Test]
    public void QuotedCallLikeText_StaysLiteral()
        => Assert.That(Run("=concat('fetch($.first)', '!')").Value<string>(),
            Is.EqualTo("fetch($.first)!"));

    [Test]
    public void UnregisteredName_StaysLiteral()
        => Assert.That(Run("=concat(notAFunction($.first), '!')").Value<string>(),
            Is.EqualTo("notAFunction($.first)!"));

    [Test]
    public void TextContainingParentheses_StaysLiteral()
        => Assert.That(Run("=concat($.first, ' ', 'a(1) b(2)')").Value<string>(),
            Is.EqualTo("Sanne a(1) b(2)"));
}
