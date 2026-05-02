using Microsoft.Extensions.Options;
using Newtonsoft.Json.Linq;
using NUnit.Framework;
using TLio.Mcp.Configuration;
using TLio.Mcp.Models;
using TLio.Mcp.Services;
using TLio.Mcp.Tools;

namespace TLio.Mcp.Tests;

/// <summary>
/// MCP-level execution quality for TLio's function library (Math, Text, TimeDate).
///
/// Function value syntax in TLio scripts: "=functionName(arg1, arg2, ...)"
/// Path args inside function calls are path strings (e.g. $.nums).
/// String literals use single quotes: 'value'.
/// Numbers and booleans are bare: 5, true, false.
/// </summary>
[TestFixture]
public sealed class McpFunctionCoverageTests
{
    private ExecutionTools _execution = null!;
    private RateLimiterService _rateLimiter = null!;

    [SetUp]
    public void SetUp()
    {
        var config = Options.Create(new McpConfiguration
        {
            Observability = new ObservabilityConfig { Enabled = true },
            RateLimit = new RateLimitConfig { RequestsPerMinute = 1000, WindowCount = 6 }
        });
        _rateLimiter = new RateLimiterService(config);
        _execution = new ExecutionTools(_rateLimiter, config);
    }

    [TearDown]
    public void TearDown() => _rateLimiter.Dispose();

    private ExecuteResult Execute(string doc, string script) =>
        (ExecuteResult)_execution.Execute(doc, "json", script);

    private static void Dump(ExecuteResult r, string label)
    {
        Console.WriteLine($"\n── {label} ────────────────��─────");
        Console.WriteLine($"  Output : {r.Output}");
        foreach (var t in r.Trace)
            Console.WriteLine($"  [{t.Outcome,-7}] {t.CommandName} @ {t.Path}: {t.Detail}");
    }

    // ── MATH: sum ────────────────────────────────────────────────────────────────

    [Test]
    public void Math_Sum_SumsArrayOfNumbers()
    {
        const string doc = """{"nums":[10,20,30],"total":0}""";
        const string script = """[{"command":"set","path":"$.total","value":"=sum($.nums)"}]""";

        var result = Execute(doc, script);

        Assert.That(result.Success, Is.True);
        Assert.That(result.Trace[0].Outcome, Is.EqualTo("success"));
        var output = JToken.Parse(result.Output);
        Assert.That(output["total"]!.Value<double>(), Is.EqualTo(60).Within(0.001));

        Dump(result, "sum");
    }

    [Test]
    public void Math_Sum_WrongPath_ReturnsFailure()
    {
        const string doc = """{"total":99}""";
        const string script = """[{"command":"set","path":"$.total","value":"=sum($.missing)"}]""";

        var result = Execute(doc, script);

        // sum with path-not-found logs an error and fails — agents must supply a valid path
        Assert.That(result.Success, Is.False,
            "sum with a missing path must fail so the agent knows to correct the path");
        Assert.That(result.Trace[0].Outcome, Is.EqualTo("failure"),
            "trace must report failure so the agent gets actionable feedback");

        Dump(result, "sum missing path → failure");
    }

    // ── MATH: avg ────────────────────────────────────────────────────────────────

    [Test]
    public void Math_Avg_ComputesAverageOfArray()
    {
        const string doc = """{"scores":[80,90,100],"avg":0}""";
        const string script = """[{"command":"set","path":"$.avg","value":"=avg($.scores)"}]""";

        var result = Execute(doc, script);

        Assert.That(result.Success, Is.True);
        var output = JToken.Parse(result.Output);
        Assert.That(output["avg"]!.Value<double>(), Is.EqualTo(90.0).Within(0.001));

        Dump(result, "avg");
    }

    // ── MATH: count ──────────────────────────────────────────────────────────────

    [Test]
    public void Math_Count_CountsArrayElements()
    {
        const string doc = """{"tags":["a","b","c","d"],"n":0}""";
        const string script = """[{"command":"set","path":"$.n","value":"=count($.tags)"}]""";

        var result = Execute(doc, script);

        Assert.That(result.Success, Is.True);
        var output = JToken.Parse(result.Output);
        Assert.That(output["n"]!.Value<int>(), Is.EqualTo(4));
    }

    // ── MATH: min / max ──────────────────────────────────────────────────────────

