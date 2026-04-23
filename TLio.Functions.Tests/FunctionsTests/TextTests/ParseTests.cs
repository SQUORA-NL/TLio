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

    [Test] public void Parse_NumericString_ReturnsNumericToken()
    {
        var fn = new Parse<JToken>();
        fn.SetArguments(new Arguments<JToken> { new PathValue<JToken>("$.num") });
        var result = fn.Execute(data, data, context);
        Assert.That(result.Success, Is.True);
        Assert.That(result.Data.First!.Value<int>(), Is.EqualTo(42));
    }

    [Test] public void Parse_JsonArrayString_ReturnsArray()
    {
        var fn = new Parse<JToken>();
        fn.SetArguments(new Arguments<JToken> { new PathValue<JToken>("$.arr") });
        var result = fn.Execute(data, data, context);
        Assert.That(result.Success, Is.True);
        Assert.That(result.Data.First!.Type, Is.EqualTo(JTokenType.Array));
    }

    [Test] public void Parse_JsonObjectString_ReturnsObject()
    {
        var d = JToken.Parse("{ \"objStr\": \"{\\\"a\\\":1}\" }");
        var fn = new Parse<JToken>();
        fn.SetArguments(new Arguments<JToken> { new PathValue<JToken>("$.objStr") });
        var result = fn.Execute(d, d, context);
        Assert.That(result.Success, Is.True);
        Assert.That(result.Data.First!.Type, Is.EqualTo(JTokenType.Object));
    }
}
