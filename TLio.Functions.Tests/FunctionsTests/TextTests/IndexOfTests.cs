using Microsoft.Extensions.Logging;
using Newtonsoft.Json.Linq;
using NUnit.Framework;
using TLio.Core.Models;
using TLio.Extensions.Text;
using TLio.Json;

namespace TLio.Functions.Tests.FunctionsTests.TextTests;

/// <summary>Ported from JLio.UnitTests.Text.IndexOfTests.</summary>
[TestFixture]
public class IndexOfTests
{
    private JToken data = null!;
    private TLio.Core.Contracts.IExecutionContext<JToken> context = null!;

    [SetUp]
    public void Setup()
    {
        context = JsonExecutionContext.CreateDefault();
        data = JToken.Parse(@"{ ""str"": ""Hello World"", ""sub"": ""World"", ""no"": ""XYZ"" }");
    }

    [Test] public void IndexOf_NotFound()
    {
        var fn = new IndexOf<JToken>();
        fn.SetArguments(new Arguments<JToken>
        {
            new PathValue<JToken>("$.str"),
            new PathValue<JToken>("$.no")
        });
        var result = fn.Execute(data, data, context);
        Assert.That(result.Success, Is.True);
        Assert.That(result.Data.First!.Value<long>(), Is.EqualTo(-1));
    }

    [Test] public void IndexOf_TooFewArgs_ReturnsFailed()
    {
        var fn = new IndexOf<JToken>();
        fn.SetArguments(new Arguments<JToken> { new PathValue<JToken>("$.str") });
        var result = fn.Execute(data, data, context);
        Assert.That(result.Success, Is.False);
    }

    [Test] public void IndexOf_PathNotFound_LogsError()
    {
        var fn = new IndexOf<JToken>();
        fn.SetArguments(new Arguments<JToken>
        {
            new PathValue<JToken>("$.missing"),
            new PathValue<JToken>("$.sub")
        });
        fn.Execute(data, data, context);
        Assert.That(context.GetLogEntries().Any(e => e.Level == LogLevel.Error), Is.True);
    }

    [Test] public void IndexOf_FoundInMiddle_ReturnsCorrectIndex()
    {
        var fn = new IndexOf<JToken>();
        fn.SetArguments(new Arguments<JToken>
        {
            new PathValue<JToken>("$.str"),
            new PathValue<JToken>("$.sub")
        });
        var result = fn.Execute(data, data, context);
        Assert.That(result.Success, Is.True);
        Assert.That(result.Data.First!.Value<long>(), Is.EqualTo(6));
    }

    [Test] public void IndexOf_FoundAtStart_ReturnsZero()
    {
        var d = JToken.Parse(@"{ ""str"": ""WorldHello"", ""sub"": ""World"" }");
        var fn = new IndexOf<JToken>();
        fn.SetArguments(new Arguments<JToken>
        {
            new PathValue<JToken>("$.str"),
            new PathValue<JToken>("$.sub")
        });
        var result = fn.Execute(d, d, context);
        Assert.That(result.Success, Is.True);
        Assert.That(result.Data.First!.Value<long>(), Is.EqualTo(0));
    }

    [Test] public void IndexOf_CaseInsensitive_LowercaseMatchesUppercase()
    {
        // IndexOf uses OrdinalIgnoreCase — "world" matches "World" at index 6
        var d = JToken.Parse(@"{ ""str"": ""Hello World"", ""sub"": ""world"" }");
        var fn = new IndexOf<JToken>();
        fn.SetArguments(new Arguments<JToken>
        {
            new PathValue<JToken>("$.str"),
            new PathValue<JToken>("$.sub")
        });
        var result = fn.Execute(d, d, context);
        Assert.That(result.Success, Is.True);
        Assert.That(result.Data.First!.Value<long>(), Is.EqualTo(6));
    }
}
