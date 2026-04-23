using Newtonsoft.Json.Linq;
using NUnit.Framework;
using TLio.Core.Models;
using TLio.Extensions.Text;
using TLio.Json;

namespace TLio.Functions.Tests.FunctionsTests.TextTests;

/// <summary>Ported from JLio.UnitTests.Text.StartsWithTests, EndsWithTests, ContainsTests.</summary>
[TestFixture]
public class PredicateTests
{
    private JToken data = null!;
    private TLio.Core.Contracts.IExecutionContext<JToken> context = null!;

    [SetUp]
    public void Setup()
    {
        context = JsonExecutionContext.CreateDefault();
        data = JToken.Parse(@"{ ""str"": ""Hello World"", ""prefix"": ""Hello"", ""suffix"": ""World"", ""sub"": ""lo Wo"", ""no"": ""XYZ"" }");
    }

    [Test] public void StartsWith_NoMatch()
    {
        var fn = new StartsWith<JToken>();
        fn.SetArguments(new Arguments<JToken>
        {
            new PathValue<JToken>("$.str"),
            new PathValue<JToken>("$.no")
        });
        var result = fn.Execute(data, data, context);
        Assert.That(result.Success, Is.True);
        Assert.That(result.Data.First!.Value<bool>(), Is.False);
    }

    [Test] public void EndsWith_NoMatch()
    {
        var fn = new EndsWith<JToken>();
        fn.SetArguments(new Arguments<JToken>
        {
            new PathValue<JToken>("$.str"),
            new PathValue<JToken>("$.no")
        });
        var result = fn.Execute(data, data, context);
        Assert.That(result.Success, Is.True);
        Assert.That(result.Data.First!.Value<bool>(), Is.False);
    }

    [Test] public void Contains_NoMatch()
    {
        var fn = new Contains<JToken>();
        fn.SetArguments(new Arguments<JToken>
        {
            new PathValue<JToken>("$.str"),
            new PathValue<JToken>("$.no")
        });
        var result = fn.Execute(data, data, context);
        Assert.That(result.Success, Is.True);
        Assert.That(result.Data.First!.Value<bool>(), Is.False);
    }

    [Test] public void Contains_CaseInsensitive()
    {
        var data2 = JToken.Parse(@"{ ""str"": ""Hello World"", ""sub"": ""hello"" }");
        var fn = new Contains<JToken>();
        fn.SetArguments(new Arguments<JToken>
        {
            new PathValue<JToken>("$.str"),
            new PathValue<JToken>("$.sub")
        });
        var result = fn.Execute(data2, data2, context);
        Assert.That(result.Success, Is.True);
        Assert.That(result.Data.First!.Value<bool>(), Is.True);
    }

    [Test] public void StartsWith_Match()
    {
        var fn = new StartsWith<JToken>();
        fn.SetArguments(new Arguments<JToken>
        {
            new PathValue<JToken>("$.str"),
            new PathValue<JToken>("$.prefix")
        });
        var result = fn.Execute(data, data, context);
        Assert.That(result.Success, Is.True);
        Assert.That(result.Data.First!.Value<bool>(), Is.True);
    }

    [Test] public void EndsWith_Match()
    {
        var fn = new EndsWith<JToken>();
        fn.SetArguments(new Arguments<JToken>
        {
            new PathValue<JToken>("$.str"),
            new PathValue<JToken>("$.suffix")
        });
        var result = fn.Execute(data, data, context);
        Assert.That(result.Success, Is.True);
        Assert.That(result.Data.First!.Value<bool>(), Is.True);
    }

    [Test] public void Contains_Match()
    {
        var fn = new Contains<JToken>();
        fn.SetArguments(new Arguments<JToken>
        {
            new PathValue<JToken>("$.str"),
            new PathValue<JToken>("$.sub")
        });
        var result = fn.Execute(data, data, context);
        Assert.That(result.Success, Is.True);
        Assert.That(result.Data.First!.Value<bool>(), Is.True);
    }

    [Test] public void StartsWith_EmptyNeedle_ReturnsTrue()
    {
        var d = JToken.Parse(@"{ ""str"": ""hello"", ""empty"": """" }");
        var fn = new StartsWith<JToken>();
        fn.SetArguments(new Arguments<JToken>
        {
            new PathValue<JToken>("$.str"),
            new PathValue<JToken>("$.empty")
        });
        var result = fn.Execute(d, d, context);
        Assert.That(result.Success, Is.True);
        Assert.That(result.Data.First!.Value<bool>(), Is.True);
    }
}
