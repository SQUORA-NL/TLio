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
}
