using Newtonsoft.Json.Linq;
using NUnit.Framework;
using TLio.Commands;
using TLio.Core.Contracts;
using TLio.Core.Models;
using TLio.Extensions.Looping;
using TLio.Json;

namespace TLio.UnitTests.CommandsTests.LoopingTests;

[TestFixture]
public class ForEachTests
{
    private IExecutionContext<JToken> executeOptions = null!;

    [SetUp]
    public void Setup()
    {
        executeOptions = JsonExecutionContext.CreateDefault();
    }

    [Test]
    public void AtBarePath_ReplacesEachElementWithItsOwnComputedValue()
    {
        // "@" alone is the element's own path — set replaces it wholesale, in place, no
        // separate output array needed.
        var data = JToken.Parse("""{ "numbers": [1,2,3] }""");
        var command = new ForEach<JToken>
        {
            Path = "$.numbers",
            Commands = new TLioScript<JToken> { new Set<JToken>("@", new DoubleCurrent()) }
        };

        var result = command.Execute(data, executeOptions);

        Assert.That(result.Success, Is.True);
        Assert.That(result.Data.SelectToken("$.numbers")!.Select(t => t.Value<int>()),
            Is.EqualTo(new[] { 2, 4, 6 }));
    }

    [Test]
    public void AtDotField_WritesOnlyThatFieldOnTheCurrentElement()
    {
        var data = JToken.Parse("""{ "items": [{"id":1},{"id":2}] }""");
        var command = new ForEach<JToken>
        {
            Path = "$.items",
            Commands = new TLioScript<JToken>
            {
                new Add<JToken>("@.status", new FixedValue<JToken>(new JValue("done")))
            }
        };

        var result = command.Execute(data, executeOptions);

        Assert.That(result.Success, Is.True);
        Assert.That(result.Data.SelectToken("$.items[0].status")!.Value<string>(), Is.EqualTo("done"));
        Assert.That(result.Data.SelectToken("$.items[1].status")!.Value<string>(), Is.EqualTo("done"));
    }

    [Test]
    public void StateElsewhereAccumulatesWhileReadingTheCurrentElement()
    {
        // Path targets an unrelated absolute location ($.total) while Value reads "@" — the one
        // case that needs context.CurrentNode to override what the command's own target would
        // otherwise pass as Value's currentNode.
        var data = JToken.Parse("""{ "items": [1,2,3,4], "total": 0 }""");
        var command = new ForEach<JToken>
        {
            Path = "$.items",
            Commands = new TLioScript<JToken>
            {
                new Set<JToken>("$.total", new AddCurrentToTotal())
            }
        };

        var result = command.Execute(data, executeOptions);

        Assert.That(result.Success, Is.True);
        Assert.That(result.Data.SelectToken("$.total")!.Value<int>(), Is.EqualTo(10)); // 1+2+3+4
    }

    [Test]
    public void EmptyArray_IsNoOpAndSucceeds()
    {
        var data = JToken.Parse("""{ "items": [] }""");
        var command = new ForEach<JToken>
        {
            Path = "$.items",
            Commands = new TLioScript<JToken> { new Add<JToken>("@.x", new FixedValue<JToken>(new JValue(1))) }
        };

        var result = command.Execute(data, executeOptions);

        Assert.That(result.Success, Is.True);
    }

    [Test]
    public void MissingPath_FailsValidation()
    {
        var data = JToken.Parse("""{}""");
        var command = new ForEach<JToken>();

        var result = command.Execute(data, executeOptions);

        Assert.That(result.Success, Is.False);
    }

    [Test]
    public void PathNotAnArray_IsNoOpAndSucceeds()
    {
        var data = JToken.Parse("""{ "items": { "not": "an array" } }""");
        var command = new ForEach<JToken>
        {
            Path = "$.items",
            Commands = new TLioScript<JToken> { new Add<JToken>("@.x", new FixedValue<JToken>(new JValue(1))) }
        };

        var result = command.Execute(data, executeOptions);

        Assert.That(result.Success, Is.True);
    }

