using Newtonsoft.Json.Linq;
using NUnit.Framework;
using TLio.Core.Models;
using TLio.Extensions.Text;
using TLio.Json;

namespace TLio.UnitTests.FunctionsTests.TextTests;

/// <summary>Ported from JLio.UnitTests.Text.SplitTests and JoinTests.</summary>
[TestFixture]
public class SplitJoinTests
{
    private JToken data = null!;
    private TLio.Core.Contracts.IExecutionContext<JToken> context = null!;

    [SetUp]
    public void Setup()
    {
        context = JsonExecutionContext.CreateDefault();
        data = JToken.Parse(@"{ ""csv"": ""a,b,c"", ""delim"": "","", ""arr"": [""x"", ""y"", ""z""], ""sep"": ""-"" }");
    }

    [Test] public void Split_ByDelimiter()
    {
        var fn = new Split<JToken>();
        fn.SetArguments(new Arguments<JToken>
        {
            new PathValue<JToken>("$.csv"),
            new PathValue<JToken>("$.delim")
        });
        var result = fn.Execute(data, data, context);
        Assert.That(result.Success, Is.True);
        var arr = (JArray)result.Data.First!;
        Assert.That(arr.Count, Is.EqualTo(3));
        Assert.That(arr[0].Value<string>(), Is.EqualTo("a"));
        Assert.That(arr[1].Value<string>(), Is.EqualTo("b"));
        Assert.That(arr[2].Value<string>(), Is.EqualTo("c"));
    }

    [Test] public void Split_TooFewArgs_ReturnsFailed()
    {
        var fn = new Split<JToken>();
        fn.SetArguments(new Arguments<JToken> { new PathValue<JToken>("$.csv") });
        var result = fn.Execute(data, data, context);
        Assert.That(result.Success, Is.False);
    }

    [Test] public void Join_WithSeparator()
    {
        var fn = new Join<JToken>();
        fn.SetArguments(new Arguments<JToken>
        {
            new PathValue<JToken>("$.arr"),
            new PathValue<JToken>("$.sep")
        });
        var result = fn.Execute(data, data, context);
        Assert.That(result.Success, Is.True);
        Assert.That(result.Data.First!.Value<string>(), Is.EqualTo("x-y-z"));
    }

    [Test] public void Join_EmptySeparator()
    {
        var data2 = JToken.Parse(@"{ ""arr"": [""a"", ""b"", ""c""], ""sep"": """" }");
        var fn = new Join<JToken>();
        fn.SetArguments(new Arguments<JToken>
        {
            new PathValue<JToken>("$.arr"),
            new PathValue<JToken>("$.sep")
        });
        var result = fn.Execute(data2, data2, context);
        Assert.That(result.Success, Is.True);
        Assert.That(result.Data.First!.Value<string>(), Is.EqualTo("abc"));
    }

    [Test] public void Join_ArrayNotFound_ReturnsFailed()
    {
        var fn = new Join<JToken>();
        fn.SetArguments(new Arguments<JToken>
        {
            new PathValue<JToken>("$.missing"),
            new PathValue<JToken>("$.sep")
        });
        var result = fn.Execute(data, data, context);
        Assert.That(result.Success, Is.False);
    }
}
