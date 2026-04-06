using Newtonsoft.Json.Linq;
using NUnit.Framework;
using TLio.Client;
using TLio.Extensions.ETL;
using TLio.Json;
using TLio.UnitTests.Fixtures;

namespace TLio.UnitTests;

/// <summary>
/// Fixture-driven tests for ETL commands (flatten, restore, toCsv, resolve).
/// Uses ParseOptions.CreateDefault() + RegisterETL to load the extension pack,
/// verifying POCO settings deserialization (FlattenSettings, CsvSettings, etc.)
/// via CommandConverter.
/// </summary>
[TestFixture]
public class EtlFixtureTests
{
    private ScriptEngine<JToken> _engine = null!;

    [SetUp]
    public void SetUp()
    {
        var options = ParseOptions<JToken>.CreateDefault();
        options.CommandsProvider.RegisterETL<JToken>();
        _engine = new ScriptEngine<JToken>(options.CommandsProvider, options.FunctionsProvider);
    }

    [TestCaseSource(typeof(FixtureTheoryLoader), nameof(FixtureTheoryLoader.Load), new object[] { "Flatten" })]
    public void Flatten(JToken input, string script, JToken expected)
        => RunFixture(input, script, expected);

    private void RunFixture(JToken input, string script, JToken expected)
    {
        var context = JsonExecutionContext.CreateDefault();
        var result = _engine.Execute(script, input, context);

        Assert.That(result.Success, Is.True,
            $"Engine reported failure. Log:\n{string.Join("\n", context.GetLogEntries().Select(e => $"  [{e.Level}] {e.Group}: {e.Message}"))}");

        Assert.That(JToken.DeepEquals(result.Data, expected), Is.True,
            $"Result mismatch.\n  Expected: {expected}\n  Actual:   {result.Data}");
    }
}
