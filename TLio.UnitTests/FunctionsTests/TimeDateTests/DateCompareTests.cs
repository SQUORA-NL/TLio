using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using NUnit.Framework;
using TLio.Core.Models;
using TLio.Extensions.TimeDate;
using TLio.Json;

namespace TLio.UnitTests.FunctionsTests.TimeDateTests;

[TestFixture]
public class DateCompareTests
{
    private JToken data = null!;
    private TLio.Core.Contracts.IExecutionContext<JToken> context = null!;

    private static JToken ParseDates(string json)
    {
        using var reader = new JsonTextReader(new System.IO.StringReader(json))
            { DateParseHandling = DateParseHandling.None };
        return JToken.Load(reader);
    }

    [SetUp]
    public void Setup()
    {
        context = JsonExecutionContext.CreateDefault();
        data = ParseDates(@"{
            ""earlier"": ""2024-01-01"",
            ""later"":   ""2024-06-15"",
            ""same"":    ""2024-01-01"",
            ""ts1"":     ""2024-03-15T10:30:00Z"",
            ""ts2"":     ""2024-03-15T18:00:00Z""
        }");
    }

    [Test]
    public void DateCompare_Earlier_ReturnsMinusOne()
    {
        var fn = new DateCompare<JToken>();
        fn.SetArguments(new Arguments<JToken>
        {
            new PathValue<JToken>("$.earlier"),
            new PathValue<JToken>("$.later")
        });
        var result = fn.Execute(data, data, context);
        Assert.That(result.Success, Is.True);
        Assert.That(result.Data.First!.Value<long>(), Is.EqualTo(-1L));
    }

    [Test]
    public void DateCompare_Later_ReturnsOne()
    {
        var fn = new DateCompare<JToken>();
        fn.SetArguments(new Arguments<JToken>
        {
            new PathValue<JToken>("$.later"),
            new PathValue<JToken>("$.earlier")
        });
        var result = fn.Execute(data, data, context);
        Assert.That(result.Success, Is.True);
        Assert.That(result.Data.First!.Value<long>(), Is.EqualTo(1L));
    }

    [Test]
    public void DateCompare_Equal_ReturnsZero()
    {
        var fn = new DateCompare<JToken>();
        fn.SetArguments(new Arguments<JToken>
        {
            new PathValue<JToken>("$.earlier"),
            new PathValue<JToken>("$.same")
        });
        var result = fn.Execute(data, data, context);
        Assert.That(result.Success, Is.True);
        Assert.That(result.Data.First!.Value<long>(), Is.EqualTo(0L));
    }

    [Test]
    public void DateCompare_WithTimestamps_Earlier()
    {
        var fn = new DateCompare<JToken>();
        fn.SetArguments(new Arguments<JToken>
        {
            new PathValue<JToken>("$.ts1"),
            new PathValue<JToken>("$.ts2")
        });
        var result = fn.Execute(data, data, context);
        Assert.That(result.Success, Is.True);
        Assert.That(result.Data.First!.Value<long>(), Is.EqualTo(-1L));
    }

    [Test]
    public void DateCompare_TooFewArgs_ReturnsFailed()
    {
        var fn = new DateCompare<JToken>();
        fn.SetArguments(new Arguments<JToken> { new PathValue<JToken>("$.earlier") });
        var result = fn.Execute(data, data, context);
        Assert.That(result.Success, Is.False);
    }

    [Test]
    public void DateCompare_PathNotFound_ReturnsFailed()
    {
        var fn = new DateCompare<JToken>();
        fn.SetArguments(new Arguments<JToken>
        {
            new PathValue<JToken>("$.missing"),
            new PathValue<JToken>("$.earlier")
        });
        var result = fn.Execute(data, data, context);
        Assert.That(result.Success, Is.False);
    }
}
