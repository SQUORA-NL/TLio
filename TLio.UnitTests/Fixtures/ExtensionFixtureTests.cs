using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using NUnit.Framework;
using TLio.Client;
using TLio.Extensions.ETL;
using TLio.Extensions.Math;
using TLio.Extensions.Text;
using TLio.Extensions.TimeDate;
using TLio.Json;

namespace TLio.UnitTests.Fixtures;

/// <summary>
/// Data-driven tests for extension-pack functions (Math, Text, TimeDate, ETL).
/// Each test case is a single JSON file: { "input": {...}, "script": [...], "result": {...} }
/// Files live under TLio.UnitTests/Fixtures/Math/, Text/, TimeDate/.
///
/// Uses an extended ParseOptions that registers all extension packs in addition
/// to the default built-in commands and functions.
/// </summary>
[TestFixture]
public class ExtensionFixtureTests
{
    private ScriptEngine<JToken> _engine = null!;

    [SetUp]
    public void SetUp()
    {
        var options = ParseOptions<JToken>.CreateDefault();
        options.FunctionsProvider.RegisterMath<JToken>();
        options.FunctionsProvider.RegisterText<JToken>();
        options.FunctionsProvider.RegisterTimeDate<JToken>();
        options.CommandsProvider.RegisterETL<JToken>();
        _engine = new ScriptEngine<JToken>(options.CommandsProvider, options.FunctionsProvider);
    }

    // ── Math ──────────────────────────────────────────────────────────────────

    [TestCaseSource(typeof(FixtureTheoryLoader), nameof(FixtureTheoryLoader.LoadSingle), new object[] { "Math/abs" })]
    public void Math_Abs(JToken input, string script, JToken expected) => RunFixture(input, script, expected);

    [TestCaseSource(typeof(FixtureTheoryLoader), nameof(FixtureTheoryLoader.LoadSingle), new object[] { "Math/avg" })]
    public void Math_Avg(JToken input, string script, JToken expected) => RunFixture(input, script, expected);

    [TestCaseSource(typeof(FixtureTheoryLoader), nameof(FixtureTheoryLoader.LoadSingle), new object[] { "Math/averageif" })]
    public void Math_AverageIf(JToken input, string script, JToken expected) => RunFixture(input, script, expected);

    [TestCaseSource(typeof(FixtureTheoryLoader), nameof(FixtureTheoryLoader.LoadSingle), new object[] { "Math/averageifs" })]
    public void Math_AverageIfs(JToken input, string script, JToken expected) => RunFixture(input, script, expected);

    [TestCaseSource(typeof(FixtureTheoryLoader), nameof(FixtureTheoryLoader.LoadSingle), new object[] { "Math/calculate" })]
    public void Math_Calculate(JToken input, string script, JToken expected) => RunFixture(input, script, expected);

    [TestCaseSource(typeof(FixtureTheoryLoader), nameof(FixtureTheoryLoader.LoadSingle), new object[] { "Math/ceiling" })]
    public void Math_Ceiling(JToken input, string script, JToken expected) => RunFixture(input, script, expected);

    [TestCaseSource(typeof(FixtureTheoryLoader), nameof(FixtureTheoryLoader.LoadSingle), new object[] { "Math/count" })]
    public void Math_Count(JToken input, string script, JToken expected) => RunFixture(input, script, expected);

    [TestCaseSource(typeof(FixtureTheoryLoader), nameof(FixtureTheoryLoader.LoadSingle), new object[] { "Math/countif" })]
    public void Math_CountIf(JToken input, string script, JToken expected) => RunFixture(input, script, expected);

    [TestCaseSource(typeof(FixtureTheoryLoader), nameof(FixtureTheoryLoader.LoadSingle), new object[] { "Math/countifs" })]
    public void Math_CountIfs(JToken input, string script, JToken expected) => RunFixture(input, script, expected);

    [TestCaseSource(typeof(FixtureTheoryLoader), nameof(FixtureTheoryLoader.LoadSingle), new object[] { "Math/floor" })]
    public void Math_Floor(JToken input, string script, JToken expected) => RunFixture(input, script, expected);

    [TestCaseSource(typeof(FixtureTheoryLoader), nameof(FixtureTheoryLoader.LoadSingle), new object[] { "Math/max" })]
    public void Math_Max(JToken input, string script, JToken expected) => RunFixture(input, script, expected);

    [TestCaseSource(typeof(FixtureTheoryLoader), nameof(FixtureTheoryLoader.LoadSingle), new object[] { "Math/maxifs" })]
    public void Math_MaxIfs(JToken input, string script, JToken expected) => RunFixture(input, script, expected);

    [TestCaseSource(typeof(FixtureTheoryLoader), nameof(FixtureTheoryLoader.LoadSingle), new object[] { "Math/median" })]
    public void Math_Median(JToken input, string script, JToken expected) => RunFixture(input, script, expected);

