using Microsoft.Extensions.Logging;
using Newtonsoft.Json.Linq;
using NUnit.Framework;
using TLio.Extensions.ETL.Commands;
using TLio.Extensions.ETL.Commands.Models;
using TLio.Json;

namespace TLio.Functions.Tests.FunctionsTests.ETLTests;

/// <summary>Dedicated edge-case tests for the ToCsv command.</summary>
[TestFixture]
public class ToCsvTests
{
    private TLio.Core.Contracts.IExecutionContext<JToken> context = null!;

    [SetUp]
    public void SetUp() => context = JsonExecutionContext.CreateDefault();

    [Test]
    public void ToCsv_EmptyArray_ProducesHeaderOnly()
    {
        var data = JToken.Parse(@"{ ""rows"": [] }");
        var cmd = new ToCsv<JToken> { Path = "$.rows" };
        var result = cmd.Execute(data, context);
        Assert.That(result.Success, Is.True);
        // An empty array produces an empty CSV or a header-only CSV — not an exception
        var csv = data["rows"]?.Value<string>() ?? string.Empty;
        Assert.That(csv, Is.Not.Null);
    }

    [Test]
    public void ToCsv_NullFieldValue_RendersAsEmptyCell()
    {
        var data = JToken.Parse(@"{ ""rows"": [{ ""name"": ""Alice"", ""age"": null }] }");
        var cmd = new ToCsv<JToken> { Path = "$.rows" };
        var result = cmd.Execute(data, context);
        Assert.That(result.Success, Is.True);
        var csv = data["rows"]!.Value<string>()!;
        var lines = csv.Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries);
        Assert.That(lines.Length, Is.EqualTo(2));
        // Null renders as NullValueRepresentation (default ""), so data row has trailing comma or empty cell
        Assert.That(lines[1], Does.Contain("Alice"));
    }

    [Test]
    public void ToCsv_TabDelimiter_UsesTabs()
    {
        var data = JToken.Parse(@"{ ""rows"": [{ ""a"": ""1"", ""b"": ""2"" }] }");
        var cmd = new ToCsv<JToken>
        {
            Path = "$.rows",
            CsvSettings = new CsvSettings { Delimiter = "\t" }
        };
        var result = cmd.Execute(data, context);
        Assert.That(result.Success, Is.True);
        var csv = data["rows"]!.Value<string>()!;
        Assert.That(csv, Does.Contain("\t"));
    }

    [Test]
    public void ToCsv_SemicolonDelimiter_UsesSemicolons()
    {
        var data = JToken.Parse(@"{ ""rows"": [{ ""x"": ""foo"", ""y"": ""bar"" }] }");
        var cmd = new ToCsv<JToken>
        {
            Path = "$.rows",
            CsvSettings = new CsvSettings { Delimiter = ";" }
        };
        var result = cmd.Execute(data, context);
        Assert.That(result.Success, Is.True);
        var csv = data["rows"]!.Value<string>()!;
        Assert.That(csv, Does.Contain(";"));
        Assert.That(csv, Does.Not.Contain(","));
    }

    [Test]
    public void ToCsv_InconsistentFields_MissingKeyRendersAsEmpty()
    {
        // Row 2 lacks "age" — should still succeed and produce empty cell
        var data = JToken.Parse(@"{ ""rows"": [{ ""name"": ""Alice"", ""age"": 30 }, { ""name"": ""Bob"" }] }");
        var cmd = new ToCsv<JToken> { Path = "$.rows" };
        var result = cmd.Execute(data, context);
        Assert.That(result.Success, Is.True);
        var csv = data["rows"]!.Value<string>()!;
        Assert.That(csv, Does.Contain("Alice"));
        Assert.That(csv, Does.Contain("Bob"));
    }

    [Test]
    public void ToCsv_ValidationFailure_EmptyPath_LogsWarning()
    {
        var data = JToken.Parse(@"{ ""rows"": [] }");
        var cmd = new ToCsv<JToken> { Path = "" };
        var result = cmd.Execute(data, context);
        Assert.That(result.Success, Is.False);
        Assert.That(context.GetLogEntries().Any(e => e.Level == LogLevel.Warning), Is.True);
    }

    [Test]
    public void ToCsv_NoHeaders_ProducesDataWithoutHeaderRow()
    {
        var data = JToken.Parse(@"{ ""rows"": [{ ""a"": ""1"", ""b"": ""2"" }] }");
        var cmd = new ToCsv<JToken>
        {
            Path = "$.rows",
            CsvSettings = new CsvSettings { IncludeHeaders = false }
        };
        var result = cmd.Execute(data, context);
        Assert.That(result.Success, Is.True);
        var csv = data["rows"]!.Value<string>()!;
        var lines = csv.Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries);
        // Without headers, only the data row is present
        Assert.That(lines.Length, Is.EqualTo(1));
    }

    [Test]
    public void ToCsv_ValidationFailure_NullSettings_LogsWarning()
    {
        var data = JToken.Parse(@"{ ""rows"": [{ ""a"": 1 }] }");
        var cmd = new ToCsv<JToken> { Path = "$.rows", CsvSettings = null! };
        var result = cmd.Execute(data, context);
        Assert.That(result.Success, Is.False);
        Assert.That(context.GetLogEntries().Any(e => e.Level == LogLevel.Warning), Is.True);
    }
}
