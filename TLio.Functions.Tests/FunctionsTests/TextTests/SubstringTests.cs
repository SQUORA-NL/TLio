using Microsoft.Extensions.Logging;
using Newtonsoft.Json.Linq;
using NUnit.Framework;
using TLio.Core.Models;
using TLio.Extensions.Text;
using TLio.Json;

namespace TLio.Functions.Tests.FunctionsTests.TextTests;

/// <summary>Ported from JLio.UnitTests.Text.SubstringTests.</summary>
[TestFixture]
public class SubstringTests
{
    private JToken data = null!;
    private TLio.Core.Contracts.IExecutionContext<JToken> context = null!;

    [SetUp]
    public void Setup()
    {
        context = JsonExecutionContext.CreateDefault();
        data = JToken.Parse(@"{ ""str"": ""Hello World"", ""start"": 6, ""len"": 5 }");
    }

    [Test] public void Substring_TooFewArgs_ReturnsFailed()
    {
        var fn = new Substring<JToken>();
        fn.SetArguments(new Arguments<JToken> { new PathValue<JToken>("$.str") });
        var result = fn.Execute(data, data, context);
        Assert.That(result.Success, Is.False);
    }

    [Test] public void Substring_TooFewArgs_LogsWarning()
    {
        var fn = new Substring<JToken>();
        fn.SetArguments(new Arguments<JToken> { new PathValue<JToken>("$.str") });
        fn.Execute(data, data, context);
        Assert.That(context.GetLogEntries().Any(e => e.Level == LogLevel.Warning), Is.True);
    }

    [Test] public void Substring_StartAndLength_ReturnsCorrectSubstring()
    {
        var fn = new Substring<JToken>();
        fn.SetArguments(new Arguments<JToken>
        {
            new PathValue<JToken>("$.str"),
            new PathValue<JToken>("$.start"),
            new PathValue<JToken>("$.len")
        });
        var result = fn.Execute(data, data, context);
        Assert.That(result.Success, Is.True);
        Assert.That(result.Data.First!.Value<string>(), Is.EqualTo("World"));
    }

    [Test] public void Substring_StartOnly_ReturnsFromStartToEnd()
    {
        var fn = new Substring<JToken>();
        fn.SetArguments(new Arguments<JToken>
        {
            new PathValue<JToken>("$.str"),
            new FixedValue<JToken>(new JValue(6))
        });
        var result = fn.Execute(data, data, context);
        Assert.That(result.Success, Is.True);
        Assert.That(result.Data.First!.Value<string>(), Is.EqualTo("World"));
    }

    [Test] public void Substring_OutOfRangeStart_ClampsToStringEnd()
    {
        var fn = new Substring<JToken>();
        fn.SetArguments(new Arguments<JToken>
        {
            new PathValue<JToken>("$.str"),
            new FixedValue<JToken>(new JValue(999))
        });
        var result = fn.Execute(data, data, context);
        Assert.That(result.Success, Is.True);
        Assert.That(result.Data.First!.Value<string>(), Is.EqualTo(string.Empty));
    }
}
