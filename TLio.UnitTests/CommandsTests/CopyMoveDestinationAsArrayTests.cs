using Newtonsoft.Json.Linq;
using NUnit.Framework;
using TLio.Commands;
using TLio.Core.Contracts;
using TLio.Core.Models;
using TLio.Json;

namespace TLio.UnitTests.CommandsTests;

/// <summary>
/// Ported from JLio.UnitTests.CommandsTests.CopyMoveDestinationAsArrayTests.
/// Adaptations:
///   - ExecutionContext.CreateDefault() → JsonExecutionContext.CreateDefault()
///   - new Copy/Move(from, to, flag) → new Copy/Move&lt;JToken&gt;(from, to, flag)
///   - NUnit classic API → Assert.That() constraint model (NUnit 4)
///
/// NOTE (constitution §VI): These tests use inline data. A follow-up task will
/// refactor all command/function tests to use file-based fixture triplets.
/// </summary>
[TestFixture]
public class CopyMoveDestinationAsArrayTests
{
    private JToken data = null!;
    private IExecutionContext<JToken> executeOptions = null!;

    [SetUp]
    public void Setup()
    {
        executeOptions = JsonExecutionContext.CreateDefault();
        data = JToken.Parse("{ \"val\": 1, \"arr\": [1], \"obj\": {\"val\": 1} }");
    }

    [Test]
    public void CopyToScalarDestinationAsArray_AppendsOldAndNew()
    {
        var result = new Copy<JToken>("$.val", "$.val", true).Execute(data, executeOptions);
        Assert.That(result.Success, Is.True);
        var arr = data["val"] as JArray;
        Assert.That(arr, Is.Not.Null);
        Assert.That(arr!.Count, Is.EqualTo(2));
        Assert.That(arr[0].Value<int>(), Is.EqualTo(1));
        Assert.That(arr[1].Value<int>(), Is.EqualTo(1));
    }

    [Test]
    public void CopyToExistingArrayDestinationAsArray_Appends()
    {
        var result = new Copy<JToken>("$.val", "$.arr", true).Execute(data, executeOptions);
        Assert.That(result.Success, Is.True);
        var arr = data["arr"] as JArray;
        Assert.That(arr, Is.Not.Null);
        Assert.That(arr!.Count, Is.EqualTo(2));
        Assert.That(arr[0].Value<int>(), Is.EqualTo(1));
        Assert.That(arr[1].Value<int>(), Is.EqualTo(1));
    }

    [Test]
    public void MoveToScalarDestinationAsArray_AppendsOldAndNewAndRemovesSource()
    {
        var result = new Move<JToken>("$.val", "$.val", true).Execute(data, executeOptions);
        Assert.That(result.Success, Is.True);
        var arr = data["val"] as JArray;
        Assert.That(arr, Is.Not.Null);
        Assert.That(arr!.Count, Is.EqualTo(2));
        Assert.That(arr[0].Value<int>(), Is.EqualTo(1));
        Assert.That(arr[1].Value<int>(), Is.EqualTo(1));
    }
}
