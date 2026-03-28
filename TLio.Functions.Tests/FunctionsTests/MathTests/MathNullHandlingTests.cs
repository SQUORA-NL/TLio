using Newtonsoft.Json.Linq;
using NUnit.Framework;
using TLio.Core.Models;
using TLio.Extensions.Math;
using TLio.Json;

namespace TLio.Functions.Tests.FunctionsTests.MathTests;

/// <summary>
/// [!] Hard requirement: null argument handling must match JLio exactly.
///
/// Contract (mirrors JLio MathFunctionBase):
///   - Path not found (Data.Count == 0) → failure
///   - Path found, value is null  → treat as 0, continue (success)
///   - Array with null elements   → each null element treated as 0
///
/// Ported from JLio.UnitTests.Math.MathNullHandlingTests.
/// </summary>
[TestFixture]
public class MathNullHandlingTests
{
    private JToken data = null!;
    private TLio.Core.Contracts.IExecutionContext<JToken> context = null!;

    [SetUp]
    public void Setup()
    {
        context = JsonExecutionContext.CreateDefault();
        data = JToken.Parse(@"{ ""nul"": null, ""nums"": [1, null, 3] }");
    }

    // ── Sum ──────────────────────────────────────────────────────────────────

    [Test] public void Sum_FoundNull_TreatedAsZero()
    {
        var fn = new Sum<JToken>();
        fn.SetArguments(new Arguments<JToken> { new PathValue<JToken>("$.nul") });
        var result = fn.Execute(data, data, context);
        Assert.That(result.Success, Is.True);
        Assert.That(result.Data.First!.Value<double>(), Is.EqualTo(0));
    }

    [Test] public void Sum_PathNotFound_ReturnsFailed()
    {
        var fn = new Sum<JToken>();
        fn.SetArguments(new Arguments<JToken> { new PathValue<JToken>("$.missing") });
        var result = fn.Execute(data, data, context);
        Assert.That(result.Success, Is.False);
    }

    [Test] public void Sum_ArrayWithNullElement_NullTreatedAsZero()
    {
        var fn = new Sum<JToken>();
        fn.SetArguments(new Arguments<JToken> { new PathValue<JToken>("$.nums") });
        var result = fn.Execute(data, data, context);
        Assert.That(result.Success, Is.True);
        Assert.That(result.Data.First!.Value<double>(), Is.EqualTo(4)); // 1 + 0 + 3
    }

    // ── Avg ──────────────────────────────────────────────────────────────────

    [Test] public void Avg_PathNotFound_ReturnsFailed()
    {
        var fn = new Avg<JToken>();
        fn.SetArguments(new Arguments<JToken> { new PathValue<JToken>("$.missing") });
        var result = fn.Execute(data, data, context);
        Assert.That(result.Success, Is.False);
    }

    // ── Abs ──────────────────────────────────────────────────────────────────

    [Test] public void Abs_FoundNull_TreatedAsZero()
    {
        var fn = new Abs<JToken>();
        fn.SetArguments(new Arguments<JToken> { new PathValue<JToken>("$.nul") });
        var result = fn.Execute(data, data, context);
        Assert.That(result.Success, Is.True);
        Assert.That(result.Data.First!.Value<double>(), Is.EqualTo(0));
    }

    [Test] public void Abs_PathNotFound_ReturnsFailed()
    {
        var fn = new Abs<JToken>();
        fn.SetArguments(new Arguments<JToken> { new PathValue<JToken>("$.missing") });
        var result = fn.Execute(data, data, context);
        Assert.That(result.Success, Is.False);
    }

    // ── Min / Max ─────────────────────────────────────────────────────────────

    [Test] public void Min_PathNotFound_ReturnsFailed()
    {
        var fn = new Min<JToken>();
        fn.SetArguments(new Arguments<JToken> { new PathValue<JToken>("$.missing") });
        var result = fn.Execute(data, data, context);
        Assert.That(result.Success, Is.False);
    }

    [Test] public void Max_PathNotFound_ReturnsFailed()
    {
        var fn = new Max<JToken>();
        fn.SetArguments(new Arguments<JToken> { new PathValue<JToken>("$.missing") });
        var result = fn.Execute(data, data, context);
        Assert.That(result.Success, Is.False);
    }

    // ── Count ─────────────────────────────────────────────────────────────────

    [Test] public void Count_FoundNull_CountsAsOne()
    {
        // null is a valid node that was found — it counts as 1 element
        var fn = new Count<JToken>();
        fn.SetArguments(new Arguments<JToken> { new PathValue<JToken>("$.nul") });
        var result = fn.Execute(data, data, context);
        Assert.That(result.Success, Is.True);
        Assert.That(result.Data.First!.Value<long>(), Is.EqualTo(1));
    }

    [Test] public void Count_PathNotFound_ReturnsZero()
    {
        // Count skips not-found — returns 0 (not failure)
        var fn = new Count<JToken>();
        fn.SetArguments(new Arguments<JToken> { new PathValue<JToken>("$.missing") });
        var result = fn.Execute(data, data, context);
        Assert.That(result.Success, Is.True);
        Assert.That(result.Data.First!.Value<long>(), Is.EqualTo(0));
    }
}
