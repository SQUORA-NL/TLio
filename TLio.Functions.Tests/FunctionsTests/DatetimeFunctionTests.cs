using Microsoft.Extensions.Logging;
using Newtonsoft.Json.Linq;
using NUnit.Framework;
using TLio.Commands;
using TLio.Core.Contracts;
using TLio.Core.Models;
using TLio.Functions;
using TLio.Json;

namespace TLio.Functions.Tests.FunctionsTests;

/// <summary>
/// Ported from JLio.UnitTests.FunctionsTests.DatetimeFunctionTests.
/// Adaptations:
///   - ExecutionContext.CreateDefault() → JsonExecutionContext.CreateDefault()
///   - NUnit classic API → Assert.That() constraint model (NUnit 4)
/// </summary>
[TestFixture]
public class DatetimeFunctionTests
{
    private JToken data = null!;
    private IExecutionContext<JToken> executeOptions = null!;

    [SetUp]
    public void Setup()
    {
        executeOptions = JsonExecutionContext.CreateDefault();
        data = JObject.Parse("{ \"demo\": \"old value\" }");
    }

    [Test]
    public void CanGetDatetimeValue()
    {
        var script = new TLioScript<JToken>
        {
            new Set<JToken>("$.demo", new FunctionSupportedValue<JToken>(new Datetime<JToken>()))
        };
        var result = script.Execute(data, executeOptions);

        Assert.That(result, Is.Not.Null);
        Assert.That(result.Success, Is.True);
        Assert.That(result.Data.SelectToken("$.demo")?.Type, Is.Not.EqualTo(JTokenType.Null));
        Assert.That(result.Data.SelectToken("$.demo")?.Value<string>(), Is.Not.Null);
    }

    [Test]
    public void CanGetDatetimeValueWithDefaultIso8601Format()
    {
        var script = new TLioScript<JToken>
        {
            new Set<JToken>("$.demo", new FunctionSupportedValue<JToken>(new Datetime<JToken>()))
        };
        var result = script.Execute(data, executeOptions);

        Assert.That(result.Success, Is.True);
        var value = result.Data.SelectToken("$.demo")?.Value<string>();
        Assert.That(value, Is.Not.Null);
        // ISO 8601 format: yyyy-MM-ddTHH:mm:ss.fffZ
        Assert.That(value, Does.Match(@"\d{4}-\d{2}-\d{2}T\d{2}:\d{2}:\d{2}\.\d{3}Z"));
    }

    [Test]
    public void CanGetDatetimeValueIsNotEmpty()
    {
        var script = new TLioScript<JToken>
        {
            new Add<JToken>("$.datetimeValue", new FunctionSupportedValue<JToken>(new Datetime<JToken>()))
        };
        var result = script.Execute(data, executeOptions);

        Assert.That(result.Success, Is.True);
        Assert.That(result.Data.SelectToken("$.datetimeValue")?.Value<string>(), Is.Not.Empty);
    }

    [Test]
    public void InvalidFormatArg_FallsBackToIso8601()
    {
        // An invalid .NET format string triggers a catch block and falls back to ISO 8601
        var fn = new Datetime<JToken>();
        fn.SetArguments(new Arguments<JToken>
        {
            new FixedValue<JToken>(new JValue("%INVALID-FORMAT-STRING%"))
        });
        var result = fn.Execute(data, data, executeOptions);
        Assert.That(result.Success, Is.True);
        var value = result.Data.First!.Value<string>();
        Assert.That(value, Is.Not.Null.And.Not.Empty);
    }

    [Test]
    public void EmptyStringFormatArg_UsesDefaultIso8601()
    {
        // Empty string format → treated as "not provided" → uses default ISO 8601
        var fn = new Datetime<JToken>();
        fn.SetArguments(new Arguments<JToken>
        {
            new FixedValue<JToken>(new JValue(string.Empty))
        });
        var result = fn.Execute(data, data, executeOptions);
        Assert.That(result.Success, Is.True);
        var value = result.Data.First!.Value<string>();
        Assert.That(value, Does.Match(@"\d{4}-\d{2}-\d{2}T\d{2}:\d{2}:\d{2}"));
    }

    [Test]
    public void CustomFormat_ReturnsFormattedDate()
    {
        var fn = new Datetime<JToken>();
        fn.SetArguments(new Arguments<JToken>
        {
            new FixedValue<JToken>(new JValue("yyyy"))
        });
        var result = fn.Execute(data, data, executeOptions);
        Assert.That(result.Success, Is.True);
        var value = result.Data.First!.Value<string>();
        Assert.That(value, Does.Match(@"\d{4}"));
        Assert.That(value!.Length, Is.EqualTo(4));
    }
}
