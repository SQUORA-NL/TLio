using Newtonsoft.Json.Linq;
using NUnit.Framework;
using TLio.Core.Models;
using TLio.Extensions.Text;
using TLio.Json;

namespace TLio.UnitTests.FunctionsTests.TextTests;

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

    [Test] public void Concat_TwoStrings()
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

    [Test] public void Concat_SingleArg()
    {
        var fn = new Concat<JToken>();
        fn.SetArguments(new Arguments<JToken> { new PathValue<JToken>("$.a") });
        var result = fn.Execute(data, data, context);
        Assert.That(result.Success, Is.True);
        Assert.That(result.Data.First!.Value<string>(), Is.EqualTo("Hello"));
    }

    [Test] public void Concat_NoArgs_ReturnsFailed()
    {
        var fn = new Concat<JToken>();
        fn.SetArguments(new Arguments<JToken>());
        var result = fn.Execute(data, data, context);
        Assert.That(result.Success, Is.False);
    }

    [Test] public void Concat_PathNotFound_ReturnsFailed()
    {
        var fn = new Concat<JToken>();
        fn.SetArguments(new Arguments<JToken> { new PathValue<JToken>("$.missing") });
        var result = fn.Execute(data, data, context);
        Assert.That(result.Success, Is.False);
    }
}
