using Newtonsoft.Json.Linq;
using NUnit.Framework;
using TLio.Core.Models;
using TLio.Extensions.Text;
using TLio.Json;

namespace TLio.UnitTests.FunctionsTests.TextTests;

/// <summary>Ported from JLio.UnitTests.Text.IsEmptyTests.</summary>
[TestFixture]
public class IsEmptyTests
{
    private JToken data = null!;
    private TLio.Core.Contracts.IExecutionContext<JToken> context = null!;

    [SetUp]
    public void Setup()
    {
        context = JsonExecutionContext.CreateDefault();
        data = JToken.Parse(@"{ ""str"": ""hello"", ""empty"": """", ""nul"": null, ""arr"": [], ""arrFull"": [1] }");
    }

    [Test] public void IsEmpty_NonEmptyString_ReturnsFalse()
    {
        var fn = new IsEmpty<JToken>();
        fn.SetArguments(new Arguments<JToken> { new PathValue<JToken>("$.str") });
        var result = fn.Execute(data, data, context);
        Assert.That(result.Success, Is.True);
        Assert.That(result.Data.First!.Value<bool>(), Is.False);
    }

    [Test] public void IsEmpty_EmptyString_ReturnsTrue()
    {
        var fn = new IsEmpty<JToken>();
        fn.SetArguments(new Arguments<JToken> { new PathValue<JToken>("$.empty") });
        var result = fn.Execute(data, data, context);
        Assert.That(result.Success, Is.True);
        Assert.That(result.Data.First!.Value<bool>(), Is.True);
    }

    [Test] public void IsEmpty_Null_ReturnsTrue()
    {
        var fn = new IsEmpty<JToken>();
        fn.SetArguments(new Arguments<JToken> { new PathValue<JToken>("$.nul") });
        var result = fn.Execute(data, data, context);
        Assert.That(result.Success, Is.True);
        Assert.That(result.Data.First!.Value<bool>(), Is.True);
    }

    [Test] public void IsEmpty_EmptyArray_ReturnsTrue()
    {
        var fn = new IsEmpty<JToken>();
        fn.SetArguments(new Arguments<JToken> { new PathValue<JToken>("$.arr") });
        var result = fn.Execute(data, data, context);
        Assert.That(result.Success, Is.True);
        Assert.That(result.Data.First!.Value<bool>(), Is.True);
    }

    [Test] public void IsEmpty_NonEmptyArray_ReturnsFalse()
    {
        var fn = new IsEmpty<JToken>();
        fn.SetArguments(new Arguments<JToken> { new PathValue<JToken>("$.arrFull") });
        var result = fn.Execute(data, data, context);
        Assert.That(result.Success, Is.True);
        Assert.That(result.Data.First!.Value<bool>(), Is.False);
    }

    [Test] public void IsEmpty_PathNotFound_ReturnsFailed()
    {
        var fn = new IsEmpty<JToken>();
        fn.SetArguments(new Arguments<JToken> { new PathValue<JToken>("$.missing") });
        var result = fn.Execute(data, data, context);
        Assert.That(result.Success, Is.False);
    }
}
