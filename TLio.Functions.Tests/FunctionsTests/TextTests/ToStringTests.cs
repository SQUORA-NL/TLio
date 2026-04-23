using Microsoft.Extensions.Logging;
using Newtonsoft.Json.Linq;
using NUnit.Framework;
using TLio.Core.Models;
using TLio.Extensions.Text;
using TLio.Json;

namespace TLio.Functions.Tests.FunctionsTests.TextTests;

[TestFixture]
public class ToStringTests
{
    private TLio.Core.Contracts.IExecutionContext<JToken> context = null!;

    [SetUp]
    public void Setup() => context = JsonExecutionContext.CreateDefault();

    [Test]
    public void ToString_NoArgs_ReturnsFailed_AndLogsWarning()
    {
        var fn = new ToStringFunction<JToken>();
        fn.SetArguments(new Arguments<JToken>());
        var result = fn.Execute(JObject.Parse("{}"), JObject.Parse("{}"), context);
        Assert.That(result.Success, Is.False);
        Assert.That(context.GetLogEntries().Any(e => e.Level == LogLevel.Warning), Is.True);
    }

    [Test]
    public void ToString_PathNotFound_ReturnsFailed_AndLogsWarning()
    {
        var data = JToken.Parse(@"{ ""x"": 1 }");
        var fn = new ToStringFunction<JToken>();
        fn.SetArguments(new Arguments<JToken> { new PathValue<JToken>("$.missing") });
        var result = fn.Execute(data, data, context);
        Assert.That(result.Success, Is.False);
        Assert.That(context.GetLogEntries().Any(e => e.Level == LogLevel.Warning), Is.True);
    }

    [Test]
    public void ToString_IntegerValue_ReturnsStringRepresentation()
    {
        var data = JToken.Parse(@"{ ""n"": 42 }");
        var fn = new ToStringFunction<JToken>();
        fn.SetArguments(new Arguments<JToken> { new PathValue<JToken>("$.n") });
        var result = fn.Execute(data, data, context);
        Assert.That(result.Success, Is.True);
        Assert.That(result.Data[0].ToObject<string>(), Is.EqualTo("42"));
        Assert.That(context.GetLogEntries().Any(e => e.Level == LogLevel.Information), Is.True);
    }

    [Test]
    public void ToString_FloatValue_ReturnsStringRepresentation()
    {
        var data = JToken.Parse(@"{ ""n"": 3.14 }");
        var fn = new ToStringFunction<JToken>();
        fn.SetArguments(new Arguments<JToken> { new PathValue<JToken>("$.n") });
        var result = fn.Execute(data, data, context);
        Assert.That(result.Success, Is.True);
        Assert.That(result.Data[0].ToObject<string>(), Is.Not.Empty);
    }

    [Test]
    public void ToString_BoolValue_ReturnsStringRepresentation()
    {
        var data = JToken.Parse(@"{ ""flag"": true }");
        var fn = new ToStringFunction<JToken>();
        fn.SetArguments(new Arguments<JToken> { new PathValue<JToken>("$.flag") });
        var result = fn.Execute(data, data, context);
        Assert.That(result.Success, Is.True);
        Assert.That(result.Data[0].ToObject<string>()!.ToLowerInvariant(), Is.EqualTo("true"));
    }

    [Test]
    public void ToString_NullValue_ReturnsEmptyString()
    {
        var data = JToken.Parse(@"{ ""n"": null }");
        var fn = new ToStringFunction<JToken>();
        fn.SetArguments(new Arguments<JToken> { new PathValue<JToken>("$.n") });
        var result = fn.Execute(data, data, context);
        Assert.That(result.Success, Is.True);
        Assert.That(result.Data[0].ToObject<string>(), Is.EqualTo(string.Empty));
    }

    [Test]
    public void ToString_ObjectValue_ReturnsJsonString()
    {
        var data = JToken.Parse(@"{ ""obj"": { ""a"": 1 } }");
        var fn = new ToStringFunction<JToken>();
        fn.SetArguments(new Arguments<JToken> { new PathValue<JToken>("$.obj") });
        var result = fn.Execute(data, data, context);
        Assert.That(result.Success, Is.True);
        var str = result.Data[0].ToObject<string>()!;
        Assert.That(str, Does.Contain("a"));
        Assert.That(context.GetLogEntries().Any(e => e.Level == LogLevel.Information), Is.True);
    }

    [Test]
    public void ToString_ArrayValue_ReturnsJsonString()
    {
        var data = JToken.Parse(@"{ ""arr"": [1, 2, 3] }");
        var fn = new ToStringFunction<JToken>();
        fn.SetArguments(new Arguments<JToken> { new PathValue<JToken>("$.arr") });
        var result = fn.Execute(data, data, context);
        Assert.That(result.Success, Is.True);
        var str = result.Data[0].ToObject<string>()!;
        Assert.That(str, Does.StartWith("["));
    }

    [Test]
    public void ToString_StringValue_ReturnsSameString()
    {
        var data = JToken.Parse(@"{ ""s"": ""hello"" }");
        var fn = new ToStringFunction<JToken>();
        fn.SetArguments(new Arguments<JToken> { new PathValue<JToken>("$.s") });
        var result = fn.Execute(data, data, context);
        Assert.That(result.Success, Is.True);
        Assert.That(result.Data[0].ToObject<string>(), Is.EqualTo("hello"));
    }
}
