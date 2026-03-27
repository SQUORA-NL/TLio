using Newtonsoft.Json.Linq;
using NUnit.Framework;
using TLio.Core.Models;
using TLio.Extensions.Text;
using TLio.Json;

namespace TLio.UnitTests.FunctionsTests.TextTests;

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

    [Test] public void IndexOf_Found()
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
}
