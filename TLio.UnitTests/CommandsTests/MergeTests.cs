using Microsoft.Extensions.Logging;
using Newtonsoft.Json.Linq;
using NUnit.Framework;
using TLio.Commands.Advanced;
using TLio.Core.Contracts;
using TLio.Core.Models;
using TLio.Json;

namespace TLio.UnitTests.CommandsTests;

/// <summary>
/// Ported from JLio.UnitTests.CommandsTests.MergeTests.
/// Adaptations:
///   - NUnit classic API → Assert.That() constraint model (NUnit 4)
///   - fluent JLioScript API → TLioScript&lt;JToken&gt; object list
/// </summary>
[TestFixture]
public class MergeTests
{
    private JToken data = null!;
    private IExecutionContext<JToken> executeOptions = null!;

    [SetUp]
    public void Setup()
    {
        executeOptions = JsonExecutionContext.CreateDefault();
        data = JToken.Parse(
            "{ \"source\": { \"a\": 1, \"b\": 2 }, \"target\": { \"b\": 99, \"c\": 3 }, \"sourceArray\": [1, 2, 3], \"targetArray\": [4, 5, 6] }");
    }

    [Test]
    public void CanMergeObjects()
    {
        var result = new Merge<JToken>("$.source", "$.target").Execute(data, executeOptions);

        Assert.That(result, Is.Not.Null);
        Assert.That(result.Success, Is.True);
        // target should now include keys from source
        Assert.That(data.SelectToken("$.target.a")?.Value<int>(), Is.EqualTo(1));
        Assert.That(data.SelectToken("$.target.c")?.Value<int>(), Is.EqualTo(3));
    }

    [Test]
    public void CanMergeArraysWithConcat()
    {
        var result = new Merge<JToken>("$.sourceArray", "$.targetArray", ArrayMergeMode.Concat)
            .Execute(data, executeOptions);

        Assert.That(result, Is.Not.Null);
        Assert.That(result.Success, Is.True);
        var targetArray = data.SelectToken("$.targetArray") as JArray;
        Assert.That(targetArray, Is.Not.Null);
        Assert.That(targetArray!.Count, Is.EqualTo(6)); // 3 original + 3 merged
    }

    [Test]
    public void CanHandleMissingSourcePath()
    {
        var result = new Merge<JToken>("$.missing", "$.target").Execute(data, executeOptions);

        Assert.That(result, Is.Not.Null);
        Assert.That(result.Success, Is.True);
    }

    [Test]
    public void CanHandleMissingTargetPath()
    {
        var result = new Merge<JToken>("$.source", "$.missing").Execute(data, executeOptions);

        Assert.That(result, Is.Not.Null);
        Assert.That(result.Success, Is.True);
    }

    [Test]
    public void CanExecuteWithMissingPaths()
    {
        var result = new Merge<JToken>("", "").Execute(data, executeOptions);

        Assert.That(result, Is.Not.Null);
        Assert.That(result.Success, Is.False);
    }

    // ── Article X: logging assertions ────────────────────────────────────────

    [Test]
    public void Merge_Success_LogsInfoEntry()
    {
        var result = new Merge<JToken>("$.source", "$.target").Execute(data, executeOptions);

        Assert.That(result.Success, Is.True);
        Assert.That(executeOptions.GetLogEntries().Any(e => e.Level == LogLevel.Information), Is.True);
    }

    [Test]
    public void Merge_SourceIsArray_TargetIsObject_DoesNotThrow()
    {
        // Merging an array into an object is an edge case — should complete without exception
        var result = new Merge<JToken>("$.sourceArray", "$.target").Execute(data, executeOptions);

        Assert.That(result, Is.Not.Null);
        Assert.That(result.Success, Is.True);
    }

    [Test]
    public void CanUseScriptApi()
    {
        var script = new TLioScript<JToken>
        {
            new Merge<JToken>("$.source", "$.target")
        };
        var result = script.Execute(data, executeOptions);

        Assert.That(result, Is.Not.Null);
        Assert.That(result.Success, Is.True);
    }
}