    [TestCaseSource(typeof(FixtureTheoryLoader), nameof(FixtureTheoryLoader.LoadSingle), new object[] { "Math/min" })]
    public void Math_Min(JToken input, string script, JToken expected) => RunFixture(input, script, expected);

    [TestCaseSource(typeof(FixtureTheoryLoader), nameof(FixtureTheoryLoader.LoadSingle), new object[] { "Math/minifs" })]
    public void Math_MinIfs(JToken input, string script, JToken expected) => RunFixture(input, script, expected);

    [TestCaseSource(typeof(FixtureTheoryLoader), nameof(FixtureTheoryLoader.LoadSingle), new object[] { "Math/modulo" })]
    public void Math_Modulo(JToken input, string script, JToken expected) => RunFixture(input, script, expected);

    [TestCaseSource(typeof(FixtureTheoryLoader), nameof(FixtureTheoryLoader.LoadSingle), new object[] { "Math/pow" })]
    public void Math_Pow(JToken input, string script, JToken expected) => RunFixture(input, script, expected);

    [TestCaseSource(typeof(FixtureTheoryLoader), nameof(FixtureTheoryLoader.LoadSingle), new object[] { "Math/round" })]
    public void Math_Round(JToken input, string script, JToken expected) => RunFixture(input, script, expected);

    [TestCaseSource(typeof(FixtureTheoryLoader), nameof(FixtureTheoryLoader.LoadSingle), new object[] { "Math/sqrt" })]
    public void Math_Sqrt(JToken input, string script, JToken expected) => RunFixture(input, script, expected);

    [TestCaseSource(typeof(FixtureTheoryLoader), nameof(FixtureTheoryLoader.LoadSingle), new object[] { "Math/subtract" })]
    public void Math_Subtract(JToken input, string script, JToken expected) => RunFixture(input, script, expected);

    [TestCaseSource(typeof(FixtureTheoryLoader), nameof(FixtureTheoryLoader.LoadSingle), new object[] { "Math/sum" })]
    public void Math_Sum(JToken input, string script, JToken expected) => RunFixture(input, script, expected);

    [TestCaseSource(typeof(FixtureTheoryLoader), nameof(FixtureTheoryLoader.LoadSingle), new object[] { "Math/sumif" })]
    public void Math_SumIf(JToken input, string script, JToken expected) => RunFixture(input, script, expected);

    [TestCaseSource(typeof(FixtureTheoryLoader), nameof(FixtureTheoryLoader.LoadSingle), new object[] { "Math/sumifs" })]
    public void Math_SumIfs(JToken input, string script, JToken expected) => RunFixture(input, script, expected);

    // ── Text ──────────────────────────────────────────────────────────────────

    [TestCaseSource(typeof(FixtureTheoryLoader), nameof(FixtureTheoryLoader.LoadSingle), new object[] { "Text/toupper" })]
    public void Text_ToUpper(JToken input, string script, JToken expected) => RunFixture(input, script, expected);

    [TestCaseSource(typeof(FixtureTheoryLoader), nameof(FixtureTheoryLoader.LoadSingle), new object[] { "Text/tolower" })]
    public void Text_ToLower(JToken input, string script, JToken expected) => RunFixture(input, script, expected);

    [TestCaseSource(typeof(FixtureTheoryLoader), nameof(FixtureTheoryLoader.LoadSingle), new object[] { "Text/concat" })]
    public void Text_Concat(JToken input, string script, JToken expected) => RunFixture(input, script, expected);

    [TestCaseSource(typeof(FixtureTheoryLoader), nameof(FixtureTheoryLoader.LoadSingle), new object[] { "Text/contains" })]
    public void Text_Contains(JToken input, string script, JToken expected) => RunFixture(input, script, expected);

    [TestCaseSource(typeof(FixtureTheoryLoader), nameof(FixtureTheoryLoader.LoadSingle), new object[] { "Text/endswith" })]
    public void Text_EndsWith(JToken input, string script, JToken expected) => RunFixture(input, script, expected);

    [TestCaseSource(typeof(FixtureTheoryLoader), nameof(FixtureTheoryLoader.LoadSingle), new object[] { "Text/format" })]
    public void Text_Format(JToken input, string script, JToken expected) => RunFixture(input, script, expected);

    [TestCaseSource(typeof(FixtureTheoryLoader), nameof(FixtureTheoryLoader.LoadSingle), new object[] { "Text/indexof" })]
    public void Text_IndexOf(JToken input, string script, JToken expected) => RunFixture(input, script, expected);

    [TestCaseSource(typeof(FixtureTheoryLoader), nameof(FixtureTheoryLoader.LoadSingle), new object[] { "Text/isempty" })]
    public void Text_IsEmpty(JToken input, string script, JToken expected) => RunFixture(input, script, expected);

    [TestCaseSource(typeof(FixtureTheoryLoader), nameof(FixtureTheoryLoader.LoadSingle), new object[] { "Text/join" })]
    public void Text_Join(JToken input, string script, JToken expected) => RunFixture(input, script, expected);

