using Newtonsoft.Json.Linq;
using NUnit.Framework;
using TLio.Commands;
using TLio.Core.Contracts;
using TLio.Core.Models;
using TLio.Functions;
using TLio.Json;

namespace TLio.Functions.Tests.FunctionsTests;

[TestFixture]
public class ToArrayTests
{
    private JToken data = null!;
    private IExecutionContext<JToken> executeOptions = null!;

    [SetUp]
    public void Setup()
    {
        executeOptions = JsonExecutionContext.CreateDefault();
        data = JToken.Parse(
            "{ \"tag\": \"red\", \"tags\": [\"red\", \"blue\"], \"missingTag\": null, \"result\": null }");
    }

    [Test]
    public void WrapsScalarAsFirstElement()
    {
        var fn = (IFunction<JToken>)new ToArray<JToken>()
            .SetArguments(new Arguments<JToken> { new FixedValue<JToken>(new JValue("$.tag")) });
        var script = new TLioScript<JToken>
        {
            new Set<JToken>("$.result", new FunctionSupportedValue<JToken>(fn))
        };
        var result = script.Execute(data, executeOptions);

        Assert.That(result.Success, Is.True);
        Assert.That(JToken.DeepEquals(result.Data.SelectToken("$.result"), JArray.Parse("[\"red\"]")), Is.True);
    }

    [Test]
    public void LeavesAnExistingArrayUnchanged()
    {
        var fn = (IFunction<JToken>)new ToArray<JToken>()
            .SetArguments(new Arguments<JToken> { new FixedValue<JToken>(new JValue("$.tags")) });
        var result = fn.Execute(data, data, executeOptions);

        Assert.That(result.Success, Is.True);
        Assert.That(JToken.DeepEquals(result.Data.First, JArray.Parse("[\"red\",\"blue\"]")), Is.True);
    }

    [Test]
    public void ReturnsEmptyArrayForNullValue()
    {
        var fn = (IFunction<JToken>)new ToArray<JToken>()
            .SetArguments(new Arguments<JToken> { new FixedValue<JToken>(new JValue("$.missingTag")) });
        var result = fn.Execute(data, data, executeOptions);

        Assert.That(result.Success, Is.True);
        Assert.That(JToken.DeepEquals(result.Data.First, new JArray()), Is.True);
    }

    [Test]
    public void ReturnsEmptyArrayForNonExistentPath()
    {
        var fn = (IFunction<JToken>)new ToArray<JToken>()
            .SetArguments(new Arguments<JToken> { new FixedValue<JToken>(new JValue("$.nonExistent")) });
        var result = fn.Execute(data, data, executeOptions);

        Assert.That(result.Success, Is.True);
        Assert.That(JToken.DeepEquals(result.Data.First, new JArray()), Is.True);
    }

    [Test]
    public void ReturnsFalseWithTooManyArguments()
    {
        var fn = (IFunction<JToken>)new ToArray<JToken>()
            .SetArguments(new Arguments<JToken>
            {
                new FixedValue<JToken>(new JValue("$.tag")),
                new FixedValue<JToken>(new JValue("extra"))
            });
        var result = fn.Execute(data, data, executeOptions);

        Assert.That(result.Success, Is.False);
    }

    [Test]
    public void WrapsTheCurrentNodeWhenCalledWithNoArguments()
    {
        var fn = new ToArray<JToken>();
        var result = fn.Execute(new JValue("red"), data, executeOptions);

        Assert.That(result.Success, Is.True);
        Assert.That(JToken.DeepEquals(result.Data.First, JArray.Parse("[\"red\"]")), Is.True);
    }

    [Test]
    public void WrapsEachMatchedItemOverAWildcard()
    {
        // "$.tags[*]" is a whole-node selector: each element IS the current node for that
        // match, so a bare toArray() wraps each tag individually rather than the parent.
        var script = new TLioScript<JToken>
        {
            new Set<JToken>("$.tags[*]", new FunctionSupportedValue<JToken>(new ToArray<JToken>()))
        };
        var result = script.Execute(data, executeOptions);

        Assert.That(result.Success, Is.True);
        Assert.That(JToken.DeepEquals(result.Data.SelectToken("$.tags"), JArray.Parse("[[\"red\"],[\"blue\"]]")), Is.True);
    }

    [Test]
    public void DoesNotMutateTheSourceNode()
    {
        var fn = (IFunction<JToken>)new ToArray<JToken>()
            .SetArguments(new Arguments<JToken> { new FixedValue<JToken>(new JValue("$.tags")) });
        fn.Execute(data, data, executeOptions);

        Assert.That(JToken.DeepEquals(data.SelectToken("$.tags"), JArray.Parse("[\"red\",\"blue\"]")), Is.True);
    }
}
