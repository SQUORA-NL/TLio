using System.Diagnostics;
using Newtonsoft.Json.Linq;
using NUnit.Framework;
using TLio.Commands;
using TLio.Core.Contracts;
using TLio.Core.Models;
using TLio.Extensions.Looping;
using TLio.Json;

namespace TLio.UnitTests.Performance;

/// <summary>
/// forEach/while do per-iteration work outside the nested script itself: saving/restoring
/// context.CurrentNode around each pass. None of that is supposed to scale with anything other
/// than iteration count, so these tests build a much larger schedule than any real ACTUS
/// contract needs (a decades-long daily cycle is a few thousand payments; this goes past that)
/// and check wall-clock time grows roughly linearly with N rather than quadratically.
/// </summary>
[TestFixture]
public class Looping_PerformanceTests
{
    private const int WarmupIterations = 3;
    private IExecutionContext<JToken> _context = null!;

    [SetUp]
    public void SetUp() => _context = JsonExecutionContext.CreateDefault();

    private static JToken BuildSourceArray(int count)
    {
        var items = new JArray();
        for (var i = 0; i < count; i++) items.Add(i);
        return new JObject { ["items"] = items };
    }

    private long TimeForEach(int count)
    {
        var command = new ForEach<JToken>
        {
            Path = "$.items",
            MaxIterations = count + 10,
            // Doubles every element in place through "@" — no separate output array, no
            // appendTo, so there is nothing here whose cost should depend on N beyond the N
            // iterations themselves.
            Commands = new TLioScript<JToken> { new Set<JToken>("@", new DoubleCurrent()) }
        };
        for (var w = 0; w < WarmupIterations; w++)
            command.Execute(BuildSourceArray(100), _context);

        var data = BuildSourceArray(count);
        var sw = Stopwatch.StartNew();
        var result = command.Execute(data, _context);
        sw.Stop();

        Assert.That(result.Success, Is.True);
        Assert.That(result.Data.SelectToken("$.items[0]")!.Value<int>(), Is.EqualTo(0));
        Assert.That(result.Data.SelectToken($"$.items[{count - 1}]")!.Value<int>(), Is.EqualTo((count - 1) * 2));
        return sw.ElapsedMilliseconds;
    }

    [Test]
    public void ForEach_TimeGrowsRoughlyLinearlyWithIterationCount()
    {
        const int small = 2_000;
        const int large = 20_000; // 10x

        var smallMs = TimeForEach(small);
        var largeMs = TimeForEach(large);

        TestContext.WriteLine($"forEach: {small:N0} items = {smallMs} ms, {large:N0} items = {largeMs} ms " +
                               $"(ratio {(smallMs == 0 ? 0 : (double)largeMs / smallMs):F1}x for a 10x size increase)");

        // A generous ceiling, not a tight bound: catching O(n^2) (which would blow well past this
        // at 20,000 iterations), not chasing a specific millisecond number.
        Assert.That(largeMs, Is.LessThan(5_000),
            $"{large:N0} forEach iterations took {largeMs} ms — looks worse than linear.");
    }

    [Test]
    public void While_TimeGrowsRoughlyLinearlyWithIterationCount()
    {
        long RunWhile(int count)
        {
            var data = new JObject { ["counter"] = 0, ["limit"] = count };
            var command = new While<JToken>
            {
                Condition = new CounterBelowLimit(),
                MaxIterations = count + 10,
                Commands = new TLioScript<JToken> { new Set<JToken>("$.counter", new IncrementCounter()) }
            };
            var sw = Stopwatch.StartNew();
            var result = command.Execute(data, _context);
            sw.Stop();
            Assert.That(result.Success, Is.True);
            Assert.That(result.Data.SelectToken("$.counter")!.Value<int>(), Is.EqualTo(count));
            return sw.ElapsedMilliseconds;
        }

        const int small = 2_000;
        const int large = 20_000;

        for (var w = 0; w < WarmupIterations; w++) RunWhile(100);

        var smallMs = RunWhile(small);
        var largeMs = RunWhile(large);

        TestContext.WriteLine($"while: {small:N0} iterations = {smallMs} ms, {large:N0} iterations = {largeMs} ms " +
                               $"(ratio {(smallMs == 0 ? 0 : (double)largeMs / smallMs):F1}x for a 10x size increase)");

        Assert.That(largeMs, Is.LessThan(5_000),
            $"{large:N0} while iterations took {largeMs} ms — looks worse than linear.");
    }

    [Test]
    public void ForEachWithFieldWrites_TimeGrowsRoughlyLinearlyWithIterationCount()
    {
        // The ACTUS PAM shape: iterate objects, write a couple of fields per element via "@.x",
        // rather than replacing the whole element via bare "@".
        JToken BuildObjectArray(int count)
        {
            var items = new JArray();
            for (var i = 0; i < count; i++) items.Add(new JObject { ["n"] = i });
            return new JObject { ["items"] = items };
        }

        var command = new ForEach<JToken>
        {
            Path = "$.items",
            MaxIterations = 30_010,
            Commands = new TLioScript<JToken>
            {
                new Add<JToken>("@.doubled", new DoubleCurrentField())
            }
        };

        for (var w = 0; w < WarmupIterations; w++)
            command.Execute(BuildObjectArray(100), _context);

        const int count = 20_000;
        var data = BuildObjectArray(count);
        var sw = Stopwatch.StartNew();
        var result = command.Execute(data, _context);
        sw.Stop();

        TestContext.WriteLine($"forEach field-writes: {count:N0} items = {sw.ElapsedMilliseconds} ms");

        Assert.That(result.Success, Is.True);
        Assert.That(result.Data.SelectToken($"$.items[{count - 1}].doubled")!.Value<int>(), Is.EqualTo((count - 1) * 2));
        Assert.That(sw.ElapsedMilliseconds, Is.LessThan(5_000));
    }

    private sealed class DoubleCurrent : IFunctionSupportedValue<JToken>
    {
        public FunctionResult<JToken> GetValue(JToken currentNode, JToken dataContext, IExecutionContext<JToken> context) =>
            FunctionResult<JToken>.Successful(new JValue(currentNode.Value<int>() * 2));
        public string ToScript() => "[double-current]";
    }

    private sealed class DoubleCurrentField : IFunctionSupportedValue<JToken>
    {
        public FunctionResult<JToken> GetValue(JToken currentNode, JToken dataContext, IExecutionContext<JToken> context) =>
            FunctionResult<JToken>.Successful(new JValue(currentNode.SelectToken("n")!.Value<int>() * 2));
        public string ToScript() => "[double-current-field]";
    }

    private sealed class CounterBelowLimit : IFunctionSupportedValue<JToken>
    {
        public FunctionResult<JToken> GetValue(JToken currentNode, JToken dataContext, IExecutionContext<JToken> context) =>
            FunctionResult<JToken>.Successful(new JValue(
                dataContext.SelectToken("$.counter")!.Value<int>() < dataContext.SelectToken("$.limit")!.Value<int>()));
        public string ToScript() => "[counter<limit]";
    }

    private sealed class IncrementCounter : IFunctionSupportedValue<JToken>
    {
        public FunctionResult<JToken> GetValue(JToken currentNode, JToken dataContext, IExecutionContext<JToken> context) =>
            FunctionResult<JToken>.Successful(new JValue(dataContext.SelectToken("$.counter")!.Value<int>() + 1));
        public string ToScript() => "[increment]";
    }
}
