using Newtonsoft.Json.Linq;
using NUnit.Framework;
using TLio.Core.Models;
using TLio.Extensions.Text;
using TLio.Json;

namespace TLio.Functions.Tests.FunctionsTests.TextTests;

/// <summary>Ported from JLio.UnitTests.Text.PadLeftTests and PadRightTests.</summary>
[TestFixture]
public class PadTests
{
    private JToken data = null!;
    private TLio.Core.Contracts.IExecutionContext<JToken> context = null!;

    [SetUp]
    public void Setup()
    {
        context = JsonExecutionContext.CreateDefault();
        data = JToken.Parse(@"{ ""str"": ""42"", ""width"": 5, ""pad"": ""0"" }");
    }

    [Test] public void PadLeft_TooFewArgs_ReturnsFailed()
    {
        var fn = new PadLeft<JToken>();
        fn.SetArguments(new Arguments<JToken> { new PathValue<JToken>("$.str") });
        var result = fn.Execute(data, data, context);
        Assert.That(result.Success, Is.False);
    }

    [Test] public void PadLeft_WithZeroChar_PadsLeftToWidth()
    {
        var fn = new PadLeft<JToken>();
        fn.SetArguments(new Arguments<JToken>
        {
            new PathValue<JToken>("$.str"),
            new PathValue<JToken>("$.width"),
            new PathValue<JToken>("$.pad")
        });
        var result = fn.Execute(data, data, context);
        Assert.That(result.Success, Is.True);
        Assert.That(result.Data.First!.Value<string>(), Is.EqualTo("00042"));
    }

    [Test] public void PadRight_WithDefaultSpace_PadsRightToWidth()
    {
        var fn = new PadRight<JToken>();
        fn.SetArguments(new Arguments<JToken>
        {
            new PathValue<JToken>("$.str"),
            new PathValue<JToken>("$.width")
        });
        var result = fn.Execute(data, data, context);
        Assert.That(result.Success, Is.True);
        Assert.That(result.Data.First!.Value<string>(), Is.EqualTo("42   "));
    }

    [Test] public void PadLeft_WhenAlreadyLongerThanWidth_ReturnsOriginal()
    {
        var d = JToken.Parse(@"{ ""str"": ""Hello World"", ""width"": 3, ""pad"": ""X"" }");
        var fn = new PadLeft<JToken>();
        fn.SetArguments(new Arguments<JToken>
        {
            new PathValue<JToken>("$.str"),
            new PathValue<JToken>("$.width"),
            new PathValue<JToken>("$.pad")
        });
        var result = fn.Execute(d, d, context);
        Assert.That(result.Success, Is.True);
        Assert.That(result.Data.First!.Value<string>(), Is.EqualTo("Hello World"));
    }
}
