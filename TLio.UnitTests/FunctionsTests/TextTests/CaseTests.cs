using Newtonsoft.Json.Linq;
using NUnit.Framework;
using TLio.Core.Models;
using TLio.Extensions.Text;
using TLio.Json;

namespace TLio.UnitTests.FunctionsTests.TextTests;

/// <summary>Ported from JLio.UnitTests.Text.ToUpperTests and ToLowerTests.</summary>
[TestFixture]
public class CaseTests
{
    private JToken data = null!;
    private TLio.Core.Contracts.IExecutionContext<JToken> context = null!;

    [SetUp]
    public void Setup()
    {
        context = JsonExecutionContext.CreateDefault();
        data = JToken.Parse(@"{ ""mixed"": ""Hello World"" }");
    }

    [Test] public void ToUpper_Basic()
    {
        var fn = new ToUpper<JToken>();
        fn.SetArguments(new Arguments<JToken> { new PathValue<JToken>("$.mixed") });
        var result = fn.Execute(data, data, context);
        Assert.That(result.Success, Is.True);
        Assert.That(result.Data.First!.Value<string>(), Is.EqualTo("HELLO WORLD"));
    }

    [Test] public void ToLower_Basic()
    {
        var fn = new ToLower<JToken>();
        fn.SetArguments(new Arguments<JToken> { new PathValue<JToken>("$.mixed") });
        var result = fn.Execute(data, data, context);
        Assert.That(result.Success, Is.True);
        Assert.That(result.Data.First!.Value<string>(), Is.EqualTo("hello world"));
    }

    [Test] public void ToUpper_PathNotFound_ReturnsFailed()
    {
        var fn = new ToUpper<JToken>();
        fn.SetArguments(new Arguments<JToken> { new PathValue<JToken>("$.missing") });
        var result = fn.Execute(data, data, context);
        Assert.That(result.Success, Is.False);
    }

    [Test] public void ToLower_PathNotFound_ReturnsFailed()
    {
        var fn = new ToLower<JToken>();
        fn.SetArguments(new Arguments<JToken> { new PathValue<JToken>("$.missing") });
        var result = fn.Execute(data, data, context);
        Assert.That(result.Success, Is.False);
    }
}
