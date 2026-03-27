using Newtonsoft.Json.Linq;
using NUnit.Framework;
using TLio.Core.Models;
using TLio.Extensions.Text;
using TLio.Json;

namespace TLio.UnitTests.FunctionsTests.TextTests;

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

    [Test] public void Format_OneArg()
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

    [Test] public void Format_TwoArgs()
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

    [Test] public void Format_NoArgs_ReturnsFailed()
    {
        var fn = new Format<JToken>();
        fn.SetArguments(new Arguments<JToken>());
        var result = fn.Execute(data, data, context);
        Assert.That(result.Success, Is.False);
    }

    [Test] public void Format_TemplateOnly_NoPlaceholders()
    {
        var data2 = JToken.Parse(@"{ ""tmpl"": ""Static text"" }");
        var fn = new Format<JToken>();
        fn.SetArguments(new Arguments<JToken> { new PathValue<JToken>("$.tmpl") });
        var result = fn.Execute(data2, data2, context);
        Assert.That(result.Success, Is.True);
        Assert.That(result.Data.First!.Value<string>(), Is.EqualTo("Static text"));
    }
}