    [Test]
    public void Math_MinAndMax_ReturnBoundaryValues()
    {
        const string doc = """{"temps":[15,22,8,31,19],"lo":0,"hi":0}""";
        const string script = """
            [
              {"command":"set","path":"$.lo","value":"=min($.temps)"},
              {"command":"set","path":"$.hi","value":"=max($.temps)"}
            ]
            """;

        var result = Execute(doc, script);

        Assert.That(result.Success, Is.True);
        Assert.That(result.Trace.Select(t => t.Outcome), Has.All.EqualTo("success"));
        var output = JToken.Parse(result.Output);
        Assert.That(output["lo"]!.Value<int>(), Is.EqualTo(8));
        Assert.That(output["hi"]!.Value<int>(), Is.EqualTo(31));

        Dump(result, "min/max");
    }

    // ── MATH: round / ceiling / floor ────────────────────────────────────────────

    [Test]
    public void Math_RoundCeilingFloor_ProduceCorrectValues()
    {
        const string doc = """{"v":7.6,"r":0,"c":0,"f":0}""";
        const string script = """
            [
              {"command":"set","path":"$.r","value":"=round($.v)"},
              {"command":"set","path":"$.c","value":"=ceiling($.v)"},
              {"command":"set","path":"$.f","value":"=floor($.v)"}
            ]
            """;

        var result = Execute(doc, script);

        Assert.That(result.Success, Is.True);
        var output = JToken.Parse(result.Output);
        Assert.That(output["r"]!.Value<int>(), Is.EqualTo(8));
        Assert.That(output["c"]!.Value<int>(), Is.EqualTo(8));
        Assert.That(output["f"]!.Value<int>(), Is.EqualTo(7));

        Dump(result, "round/ceiling/floor");
    }

    // ── MATH: sumIf ──────────────────────────────────────────────────────────────

    [Test]
    public void Math_SumIf_SumsOnlyItemsMatchingCriteria()
    {
        // sumif(range, criteria, sum_range) — parallel arrays at root
        const string doc = """{"status":["paid","pending","paid"],"amounts":[100,200,150],"paid_total":0}""";
        const string script = """[{"command":"set","path":"$.paid_total","value":"=sumif($.status,'paid',$.amounts)"}]""";

        var result = Execute(doc, script);

        Assert.That(result.Success, Is.True);
        var output = JToken.Parse(result.Output);
        Assert.That(output["paid_total"]!.Value<double>(), Is.EqualTo(250).Within(0.001),
            "sumIf must sum only the two 'paid' amounts (100 + 150 = 250)");

        Dump(result, "sumIf");
    }

    // ── MATH: sqrt / pow / abs ────────────────────────────────────────────────────

    [Test]
    public void Math_SqrtPowAbs_ProduceExpectedValues()
    {
        const string doc = """{"n":16,"neg":-5,"base":2,"exp":8,"sq":0,"pw":0,"ab":0}""";
        const string script = """
            [
              {"command":"set","path":"$.sq","value":"=sqrt($.n)"},
              {"command":"set","path":"$.pw","value":"=pow($.base,$.exp)"},
              {"command":"set","path":"$.ab","value":"=abs($.neg)"}
            ]
            """;

        var result = Execute(doc, script);

        Assert.That(result.Success, Is.True);
        var output = JToken.Parse(result.Output);
        Assert.That(output["sq"]!.Value<double>(), Is.EqualTo(4).Within(0.001));
        Assert.That(output["pw"]!.Value<double>(), Is.EqualTo(256).Within(0.001));
        Assert.That(output["ab"]!.Value<double>(), Is.EqualTo(5).Within(0.001));
    }

    // ── TEXT: concat ─────────────────────────────────────────────────────────────

    [Test]
    public void Text_Concat_JoinsValuesWithLiteralSeparator()
    {
        const string doc = """{"first":"John","last":"Doe","sep":" ","full":""}""";
        const string script = """[{"command":"set","path":"$.full","value":"=concat($.first,$.sep,$.last)"}]""";

        var result = Execute(doc, script);

        Assert.That(result.Success, Is.True);
        var output = JToken.Parse(result.Output);
        Assert.That(output["full"]!.ToString(), Is.EqualTo("John Doe"));

        Dump(result, "concat");
    }

    // ── TEXT: replace ────────────────────────────────────────────────────────────

    [Test]
    public void Text_Replace_SubstitutesSubstring()
    {
        const string doc = """{"msg":"Hello World","old":"World","new":"TLio","result":""}""";
        const string script = """[{"command":"set","path":"$.result","value":"=replace($.msg,$.old,$.new)"}]""";

        var result = Execute(doc, script);

        Assert.That(result.Success, Is.True);
        var output = JToken.Parse(result.Output);
        Assert.That(output["result"]!.ToString(), Is.EqualTo("Hello TLio"));

        Dump(result, "replace");
    }

    // ── TEXT: toLower / toUpper ───────────────────────────────────────────────────

