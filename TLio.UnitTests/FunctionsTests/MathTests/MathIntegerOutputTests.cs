using Newtonsoft.Json.Linq;
using NUnit.Framework;
using TLio.Core.Models;
using TLio.Extensions.Math;
using TLio.Json;

namespace TLio.UnitTests.FunctionsTests.MathTests;

/// <summary>
/// [!] Hard requirement: integer vs double output must match JLio exactly.
///
/// CreateNumericResult produces a JTokenType.Integer (long) for whole-number results
/// and JTokenType.Float for fractional results, mirroring JLio's MathHelper.CreateNumericValue.
///
/// Ported from JLio.UnitTests.Math.MathIntegerOutputTests.
/// </summary>
[TestFixture]
public class MathIntegerOutputTests
{
    private JToken data = null!;
    private TLio.Core.Contracts.IExecutionContext<JToken> context = null!;

    [SetUp]
    public void Setup()
    {
        context = JsonExecutionContext.CreateDefault();
        data = JToken.Parse(@"{
            ""int_val"": 4,
            ""frac_val"": 4.5,
            ""arr_int"": [2, 4, 6],
            ""arr_frac"": [1, 2]
        }");
    }

    [Test] public void Sum_WholeNumber_ProducesInteger()
    {
        var fn = new Sum<JToken>();
        fn.SetArguments(new Arguments<JToken> { new PathValue<JToken>("$.int_val") });
        var result = fn.Execute(data, data, context);
        Assert.That(result.Success, Is.True);
        Assert.That(result.Data.First!.Type, Is.EqualTo(JTokenType.Integer));
    }

    [Test] public void Sum_FractionalNumber_ProducesFloat()
    {
        var fn = new Sum<JToken>();
        fn.SetArguments(new Arguments<JToken> { new PathValue<JToken>("$.frac_val") });
        var result = fn.Execute(data, data, context);
        Assert.That(result.Success, Is.True);
        Assert.That(result.Data.First!.Type, Is.EqualTo(JTokenType.Float));
    }

    [Test] public void Avg_WholeResult_ProducesInteger()
    {
        // avg([2,4,6]) = 4 — whole number
        var fn = new Avg<JToken>();
        fn.SetArguments(new Arguments<JToken> { new PathValue<JToken>("$.arr_int") });
        var result = fn.Execute(data, data, context);
        Assert.That(result.Success, Is.True);
        Assert.That(result.Data.First!.Type, Is.EqualTo(JTokenType.Integer));
        Assert.That(result.Data.First!.Value<long>(), Is.EqualTo(4));
    }

    [Test] public void Avg_FractionalResult_ProducesFloat()
    {
        // avg([1,2]) = 1.5 — fractional
        var fn = new Avg<JToken>();
        fn.SetArguments(new Arguments<JToken> { new PathValue<JToken>("$.arr_frac") });
        var result = fn.Execute(data, data, context);
        Assert.That(result.Success, Is.True);
        Assert.That(result.Data.First!.Type, Is.EqualTo(JTokenType.Float));
        Assert.That(result.Data.First!.Value<double>(), Is.EqualTo(1.5));
    }

    [Test] public void Sqrt_WholeResult_ProducesInteger()
    {
        // sqrt(4) = 2 — whole number
        var fn = new Sqrt<JToken>();
        fn.SetArguments(new Arguments<JToken> { new PathValue<JToken>("$.int_val") });
        var result = fn.Execute(data, data, context);
        Assert.That(result.Success, Is.True);
        Assert.That(result.Data.First!.Type, Is.EqualTo(JTokenType.Integer));
        Assert.That(result.Data.First!.Value<long>(), Is.EqualTo(2));
    }

    [Test] public void Sqrt_FractionalResult_ProducesFloat()
    {
        // sqrt(4.5) ≈ 2.121... — fractional
        var fn = new Sqrt<JToken>();
        fn.SetArguments(new Arguments<JToken> { new PathValue<JToken>("$.frac_val") });
        var result = fn.Execute(data, data, context);
        Assert.That(result.Success, Is.True);
        Assert.That(result.Data.First!.Type, Is.EqualTo(JTokenType.Float));
    }

    [Test] public void Count_AlwaysProducesInteger()
    {
        var fn = new Count<JToken>();
        fn.SetArguments(new Arguments<JToken> { new PathValue<JToken>("$.arr_int") });
        var result = fn.Execute(data, data, context);
        Assert.That(result.Success, Is.True);
        Assert.That(result.Data.First!.Type, Is.EqualTo(JTokenType.Integer));
    }

    [Test] public void CountIf_AlwaysProducesInteger()
    {
        var condData = JToken.Parse(@"{ ""nums"": [1,2,3,4,5], ""crit"": "">3"" }");
        var fn = new CountIf<JToken>();
        fn.SetArguments(new Arguments<JToken>
        {
            new PathValue<JToken>("$.nums"),
            new PathValue<JToken>("$.crit")
        });
        var result = fn.Execute(condData, condData, context);
        Assert.That(result.Success, Is.True);
        Assert.That(result.Data.First!.Type, Is.EqualTo(JTokenType.Integer));
    }
}
