using Microsoft.Extensions.Logging;
using NUnit.Framework;
using TLio.Client;
using TLio.Core.Contracts;
using TLio.Yaml;
using YamlDotNet.RepresentationModel;

namespace TLio.Yaml.Tests.Commands;

/// <summary>
/// A null-valued node (an explicit <c>null</c> or <c>~</c>) is an unfilled container: the
/// structure-creating commands (add, put) build into it — upgrading the null to the object or
/// array the path describes — exactly as they build into XML's empty element and into a JSON
/// null. set stays strict and non-null scalars still refuse.
///
/// Note: a bare <c>key:</c> (no value) currently reads as an empty string, not null, because
/// <see cref="YamlNodeAdapter.IsNull"/> tests the text and YamlDotNet hands back "" for an
/// absent value — so the upgrade does not fire for it. The YAML spec reads a plain empty
/// scalar as null; aligning that is a YamlNodeAdapter decision, tracked separately.
/// </summary>
[TestFixture]
public class NullContainerUpgradeTests
{
    private ScriptEngine<YamlNode> _engine = null!;

    [SetUp]
    public void SetUp()
    {
        var options = ParseOptions<YamlNode>.CreateDefault();
        _engine = new ScriptEngine<YamlNode>(options.CommandsProvider, options.FunctionsProvider);
    }

    private (YamlNode Data, IExecutionContext<YamlNode> Context, bool Success) Run(string input, string script)
    {
        var context = YamlExecutionContext.CreateDefault();
        var result  = _engine.Execute(script, context.NodeAdapter.Parse(input), context);
        return (result.Data!, context, result.Success);
    }

    private static bool HasPrimitiveWarning(IExecutionContext<YamlNode> context) =>
        context.GetLogEntries().Any(e => e.Level == LogLevel.Warning &&
            e.Message.Contains("cannot add property to a primitive node"));

    [TestCase("customer: null")]
    [TestCase("customer: ~")]
    public void Add_IntoNullValue_UpgradesTheNullToAMapping(string input)
    {
        var (data, context, success) = Run(input,
            """[{"command":"add","path":"$.customer.demo","value":3}]""");

        Assert.That(success, Is.True);
        var demo = context.ItemsFetcher.SelectNode("$.customer.demo", data);
        Assert.That(demo, Is.Not.Null);
        Assert.That(context.NodeAdapter.TryGetString(demo!), Is.EqualTo("3"));
        Assert.That(HasPrimitiveWarning(context), Is.False,
            "a null node is an unfilled container, not a primitive");
    }

    [Test]
    public void Put_IntoNullValue_UpgradesTheNullToAMapping()
    {
        var (data, context, success) = Run("customer: null",
            """[{"command":"put","path":"$.customer.demo","value":3}]""");

        Assert.That(success, Is.True);
        var demo = context.ItemsFetcher.SelectNode("$.customer.demo", data);
        Assert.That(context.NodeAdapter.TryGetString(demo!), Is.EqualTo("3"));
    }

    [Test]
    public void Add_DeepPathThroughNull_BuildsTheWholePath()
    {
        var (data, context, success) = Run("a: null",
            """[{"command":"add","path":"$.a.b.c","value":3}]""");

        Assert.That(success, Is.True);
        var c = context.ItemsFetcher.SelectNode("$.a.b.c", data);
        Assert.That(c, Is.Not.Null);
        Assert.That(context.NodeAdapter.TryGetString(c!), Is.EqualTo("3"));
    }

    [Test]
    public void Add_ComplexValueIntoNullValue_LandsIntact()
    {
        var (data, context, success) = Run("a: null",
            """[{"command":"add","path":"$.a.addr","value":{"city":"A"}}]""");

        Assert.That(success, Is.True);
        var city = context.ItemsFetcher.SelectNode("$.a.addr.city", data);
        Assert.That(context.NodeAdapter.TryGetString(city!), Is.EqualTo("A"));
    }

    [Test]
    public void Add_AtIndexZeroOfNullValue_UpgradesTheNullToASequence()
    {
        var (data, context, success) = Run("a: null",
            """[{"command":"add","path":"$.a[0]","value":1}]""");

        Assert.That(success, Is.True);
        var a = context.ItemsFetcher.SelectNode("$.a", data);
        Assert.That(a, Is.InstanceOf<YamlSequenceNode>());
        Assert.That(context.NodeAdapter.TryGetString(
            context.ItemsFetcher.SelectNode("$.a[0]", data)!), Is.EqualTo("1"));
    }

    [Test]
    public void Add_AtIndexOneOfNullValue_WarnsAndChangesNothing()
    {
        var (data, context, _) = Run("a: null",
            """[{"command":"add","path":"$.a[1]","value":1}]""");

        var a = context.ItemsFetcher.SelectNode("$.a", data);
        Assert.That(context.NodeAdapter.IsNull(a!), Is.True,
            "position 0 is the only element that can go into an unfilled container");
        Assert.That(context.GetLogEntries().Any(e => e.Level == LogLevel.Warning), Is.True);
    }

    [Test]
    public void Set_IntoNullValue_StaysStrictAndChangesNothing()
    {
        var (data, context, _) = Run("a: null",
            """[{"command":"set","path":"$.a.b","value":3}]""");

        var a = context.ItemsFetcher.SelectNode("$.a", data);
        Assert.That(context.NodeAdapter.IsNull(a!), Is.True,
            "set never invents structure, not even through a null");
        Assert.That(context.ItemsFetcher.SelectNode("$.a.b", data), Is.Null);
    }

    [Test]
    public void Add_IntoNonNullScalar_StillWarnsPrimitive()
    {
        var (data, context, _) = Run("a: x",
            """[{"command":"add","path":"$.a.b","value":3}]""");

        var a = context.ItemsFetcher.SelectNode("$.a", data);
        Assert.That(context.NodeAdapter.TryGetString(a!), Is.EqualTo("x"));
        Assert.That(HasPrimitiveWarning(context), Is.True,
            "a scalar that holds a value is not an unfilled container");
    }
}
