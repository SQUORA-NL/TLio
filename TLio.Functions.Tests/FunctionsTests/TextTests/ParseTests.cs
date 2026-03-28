using Newtonsoft.Json.Linq;
using NUnit.Framework;
using TLio.Core.Models;
using TLio.Extensions.Text;
using TLio.Json;

namespace TLio.Functions.Tests.FunctionsTests.TextTests;

/// <summary>Ported from JLio.UnitTests.Text.ParseTests.</summary>
[TestFixture]
public class ParseTests
{
    private JToken data = null!;
    private TLio.Core.Contracts.IExecutionContext<JToken> context = null!;

    [SetUp]
    public void Setup()
    {
        context = JsonExecutionContext.CreateDefault();
        data = JToken.Parse(@"{ ""num"": ""42"", ""arr"": ""[1,2,3]"", ""obj"": ""{\""\""a\""\"":\""1\""}"", ""plain"": ""hello"" }");
    }

    [Test] public void Parse_PlainString_ReturnsString()
    {
        var fn = new Parse<JToken>();
        fn.SetArguments(new Arguments<JToken> { new PathValue<JToken>("$.plain") });
        var result = fn.Execute(data, data, context);
        Assert.That(result.Success, Is.True);
        Assert.That(result.Data.First!.Value<string>(), Is.EqualTo("hello"));
    }

    [Test] public void Parse_NoArgs_ReturnsFailed()
    {
        var fn = new Parse<JToken>();
        fn.SetArguments(new Arguments<JToken>());
        var result = fn.Execute(data, data, context);
        Assert.That(result.Success, Is.False);
    }
}
