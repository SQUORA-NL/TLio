using Newtonsoft.Json.Linq;
using NUnit.Framework;
using TLio.Core.Models;
using TLio.Extensions.Text;
using TLio.Json;

namespace TLio.UnitTests.FunctionsTests.TextTests;

/// <summary>Ported from JLio.UnitTests.Text.TrimTests.</summary>
[TestFixture]
public class TrimTests
{
    private JToken data = null!;
    private TLio.Core.Contracts.IExecutionContext<JToken> context = null!;

    [SetUp]
    public void Setup()
    {
        context = JsonExecutionContext.CreateDefault();
        data = JToken.Parse(@"{ ""padded"": ""  hello  "" }");
    }

    [Test] public void Trim_BothEnds()
    {
        var fn = new Trim<JToken>();
        fn.SetArguments(new Arguments<JToken> { new PathValue<JToken>("$.padded") });
        var result = fn.Execute(data, data, context);
        Assert.That(result.Success, Is.True);
        Assert.That(result.Data.First!.Value<string>(), Is.EqualTo("hello"));
    }

    [Test] public void TrimStart_LeftOnly()
    {
        var fn = new TrimStart<JToken>();
        fn.SetArguments(new Arguments<JToken> { new PathValue<JToken>("$.padded") });
        var result = fn.Execute(data, data, context);
        Assert.That(result.Success, Is.True);
        Assert.That(result.Data.First!.Value<string>(), Is.EqualTo("hello  "));
    }

    [Test] public void TrimEnd_RightOnly()
    {
        var fn = new TrimEnd<JToken>();
        fn.SetArguments(new Arguments<JToken> { new PathValue<JToken>("$.padded") });
        var result = fn.Execute(data, data, context);
        Assert.That(result.Success, Is.True);
        Assert.That(result.Data.First!.Value<string>(), Is.EqualTo("  hello"));
    }

    [Test] public void Trim_PathNotFound_ReturnsFailed()
    {
        var fn = new Trim<JToken>();
        fn.SetArguments(new Arguments<JToken> { new PathValue<JToken>("$.missing") });
        var result = fn.Execute(data, data, context);
        Assert.That(result.Success, Is.False);
    }
}
