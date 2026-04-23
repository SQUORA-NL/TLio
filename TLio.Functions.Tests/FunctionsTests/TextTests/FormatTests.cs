using Microsoft.Extensions.Logging;
using Newtonsoft.Json.Linq;
using NUnit.Framework;
using TLio.Core.Models;
using TLio.Extensions.Text;
using TLio.Json;

namespace TLio.Functions.Tests.FunctionsTests.TextTests;

/// <summary>Ported from JLio.UnitTests.Text.FormatTests.</summary>
[TestFixture]
public class FormatTests
{
    private JToken data = null!;
    private TLio.Core.Contracts.IExecutionContext<JToken> context = null!;

    [SetUp]
    public void Setup()
    {
        context = JsonExecutionContext.CreateDefault();
        data = JToken.Parse(@"{ ""tmpl"": ""Hello {0}!"", ""name"": ""World"", ""tmpl2"": ""{0} and {1}"", ""a"": ""foo"", ""b"": ""bar"" }");
    }

    [Test] public void Format_NoArgs_ReturnsFailed()
    {
        var fn = new Format<JToken>();
        fn.SetArguments(new Arguments<JToken>());
        var result = fn.Execute(data, data, context);
        Assert.That(result.Success, Is.False);
    }

    [Test] public void Format_NoArgs_LogsWarning()
    {
        var fn = new Format<JToken>();
        fn.SetArguments(new Arguments<JToken>());
        fn.Execute(data, data, context);
        Assert.That(context.GetLogEntries().Any(e => e.Level == LogLevel.Warning), Is.True);
    }

    [Test] public void Format_SinglePlaceholder_ReturnsFormattedString()
    {
        var fn = new Format<JToken>();
        fn.SetArguments(new Arguments<JToken>
        {
            new PathValue<JToken>("$.tmpl"),
            new PathValue<JToken>("$.name")
        });
        var result = fn.Execute(data, data, context);
        Assert.That(result.Success, Is.True);
        Assert.That(result.Data.First!.Value<string>(), Is.EqualTo("Hello World!"));
    }

    [Test] public void Format_MultiplePlaceholders_ReturnsFormattedString()
    {
        var fn = new Format<JToken>();
        fn.SetArguments(new Arguments<JToken>
        {
            new PathValue<JToken>("$.tmpl2"),
            new PathValue<JToken>("$.a"),
            new PathValue<JToken>("$.b")
        });
        var result = fn.Execute(data, data, context);
        Assert.That(result.Success, Is.True);
        Assert.That(result.Data.First!.Value<string>(), Is.EqualTo("foo and bar"));
    }

    [Test] public void Format_TemplateOnly_ReturnsTemplateUnchanged()
    {
        var d = JToken.Parse(@"{ ""tmpl"": ""no placeholders here"" }");
        var fn = new Format<JToken>();
        fn.SetArguments(new Arguments<JToken> { new PathValue<JToken>("$.tmpl") });
        var result = fn.Execute(d, d, context);
        Assert.That(result.Success, Is.True);
        Assert.That(result.Data.First!.Value<string>(), Is.EqualTo("no placeholders here"));
    }
}