    [Test]
    public void Text_ToLowerAndToUpper_NormaliseCase()
    {
        const string doc = """{"code":"AbC123","lower":"","upper":""}""";
        const string script = """
            [
              {"command":"set","path":"$.lower","value":"=toLower($.code)"},
              {"command":"set","path":"$.upper","value":"=toUpper($.code)"}
            ]
            """;

        var result = Execute(doc, script);

        Assert.That(result.Success, Is.True);
        var output = JToken.Parse(result.Output);
        Assert.That(output["lower"]!.ToString(), Is.EqualTo("abc123"));
        Assert.That(output["upper"]!.ToString(), Is.EqualTo("ABC123"));

        Dump(result, "toLower/toUpper");
    }

    // ── TEXT: trim ───────────────────────────────────────────────────────────────

    [Test]
    public void Text_Trim_RemovesWhitespace()
    {
        const string doc = """{"raw":"  hello  ","clean":""}""";
        const string script = """[{"command":"set","path":"$.clean","value":"=trim($.raw)"}]""";

        var result = Execute(doc, script);

        Assert.That(result.Success, Is.True);
        var output = JToken.Parse(result.Output);
        Assert.That(output["clean"]!.ToString(), Is.EqualTo("hello"));
    }

    // ── TEXT: length ─────────────────────────────────────────────────────────────

    [Test]
    public void Text_Length_ReturnsStringLength()
    {
        const string doc = """{"pw":"secret123","len":0}""";
        const string script = """[{"command":"set","path":"$.len","value":"=length($.pw)"}]""";

        var result = Execute(doc, script);

        Assert.That(result.Success, Is.True);
        var output = JToken.Parse(result.Output);
        Assert.That(output["len"]!.Value<int>(), Is.EqualTo(9));
    }

    // ── TEXT: substring ──────────────────────────────────────────────────────────

    [Test]
    public void Text_Substring_ExtractsCharacterRange()
    {
        const string doc = """{"sku":"PRD-001","prefix":"","start":0,"end":3}""";
        const string script = """[{"command":"set","path":"$.prefix","value":"=substring($.sku,$.start,$.end)"}]""";

        var result = Execute(doc, script);

        Assert.That(result.Success, Is.True);
        var output = JToken.Parse(result.Output);
        Assert.That(output["prefix"]!.ToString(), Is.EqualTo("PRD"));

        Dump(result, "substring");
    }

    // ── TEXT: split ──────────────────────────────────────────────────────────────

    [Test]
    public void Text_Split_ProducesArrayFromDelimitedString()
    {
        const string doc = """{"csv":"java,python,csharp","delim":",","tags":null}""";
        const string script = """[{"command":"set","path":"$.tags","value":"=split($.csv,$.delim)"}]""";

        var result = Execute(doc, script);

        Assert.That(result.Success, Is.True);
        var output = JToken.Parse(result.Output);
        Assert.That(output["tags"]!.ToObject<string[]>()!,
            Is.EqualTo(new[] { "java", "python", "csharp" }));

        Dump(result, "split");
    }

    // ── TEXT: join ───────────────────────────────────────────────────────────────

    [Test]
    public void Text_Join_CombinesArrayToString()
    {
        const string doc = """{"parts":["2024","01","15"],"sep":"-","date":""}""";
        const string script = """[{"command":"set","path":"$.date","value":"=join($.parts,$.sep)"}]""";

        var result = Execute(doc, script);

        Assert.That(result.Success, Is.True);
        var output = JToken.Parse(result.Output);
        Assert.That(output["date"]!.ToString(), Is.EqualTo("2024-01-15"));

        Dump(result, "join");
    }

    // ── TEXT: startsWith / endsWith / contains ────────────────────────────────────

    [Test]
    public void Text_StartsWithEndsWithContains_ReturnBooleans()
    {
        const string doc = """{"url":"https://api.example.com/v2","prefix":"https","suffix":"v2","keyword":"example","sw":null,"ew":null,"has":null}""";
        const string script = """
            [
              {"command":"set","path":"$.sw","value":"=startsWith($.url,$.prefix)"},
              {"command":"set","path":"$.ew","value":"=endsWith($.url,$.suffix)"},
              {"command":"set","path":"$.has","value":"=contains($.url,$.keyword)"}
            ]
            """;

        var result = Execute(doc, script);

        Assert.That(result.Success, Is.True);
        var output = JToken.Parse(result.Output);
        Assert.That(output["sw"]!.Value<bool>(), Is.True);
        Assert.That(output["ew"]!.Value<bool>(), Is.True);
        Assert.That(output["has"]!.Value<bool>(), Is.True);

        Dump(result, "startsWith/endsWith/contains");
    }

