using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using NUnit.Framework;
using TLio.Core.Models;
using TLio.Extensions.TimeDate;
using TLio.Json;

namespace TLio.Functions.Tests.FunctionsTests.TimeDateTests;

[TestFixture]
public class IsDateBetweenTests
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
            ""start"":  ""2024-01-01"",
            ""end"":    ""2024-12-31"",
            ""inside"": ""2024-06-15"",
            ""before"": ""2023-12-31"",
            ""after"":  ""2025-01-01"",
            ""onStart"": ""2024-01-01"",
            ""onEnd"":   ""2024-12-31""
        }");
    }

    [Test]
    public void IsDateBetween_TooFewArgs_ReturnsFailed()
    {
        var fn = new IsDateBetween<JToken>();
        fn.SetArguments(new Arguments<JToken>
        {
            new PathValue<JToken>("$.inside"),
            new PathValue<JToken>("$.start")
        });
        var result = fn.Execute(data, data, context);
        Assert.That(result.Success, Is.False);
    }

    [Test]
    public void IsDateBetween_PathNotFound_ReturnsFailed()
    {
        var fn = new IsDateBetween<JToken>();
        fn.SetArguments(new Arguments<JToken>
        {
            new PathValue<JToken>("$.missing"),
            new PathValue<JToken>("$.start"),
            new PathValue<JToken>("$.end")
        });
        var result = fn.Execute(data, data, context);
        Assert.That(result.Success, Is.False);
    }
}
