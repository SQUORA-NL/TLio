using Newtonsoft.Json.Linq;
using NUnit.Framework;
using TLio.Commands;
using TLio.Core.Contracts;
using TLio.Core.Models;
using TLio.Extensions.Looping;
using TLio.Json;

namespace TLio.UnitTests.CommandsTests.LoopingTests;

[TestFixture]
public class WhileTests
{
    private IExecutionContext<JToken> executeOptions = null!;

    [SetUp]
    public void Setup()
    {
        executeOptions = JsonExecutionContext.CreateDefault();
    }

    /// <summary>Truthy while <c>$.counter</c> is below 5; each pass increments it.</summary>
    private static IFunctionSupportedValue<JToken> CounterBelow(int limit) =>
        new ConditionStub(dc => dc.SelectToken("$.counter")!.Value<int>() < limit);

    [Test]
    public void RepeatsUntilTheConditionGoesFalse()
    {
        var data = JToken.Parse("""{ "counter": 0 }""");
        var command = new While<JToken>
        {
            Condition = CounterBelow(5),
            MaxIterations = 100,
            Commands = new TLioScript<JToken>
            {
                new Set<JToken>("$.counter", new IncrementStub())
            }
        };

        var result = command.Execute(data, executeOptions);

        Assert.That(result.Success, Is.True);
        Assert.That(result.Data.SelectToken("$.counter")!.Value<int>(), Is.EqualTo(5));
    }

    [Test]
    public void FalseConditionFromTheStart_NeverRunsTheBody()
    {
        var data = JToken.Parse("""{ "counter": 10 }""");
        var command = new While<JToken>
        {
            Condition = CounterBelow(5),
            MaxIterations = 100,
            Commands = new TLioScript<JToken> { new Set<JToken>("$.counter", new IncrementStub()) }
        };

        var result = command.Execute(data, executeOptions);

        Assert.That(result.Success, Is.True);
        Assert.That(result.Data.SelectToken("$.counter")!.Value<int>(), Is.EqualTo(10));
    }

    [Test]
    public void MaxIterations_StopsAnOtherwiseUnboundedLoop()
    {
        // The condition never turns false on its own (counter is never touched) — MaxIterations
        // is the only thing that stops this, exactly the hang scenario it exists to prevent.
        var data = JToken.Parse("""{ "counter": 0, "runs": 0 }""");
        var command = new While<JToken>
        {
            Condition = CounterBelow(5),
            MaxIterations = 3,
            Commands = new TLioScript<JToken> { new Set<JToken>("$.runs", new IncrementStub("$.runs")) }
        };

        var result = command.Execute(data, executeOptions);

        Assert.That(result.Success, Is.True);
        Assert.That(result.Data.SelectToken("$.runs")!.Value<int>(), Is.EqualTo(3));
    }

    [Test]
    public void MissingCondition_FailsValidation()
    {
        var data = JToken.Parse("""{}""");
        var command = new While<JToken> { MaxIterations = 10 };

        var result = command.Execute(data, executeOptions);

        Assert.That(result.Success, Is.False);
    }

    [Test]
    public void NonPositiveMaxIterations_FailsValidation()
    {
        var data = JToken.Parse("""{ "counter": 0 }""");
        var command = new While<JToken> { Condition = CounterBelow(5), MaxIterations = 0 };

        var result = command.Execute(data, executeOptions);

        Assert.That(result.Success, Is.False);
    }

    [Test]
    public void NestedInsideForEach_ConditionCanReadTheOuterCurrentElement()
    {
        // while sets no current item of its own, but doesn't clear the enclosing forEach's
        // either — its condition still sees "@" as the item that forEach is currently on.
        var data = JToken.Parse("""{ "items": [3, 0, 2] }""");
        var outer = new TLio.Extensions.Looping.ForEach<JToken>
        {
            Path = "$.items",
            Commands = new TLioScript<JToken>
            {
                new While<JToken>
                {
                    Condition = new CurrentGreaterThanZero(),
                    MaxIterations = 10,
                    Commands = new TLioScript<JToken>
                    {
                        new Set<JToken>("@", new DecrementCurrent())
                    }
                }
            }
        };

        var result = outer.Execute(data, executeOptions);

        Assert.That(result.Success, Is.True);
        // Each element counts itself down to 0 via its own nested while.
        Assert.That(result.Data.SelectToken("$.items[0]")!.Value<int>(), Is.EqualTo(0));
        Assert.That(result.Data.SelectToken("$.items[1]")!.Value<int>(), Is.EqualTo(0));
        Assert.That(result.Data.SelectToken("$.items[2]")!.Value<int>(), Is.EqualTo(0));
    }

    private sealed class CurrentGreaterThanZero : IFunctionSupportedValue<JToken>
    {
        public FunctionResult<JToken> GetValue(JToken currentNode, JToken dataContext, IExecutionContext<JToken> context) =>
            FunctionResult<JToken>.Successful(new JValue(currentNode.Value<int>() > 0));
        public string ToScript() => "[current>0]";
    }

    private sealed class DecrementCurrent : IFunctionSupportedValue<JToken>
    {
        public FunctionResult<JToken> GetValue(JToken currentNode, JToken dataContext, IExecutionContext<JToken> context) =>
            FunctionResult<JToken>.Successful(new JValue(currentNode.Value<int>() - 1));
        public string ToScript() => "[decrement]";
    }

    private sealed class ConditionStub : IFunctionSupportedValue<JToken>
    {
        private readonly Func<JToken, bool> _predicate;
        public ConditionStub(Func<JToken, bool> predicate) => _predicate = predicate;

        public FunctionResult<JToken> GetValue(JToken currentNode, JToken dataContext, IExecutionContext<JToken> context) =>
            FunctionResult<JToken>.Successful(new JValue(_predicate(dataContext)));

        public string ToScript() => "[condition-stub]";
    }

    private sealed class IncrementStub : IFunctionSupportedValue<JToken>
    {
        private readonly string _path;
        public IncrementStub(string path = "$.counter") => _path = path;

        public FunctionResult<JToken> GetValue(JToken currentNode, JToken dataContext, IExecutionContext<JToken> context) =>
            FunctionResult<JToken>.Successful(new JValue(dataContext.SelectToken(_path)!.Value<int>() + 1));

        public string ToScript() => "[increment-stub]";
    }
}