    [Test]
    public void MaxIterations_TruncatesTheRun()
    {
        var data = JToken.Parse("""{ "items": [{},{},{},{},{}] }""");
        var command = new ForEach<JToken>
        {
            Path = "$.items",
            MaxIterations = 2,
            Commands = new TLioScript<JToken> { new Add<JToken>("@.x", new FixedValue<JToken>(new JValue(1))) }
        };

        var result = command.Execute(data, executeOptions);

        Assert.That(result.Success, Is.True);
        Assert.That(result.Data.SelectTokens("$.items[*].x").Count(), Is.EqualTo(2));
    }

    [Test]
    public void AFailingIterationStopsTheRemainingOnes()
    {
        var data = JToken.Parse("""{ "items": [1,2,3] }""");
        var command = new ForEach<JToken>
        {
            Path = "$.items",
            Commands = new TLioScript<JToken> { new IfElse<JToken>() } // missing condition -> fails
        };

        var result = command.Execute(data, executeOptions);

        Assert.That(result.Success, Is.False);
    }

    [Test]
    public void CurrentNodeIsRestoredAfterTheLoopCompletes()
    {
        var data = JToken.Parse("""{ "items": [1,2] }""");
        var command = new ForEach<JToken>
        {
            Path = "$.items",
            Commands = new TLioScript<JToken> { new Add<JToken>("@.touched", new FixedValue<JToken>(new JValue(true))) }
        };

        command.Execute(data, executeOptions);

        Assert.That(executeOptions.CurrentNode, Is.Null);
    }

    [Test]
    public void NestedForEach_InnerLoopDoesNotClobberTheOuterCurrentNode()
    {
        // Outer iterates groups; inner iterates each group's own members (proving "@.members" —
        // relative to the OUTER current node — resolves correctly as the inner loop's own Path).
        // After the inner forEach returns, the outer body's own next command's "@" must still
        // land on the outer element (proving CurrentNode is restored, not left as the last member).
        var data = JToken.Parse("""
            { "groups": [
                { "name": "a", "members": [{"id":1},{"id":2}] },
                { "name": "b", "members": [{"id":1},{"id":2},{"id":3}] }
              ] }
            """);

        var outer = new ForEach<JToken>
        {
            Path = "$.groups",
            Commands = new TLioScript<JToken>
            {
                new ForEach<JToken>
                {
                    Path = "@.members",
                    Commands = new TLioScript<JToken>
                    {
                        new Add<JToken>("@.touched", new FixedValue<JToken>(new JValue(true)))
                    }
                },
                new Add<JToken>("@.done", new FixedValue<JToken>(new JValue(true)))
            }
        };

        var result = outer.Execute(data, executeOptions);

        Assert.That(result.Success, Is.True);
        Assert.That(result.Data.SelectToken("$.groups[0].done")!.Value<bool>(), Is.True);
        Assert.That(result.Data.SelectToken("$.groups[1].done")!.Value<bool>(), Is.True);
        Assert.That(result.Data.SelectTokens("$.groups[0].members[*].touched").Count(), Is.EqualTo(2));
        Assert.That(result.Data.SelectTokens("$.groups[1].members[*].touched").Count(), Is.EqualTo(3));
    }

    /// <summary>Writes 2x the current node's own numeric value.</summary>
    private sealed class DoubleCurrent : IFunctionSupportedValue<JToken>
    {
        public FunctionResult<JToken> GetValue(JToken currentNode, JToken dataContext, IExecutionContext<JToken> context) =>
            FunctionResult<JToken>.Successful(new JValue(currentNode.Value<int>() * 2));
        public string ToScript() => "[double-current]";
    }

    /// <summary>Ignores its own target ($.total's current value) and instead reads context.CurrentNode.</summary>
    private sealed class AddCurrentToTotal : IFunctionSupportedValue<JToken>
    {
        public FunctionResult<JToken> GetValue(JToken currentNode, JToken dataContext, IExecutionContext<JToken> context) =>
            FunctionResult<JToken>.Successful(new JValue(
                dataContext.SelectToken("$.total")!.Value<int>() + currentNode.Value<int>()));
        public string ToScript() => "[add-current-to-total]";
    }
}