    // ── TEXT: padLeft / padRight ─────────────────────────────────────────────────

    [Test]
    public void Text_PadLeftPadRight_ProduceFixedWidthStrings()
    {
        const string doc = """{"id":"42","width":6,"zeroPad":"0","starPad":"*","padl":"","padr":""}""";
        const string script = """
            [
              {"command":"set","path":"$.padl","value":"=padleft($.id,$.width,$.zeroPad)"},
              {"command":"set","path":"$.padr","value":"=padright($.id,$.width,$.starPad)"}
            ]
            """;

        var result = Execute(doc, script);

        Assert.That(result.Success, Is.True);
        var output = JToken.Parse(result.Output);
        Assert.That(output["padl"]!.ToString(), Is.EqualTo("000042"));
        Assert.That(output["padr"]!.ToString(), Is.EqualTo("42****"));

        Dump(result, "padLeft/padRight");
    }

    // ── TEXT: indexOf ────────────────────────────────────────────────────────────

    [Test]
    public void Text_IndexOf_ReturnsPositionOfSubstring()
    {
        const string doc = """{"str":"Hello World","sub":"World","pos":0}""";
        const string script = """[{"command":"set","path":"$.pos","value":"=indexOf($.str,$.sub)"}]""";

        var result = Execute(doc, script);

        Assert.That(result.Success, Is.True);
        var output = JToken.Parse(result.Output);
        Assert.That(output["pos"]!.Value<int>(), Is.EqualTo(6));

        Dump(result, "indexOf");
    }

    // ── TEXT: isEmpty ────────────────────────────────────────────────────────────

    [Test]
    public void Text_IsEmpty_ReturnsTrueForBlankFalseForNonBlank()
    {
        const string doc = """{"filled":"hello","blank":"","cf":null,"cb":null}""";
        const string script = """
            [
              {"command":"set","path":"$.cf","value":"=isEmpty($.filled)"},
              {"command":"set","path":"$.cb","value":"=isEmpty($.blank)"}
            ]
            """;

        var result = Execute(doc, script);

        Assert.That(result.Success, Is.True);
        var output = JToken.Parse(result.Output);
        Assert.That(output["cf"]!.Value<bool>(), Is.False);
        Assert.That(output["cb"]!.Value<bool>(), Is.True);
    }

    // ── TIMEDATE: isDateBetween ───────────────────────────────────────────────────

    [Test]
    public void TimeDate_IsDateBetween_ReturnsTrueWhenInRange()
    {
        const string doc = """{"event":"2024-06-15","from":"2024-01-01","to":"2024-12-31","result":null}""";
        const string script = """[{"command":"set","path":"$.result","value":"=isdatebetween($.event,$.from,$.to)"}]""";

        var result = Execute(doc, script);

        Assert.That(result.Success, Is.True);
        var output = JToken.Parse(result.Output);
        Assert.That(output["result"]!.Value<bool>(), Is.True,
            "2024-06-15 is within 2024-01-01 to 2024-12-31");

        Dump(result, "isDateBetween");
    }

    [Test]
    public void TimeDate_IsDateBetween_ReturnsFalseWhenOutOfRange()
    {
        const string doc = """{"event":"2023-12-31","from":"2024-01-01","to":"2024-12-31","result":null}""";
        const string script = """[{"command":"set","path":"$.result","value":"=isdatebetween($.event,$.from,$.to)"}]""";

        var result = Execute(doc, script);

        Assert.That(result.Success, Is.True);
        var output = JToken.Parse(result.Output);
        Assert.That(output["result"]!.Value<bool>(), Is.False,
            "2023-12-31 is not in 2024 range");
    }

    // ── TIMEDATE: dateCompare ────────────────────────────────────────────────────
    // dateCompare returns -1 (d1 < d2), 0 (equal), or 1 (d1 > d2)

    [Test]
    public void TimeDate_DateCompare_ReturnsMinusOneWhenFirstIsEarlier()
    {
        const string doc = """{"d1":"2024-03-01","d2":"2024-06-01","cmp":null}""";
        const string script = """[{"command":"set","path":"$.cmp","value":"=datecompare($.d1,$.d2)"}]""";

        var result = Execute(doc, script);

        Assert.That(result.Success, Is.True);
        var output = JToken.Parse(result.Output);
        Assert.That(output["cmp"]!.Value<long>(), Is.EqualTo(-1L),
            "dateCompare returns -1 when first date is earlier");

        Dump(result, "dateCompare");
    }

