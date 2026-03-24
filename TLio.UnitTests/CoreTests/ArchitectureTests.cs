using Newtonsoft.Json.Linq;
using NUnit.Framework;
using TLio.Commands;
using TLio.Core.Contracts;
using TLio.Core.Models;
using TLio.Json;

namespace TLio.UnitTests.CoreTests;

/// <summary>
/// Smoke tests that verify the generic architecture wires up correctly.
/// These tests do NOT exercise real transformation logic (stubs are not yet
/// implemented). They exist to confirm that:
///   - The contracts compile and are instantiable.
///   - The execution context can be built for a specific TNode type.
///   - Commands can be constructed and validated.
/// </summary>
[TestFixture]
public class ArchitectureTests
{
    private IExecutionContext<JToken> _context = null!;
    private JToken _data = null!;

    [SetUp]
    public void SetUp()
    {
        _context = JsonExecutionContext.CreateDefault();
        _data = JToken.Parse("""{ "name": "TLio", "version": 1 }""");
    }

    [Test]
    public void ExecutionContext_IsCreatedWithJsonAdapters()
    {
        Assert.That(_context.ItemsFetcher, Is.Not.Null);
        Assert.That(_context.NodeAdapter, Is.Not.Null);
        Assert.That(_context.Logger, Is.Not.Null);
    }

    [Test]
    public void Set_ValidatesRequiredProperties()
    {
        var cmd = new Set<JToken>(); // no Path or Value set
        var result = cmd.ValidateCommandInstance();
        Assert.That(result.IsValid, Is.False);
        Assert.That(result.ValidationMessages, Has.Count.GreaterThan(0));
    }

    [Test]
    public void Set_WithValidConfig_PassesValidation()
    {
        var cmd = new Set<JToken>("$.name", new FixedValue<JToken>(new JValue("world")));
        var result = cmd.ValidateCommandInstance();
        Assert.That(result.IsValid, Is.True);
    }

    [Test]
    public void Remove_ValidatesRequiredPath()
    {
        var cmd = new Remove<JToken>();
        var result = cmd.ValidateCommandInstance();
        Assert.That(result.IsValid, Is.False);
    }

    [Test]
    public void Script_RunsCommandsInOrder()
    {
        // Demonstrates that a TLioScript<TNode> can be built and executed without errors.
        // The stub commands currently do nothing but log — the result should still be Success.
        var script = new TLioScript<JToken>
        {
            new Set<JToken>("$.name", new FixedValue<JToken>(new JValue("TLio")))
        };

        var result = script.Execute(_data, _context);
        Assert.That(result, Is.Not.Null);
    }

    [Test]
    public void JsonNodeAdapter_TypeChecks()
    {
        var adapter = new JsonNodeAdapter();
        var obj = JToken.Parse("""{ "a": 1 }""");
        var arr = JToken.Parse("[1, 2, 3]");
        var str = new JValue("hello");

        Assert.That(adapter.IsObject(obj), Is.True);
        Assert.That(adapter.IsArray(arr), Is.True);
        Assert.That(adapter.IsPrimitive(str), Is.True);
        Assert.That(adapter.IsNull(JValue.CreateNull()), Is.True);
    }
}