    [TestCaseSource(typeof(FixtureTheoryLoader), nameof(FixtureTheoryLoader.LoadSingle), new object[] { "Text/length" })]
    public void Text_Length(JToken input, string script, JToken expected) => RunFixture(input, script, expected);

    [TestCaseSource(typeof(FixtureTheoryLoader), nameof(FixtureTheoryLoader.LoadSingle), new object[] { "Text/padleft" })]
    public void Text_PadLeft(JToken input, string script, JToken expected) => RunFixture(input, script, expected);

    [TestCaseSource(typeof(FixtureTheoryLoader), nameof(FixtureTheoryLoader.LoadSingle), new object[] { "Text/padright" })]
    public void Text_PadRight(JToken input, string script, JToken expected) => RunFixture(input, script, expected);

    [TestCaseSource(typeof(FixtureTheoryLoader), nameof(FixtureTheoryLoader.LoadSingle), new object[] { "Text/parse" })]
    public void Text_Parse(JToken input, string script, JToken expected) => RunFixture(input, script, expected);

    [TestCaseSource(typeof(FixtureTheoryLoader), nameof(FixtureTheoryLoader.LoadSingle), new object[] { "Text/replace" })]
    public void Text_Replace(JToken input, string script, JToken expected) => RunFixture(input, script, expected);

    [TestCaseSource(typeof(FixtureTheoryLoader), nameof(FixtureTheoryLoader.LoadSingle), new object[] { "Text/split" })]
    public void Text_Split(JToken input, string script, JToken expected) => RunFixture(input, script, expected);

    [TestCaseSource(typeof(FixtureTheoryLoader), nameof(FixtureTheoryLoader.LoadSingle), new object[] { "Text/startswith" })]
    public void Text_StartsWith(JToken input, string script, JToken expected) => RunFixture(input, script, expected);

    [TestCaseSource(typeof(FixtureTheoryLoader), nameof(FixtureTheoryLoader.LoadSingle), new object[] { "Text/substring" })]
    public void Text_Substring(JToken input, string script, JToken expected) => RunFixture(input, script, expected);

    [TestCaseSource(typeof(FixtureTheoryLoader), nameof(FixtureTheoryLoader.LoadSingle), new object[] { "Text/trim" })]
    public void Text_Trim(JToken input, string script, JToken expected) => RunFixture(input, script, expected);

    [TestCaseSource(typeof(FixtureTheoryLoader), nameof(FixtureTheoryLoader.LoadSingle), new object[] { "Text/trimstart" })]
    public void Text_TrimStart(JToken input, string script, JToken expected) => RunFixture(input, script, expected);

    [TestCaseSource(typeof(FixtureTheoryLoader), nameof(FixtureTheoryLoader.LoadSingle), new object[] { "Text/trimend" })]
    public void Text_TrimEnd(JToken input, string script, JToken expected) => RunFixture(input, script, expected);

    // ── TimeDate ──────────────────────────────────────────────────────────────

    [TestCaseSource(typeof(FixtureTheoryLoader), nameof(FixtureTheoryLoader.LoadSingle), new object[] { "TimeDate/avgdate" })]
    public void TimeDate_AvgDate(JToken input, string script, JToken expected) => RunFixture(input, script, expected);

    [TestCaseSource(typeof(FixtureTheoryLoader), nameof(FixtureTheoryLoader.LoadSingle), new object[] { "TimeDate/datecompare" })]
    public void TimeDate_DateCompare(JToken input, string script, JToken expected) => RunFixture(input, script, expected);

    [TestCaseSource(typeof(FixtureTheoryLoader), nameof(FixtureTheoryLoader.LoadSingle), new object[] { "TimeDate/isdatebetween" })]
    public void TimeDate_IsDateBetween(JToken input, string script, JToken expected) => RunFixture(input, script, expected);

    [TestCaseSource(typeof(FixtureTheoryLoader), nameof(FixtureTheoryLoader.LoadSingle), new object[] { "TimeDate/mindate" })]
    public void TimeDate_MinDate(JToken input, string script, JToken expected) => RunFixture(input, script, expected);

    [TestCaseSource(typeof(FixtureTheoryLoader), nameof(FixtureTheoryLoader.LoadSingle), new object[] { "TimeDate/maxdate" })]
    public void TimeDate_MaxDate(JToken input, string script, JToken expected) => RunFixture(input, script, expected);

    // ── Helper ────────────────────────────────────────────────────────────────

    private void RunFixture(JToken input, string script, JToken expected)
    {
        var context = JsonExecutionContext.CreateDefault();
        var result = _engine.Execute(script, input, context);

        Assert.That(result.Success, Is.True,
            $"Engine reported failure. Log:\n{string.Join("\n", context.GetLogEntries().Select(e => $"  [{e.Level}] {e.Group}: {e.Message}"))}");

        Assert.That(JToken.DeepEquals(result.Data, expected), Is.True,
            $"Result mismatch.\n  Expected: {expected.ToString(Formatting.Indented)}\n  Actual:   {result.Data?.ToString(Formatting.Indented)}");
    }
}