    [Test]
    public void TimeDate_DateCompare_ReturnsZeroForEqualDates()
    {
        const string doc = """{"d1":"2024-06-15","d2":"2024-06-15","cmp":null}""";
        const string script = """[{"command":"set","path":"$.cmp","value":"=datecompare($.d1,$.d2)"}]""";

        var result = Execute(doc, script);

        Assert.That(result.Success, Is.True);
        var output = JToken.Parse(result.Output);
        Assert.That(output["cmp"]!.Value<long>(), Is.EqualTo(0L));
    }

    // ── Combined scenario: chained math + text in single script ──────────────────

    [Test]
    public void Functions_CombinedPipeline_MathAndTextInOneScript()
    {
        const string doc = """{"name":"  Widget Pro  ","prices":[9.99,14.99,4.99],"total":0,"label":"","count":0}""";
        const string script = """
            [
              {"command":"set","path":"$.total","value":"=sum($.prices)"},
              {"command":"set","path":"$.count","value":"=count($.prices)"},
              {"command":"set","path":"$.label","value":"=trim($.name)"}
            ]
            """;

        var result = Execute(doc, script);

        Assert.That(result.Success, Is.True);
        Assert.That(result.Trace.Select(t => t.Outcome), Has.All.EqualTo("success"));

        var output = JToken.Parse(result.Output);
        Assert.That(output["total"]!.Value<double>(), Is.EqualTo(29.97).Within(0.01));
        Assert.That(output["count"]!.Value<int>(), Is.EqualTo(3));
        Assert.That(output["label"]!.ToString(), Is.EqualTo("Widget Pro"));

        Dump(result, "combined pipeline");
    }

    // ── Function on each array element via Property syntax ────────────────────────

    [Test]
    public void Functions_IndexedArrayPaths_ApplyFunctionToEachItem()
    {
        // Functions in TLio use dataContext (root) for path resolution.
        // Use absolute indexed paths to transform each array element's field.
        const string doc = """{"users":[{"id":1,"email":"Alice@Example.COM"},{"id":2,"email":"BOB@EXAMPLE.COM"}]}""";
        const string script = """
            [
              {"command":"set","path":"$.users[0].email","value":"=toLower($.users[0].email)"},
              {"command":"set","path":"$.users[1].email","value":"=toLower($.users[1].email)"}
            ]
            """;

        var result = Execute(doc, script);

        Assert.That(result.Success, Is.True);
        Assert.That(result.Trace.Select(t => t.Outcome), Has.All.EqualTo("success"));
        var output = JToken.Parse(result.Output);
        Assert.That(output["users"]![0]!["email"]!.ToString(), Is.EqualTo("alice@example.com"));
        Assert.That(output["users"]![1]!["email"]!.ToString(), Is.EqualTo("bob@example.com"));

        Dump(result, "toLower on indexed array elements");
    }

    // ── tlio_list_functions covers all registered functions ───────────────────────

    [Test]
    public void ListFunctions_ReturnsAllRegisteredFunctionNames()
    {
        var config = Options.Create(new McpConfiguration
        {
            Observability = new ObservabilityConfig { Enabled = true },
            RateLimit = new RateLimitConfig { RequestsPerMinute = 1000, WindowCount = 6 }
        });
        using var rateLimiter = new RateLimiterService(config);
        var reader = new TLio.Mcp.Services.AiRefReader(config);
        var discovery = new DiscoveryTools(reader, rateLimiter, config);

        var listResult = discovery.ListFunctions();
        var json = System.Text.Json.JsonSerializer.Serialize(listResult);
        var doc = System.Text.Json.JsonDocument.Parse(json);
        var functions = doc.RootElement.GetProperty("functions");

        Assert.That(functions.GetArrayLength(), Is.GreaterThanOrEqualTo(20),
            "At least 20 functions must be registered");

        var names = Enumerable.Range(0, functions.GetArrayLength())
            .Select(i => functions[i].GetProperty("name").GetString()!.ToLowerInvariant())
            .ToHashSet();

        Assert.That(names, Does.Contain("sum"),        "sum must be listed");
        Assert.That(names, Does.Contain("avg"),        "avg must be listed");
        Assert.That(names, Does.Contain("count"),      "count must be listed");
        Assert.That(names, Does.Contain("concat"),     "concat must be listed");
        Assert.That(names, Does.Contain("replace"),    "replace must be listed");
        Assert.That(names, Does.Contain("tolower"),    "toLower must be listed");
        Assert.That(names, Does.Contain("isdatebetween"), "isdatebetween must be listed");

        Console.WriteLine($"[listFunctions] {names.Count} functions (lowercase): {string.Join(", ", names.OrderBy(n => n))}");
    }
}
