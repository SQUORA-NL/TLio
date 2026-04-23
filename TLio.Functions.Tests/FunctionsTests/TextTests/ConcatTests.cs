using Microsoft.Extensions.Logging;
using Newtonsoft.Json.Linq;
using NUnit.Framework;
using TLio.Core.Models;
using TLio.Extensions.Text;
using TLio.Json;

namespace TLio.Functions.Tests.FunctionsTests.TextTests;

/// <summary>Ported from JLio.UnitTests.Text.ConcatTests.</summary>
[TestFixture]
public class ConcatTests
{
    private JToken data = null!;
    private TLio.Core.Contracts.IExecutionContext<JToken> context = null!;

    [SetUp]
    public void Setup()
    {
        context = JsonExecutionContext.CreateDefault();
        data = JToken.Parse(@"{ ""a"": ""Hello"", ""b"": "" "", ""c"": ""World"" }");
    }

    [Test] public void Concat_NoArgs_ReturnsFailed()
    {
        var fn = new Concat<JToken>();
        fn.SetArguments(new Arguments<JToken>());
        var result = fn.Execute(data, data, context);
        Assert.That(result.Success, Is.False);
    }

    [Test] public void Concat_NoArgs_LogsWarning()
    {
        var fn = new Concat<JToken>();
        fn.SetArguments(new Arguments<JToken>());
        fn.Execute(data, data, context);
        Assert.That(context.GetLogEntries().Any(e => e.Level == LogLevel.Warning), Is.True);
    }

    [Test] public void Concat_PathNotFound_ReturnsFailed()
    {
        var fn = new Concat<JToken>();
        fn.SetArguments(new Arguments<JToken> { new PathValue<JToken>("$.missing") });
        var result = fn.Execute(data, data, context);
        Assert.That(result.Success, Is.False);
    }

    [Test] public void Concat_PathNotFound_LogsError()
    {
        var fn = new Concat<JToken>();
        fn.SetArguments(new Arguments<JToken> { new PathValue<JToken>("$.missing") });
        fn.Execute(data, data, context);
        Assert.That(context.GetLogEntries().Any(e => e.Level == LogLevel.Error), Is.True);
    }

    [Test] public void Concat_ThreeStrings_ReturnsJoinedResult()
    {
        var fn = new Concat<JToken>();
        fn.SetArguments(new Arguments<JToken>
        {
            new PathValue<JToken>("$.a"),
            new PathValue<JToken>("$.b"),
            new PathValue<JToken>("$.c")
        });
        var result = fn.Execute(data, data, context);
        Assert.That(result.Success, Is.True);
        Assert.That(result.Data.First!.Value<string>(), Is.EqualTo("Hello World"));
    }

    [Test] public void Concat_SingleArg_ReturnsThatString()
    {
        var fn = new Concat<JToken>();
        fn.SetArguments(new Arguments<JToken> { new PathValue<JToken>("$.a") });
        var result = fn.Execute(data, data, context);
        Assert.That(result.Success, Is.True);
        Assert.That(result.Data.First!.Value<string>(), Is.EqualTo("Hello"));
    }

    [Test] public void Concat_EmptyStringArgs_ReturnsEmptyString()
    {
        var d = JToken.Parse(@"{ ""x"": """", ""y"": """" }");
        var fn = new Concat<JToken>();
        fn.SetArguments(new Arguments<JToken>
        {
            new PathValue<JToken>("$.x"),
            new PathValue<JToken>("$.y")
        });
        var result = fn.Execute(d, d, context);
        Assert.That(result.Success, Is.True);
        Assert.That(result.Data.First!.Value<string>(), Is.EqualTo(string.Empty));
    }
}
