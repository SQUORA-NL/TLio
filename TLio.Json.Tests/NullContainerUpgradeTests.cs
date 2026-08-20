using Microsoft.Extensions.Logging;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using NUnit.Framework;
using TLio.Client;
using TLio.Core.Contracts;

namespace TLio.Json.Tests;

/// <summary>
/// A null-valued node is an unfilled container: the structure-creating commands (add, put)
/// build into it — upgrading the null to the object or array the path describes — exactly as
/// they build into XML's empty element. set stays strict, non-null scalars still refuse, and
/// a null document root (which has no parent to swap it in) keeps the warning.
/// </summary>
[TestFixture]
public class NullContainerUpgradeTests
{
    private ScriptEngine<JToken> _engine = null!;

    [SetUp]
    public void SetUp()
    {
        var options = ParseOptions<JToken>.CreateDefault();
        _engine = new ScriptEngine<JToken>(options.CommandsProvider, options.FunctionsProvider);
    }

    private (string Output, IExecutionContext<JToken> Context, bool Success) Run(string input, string script)
    {
        var context = JsonExecutionContext.CreateDefault();
        var result  = _engine.Execute(script, JToken.Parse(input), context);
        return (result.Data!.ToString(Formatting.None), context, result.Success);
    }

    private static bool HasPrimitiveWarning(IExecutionContext<JToken> context) =>
        context.GetLogEntries().Any(e => e.Level == LogLevel.Warning &&
            e.Message.Contains("cannot add property to a primitive node"));

    [Test]
    public void Add_IntoNullProperty_UpgradesTheNullToAnObject()
    {
        var (output, context, success) = Run(
            """{"customer":null}""",
            """[{"command":"add","path":"$.customer.demo","value":3}]""");

        Assert.That(success, Is.True);
        Assert.That(output, Is.EqualTo("""{"customer":{"demo":3}}"""));
        Assert.That(HasPrimitiveWarning(context), Is.False,
            "a null node is an unfilled container, not a primitive");
    }

    [Test]
    public void Put_IntoNullProperty_UpgradesTheNullToAnObject()
    {
        var (output, _, success) = Run(
            """{"customer":null}""",
            """[{"command":"put","path":"$.customer.demo","value":3}]""");

        Assert.That(success, Is.True);
        Assert.That(output, Is.EqualTo("""{"customer":{"demo":3}}"""));
    }

    [Test]
    public void Add_DeepPathThroughNull_BuildsTheWholePath()
    {
        var (output, _, success) = Run(
            """{"a":null}""",
            """[{"command":"add","path":"$.a.b.c","value":3}]""");

        Assert.That(success, Is.True);
        Assert.That(output, Is.EqualTo("""{"a":{"b":{"c":3}}}"""));
    }

    [Test]
    public void Add_ComplexValueIntoNullProperty_LandsIntact()
    {
        var (output, _, success) = Run(
            """{"a":null}""",
            """[{"command":"add","path":"$.a.addr","value":{"city":"A"}}]""");

        Assert.That(success, Is.True);
        Assert.That(output, Is.EqualTo("""{"a":{"addr":{"city":"A"}}}"""));
    }

    [Test]
    public void Add_AtIndexZeroOfNullProperty_UpgradesTheNullToAnArray()
    {
        var (output, _, success) = Run(
            """{"a":null}""",
            """[{"command":"add","path":"$.a[0]","value":1}]""");

        Assert.That(success, Is.True);
        Assert.That(output, Is.EqualTo("""{"a":[1]}"""));
    }

    [Test]
    public void Add_AtIndexOneOfNullProperty_WarnsAndChangesNothing()
    {
        var (output, context, _) = Run(
            """{"a":null}""",
            """[{"command":"add","path":"$.a[1]","value":1}]""");

        Assert.That(output, Is.EqualTo("""{"a":null}"""),
            "position 0 is the only element that can go into an unfilled container");
        Assert.That(context.GetLogEntries().Any(e => e.Level == LogLevel.Warning), Is.True);
    }

    [Test]
    public void Set_IntoNullProperty_StaysStrictAndChangesNothing()
    {
        var (output, _, _) = Run(
            """{"a":null}""",
            """[{"command":"set","path":"$.a.b","value":3}]""");

        Assert.That(output, Is.EqualTo("""{"a":null}"""),
            "set never invents structure, not even through a null");
    }

    [Test]
    public void Add_IntoNonNullScalar_StillWarnsPrimitive()
    {
        var (output, context, _) = Run(
            """{"a":"x"}""",
            """[{"command":"add","path":"$.a.b","value":3}]""");

        Assert.That(output, Is.EqualTo("""{"a":"x"}"""));
        Assert.That(HasPrimitiveWarning(context), Is.True,
            "a scalar that holds a value is not an unfilled container");
    }

    [Test]
    public void Add_IntoNullDocumentRoot_WarnsBecauseTheRootHasNoParentToSwap()
    {
        var (output, context, _) = Run(
            "null",
            """[{"command":"add","path":"$.demo","value":3}]""");

        Assert.That(output, Is.EqualTo("null"));
        Assert.That(HasPrimitiveWarning(context), Is.True);
    }
}
