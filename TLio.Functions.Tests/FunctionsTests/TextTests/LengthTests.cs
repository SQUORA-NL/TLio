using Microsoft.Extensions.Logging;
using Newtonsoft.Json.Linq;
using NUnit.Framework;
using TLio.Core.Models;
using TLio.Extensions.Text;
using TLio.Json;

namespace TLio.Functions.Tests.FunctionsTests.TextTests;

/// <summary>Ported from JLio.UnitTests.Text.LengthTests.</summary>
[TestFixture]
public class LengthTests
{
    private JToken data = null!;
    private TLio.Core.Contracts.IExecutionContext<JToken> context = null!;

    [SetUp]
    public void Setup()
    {
        context = JsonExecutionContext.CreateDefault();
        data = JToken.Parse(@"{ ""str"": ""Hello"", ""arr"": [1, 2, 3], ""empty"": """", ""nul"": null }");
    }

    [Test] public void Length_PathNotFound_ReturnsFailed()
    {
        var fn = new Length<JToken>();
        fn.SetArguments(new Arguments<JToken> { new PathValue<JToken>("$.missing") });
        var result = fn.Execute(data, data, context);
        Assert.That(result.Success, Is.False);
    }

    [Test] public void Length_PathNotFound_LogsError()
    {
        var fn = new Length<JToken>();
        fn.SetArguments(new Arguments<JToken> { new PathValue<JToken>("$.missing") });
        fn.Execute(data, data, context);
        Assert.That(context.GetLogEntries().Any(e => e.Level == LogLevel.Error), Is.True);
    }

    [Test] public void Length_OfString_ReturnsCharacterCount()
    {
        var fn = new Length<JToken>();
        fn.SetArguments(new Arguments<JToken> { new PathValue<JToken>("$.str") });
        var result = fn.Execute(data, data, context);
        Assert.That(result.Success, Is.True);
        Assert.That(result.Data.First!.Value<long>(), Is.EqualTo(5));
    }

    [Test] public void Length_OfEmptyString_ReturnsZero()
    {
        var fn = new Length<JToken>();
        fn.SetArguments(new Arguments<JToken> { new PathValue<JToken>("$.empty") });
        var result = fn.Execute(data, data, context);
        Assert.That(result.Success, Is.True);
        Assert.That(result.Data.First!.Value<long>(), Is.EqualTo(0));
    }

    [Test] public void Length_OfArray_ReturnsElementCount()
    {
        var fn = new Length<JToken>();
        fn.SetArguments(new Arguments<JToken> { new PathValue<JToken>("$.arr") });
        var result = fn.Execute(data, data, context);
        Assert.That(result.Success, Is.True);
        Assert.That(result.Data.First!.Value<long>(), Is.EqualTo(3));
    }

    [Test] public void Length_OfNull_ReturnsZero()
    {
        var fn = new Length<JToken>();
        fn.SetArguments(new Arguments<JToken> { new PathValue<JToken>("$.nul") });
        var result = fn.Execute(data, data, context);
        Assert.That(result.Success, Is.True);
        Assert.That(result.Data.First!.Value<long>(), Is.EqualTo(0));
    }
}
