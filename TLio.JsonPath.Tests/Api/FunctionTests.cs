using System.Text.Json.Nodes;
using NUnit.Framework;

namespace TLio.JsonPath.Tests.Api;

/// <summary>Function extensions: the built-ins' typing rules and custom registration.</summary>
[TestFixture]
public class FunctionTests
{
    private static JsonPathEngine With(params JsonPathFunction[] functions)
    {
        var registry = new JsonPathFunctionRegistry();
        foreach (var f in functions) registry.Register(f);
        return new JsonPathEngine(new JsonPathOptions { Dialect = JsonPathDialect.Rfc9535, Functions = registry });
    }

    private static readonly JsonPathFunction EndsWith = new(
        "ends_with",
        [JsonPathFunctionType.Value, JsonPathFunctionType.Value],
        JsonPathFunctionType.Logical,
        a => JsonPathFunctionResult.FromLogical(
            !a.Value(0).IsNothing && !a.Value(1).IsNothing
            && a.Value(0).Node is JsonValue v0 && v0.TryGetValue<string>(out var s)
            && a.Value(1).Node is JsonValue v1 && v1.TryGetValue<string>(out var suffix)
            && s.EndsWith(suffix, StringComparison.Ordinal)));

    private static readonly JsonPathFunction Double = new(
        "double",
        [JsonPathFunctionType.Value],
        JsonPathFunctionType.Value,
        a => a.Value(0).Node is JsonValue v && v.TryGetValue<long>(out var n)
            ? JsonPathFunctionResult.FromNode(JsonValue.Create(n * 2))
            : JsonPathFunctionResult.FromValue(JsonPathValue.Nothing));

    private static readonly JsonPathFunction FirstChild = new(
        "first_child",
        [JsonPathFunctionType.Nodes],
        JsonPathFunctionType.Nodes,
        a => JsonPathFunctionResult.FromNodes(a.Nodes(0).Select(n => n is JsonArray arr && arr.Count > 0 ? arr[0] : null).Where(n => n != null).ToList()));

    private static readonly JsonPathFunction AnyTrue = new(
        "any_true",
        [JsonPathFunctionType.Logical, JsonPathFunctionType.Logical],
        JsonPathFunctionType.Logical,
        a => JsonPathFunctionResult.FromLogical(a.Logical(0) || a.Logical(1)));

    private static readonly JsonNode Doc = JsonNode.Parse("""
        [ {"name": "report.pdf", "n": 4, "tags": ["x", "y"]}, {"name": "photo.png", "n": 2, "tags": []}, {"name": 7, "n": "x"} ]
        """)!;

    [Test]
    public void A_custom_LogicalType_function_is_usable_as_a_test()
    {
        var engine = With(EndsWith);
        Assert.That(engine.Select("$[?ends_with(@.name, '.pdf')]", Doc).Select(m => m.Index), Is.EqualTo(new[] { 0 }));
        Assert.That(engine.Select("$[?!ends_with(@.name, '.pdf')]", Doc).Select(m => m.Index), Is.EqualTo(new[] { 1, 2 }));
        Assert.That(engine.Select("$[?ends_with(@.name, '.png') || ends_with(@.name, '.pdf')]", Doc).Select(m => m.Index), Is.EqualTo(new[] { 0, 1 }));
    }

    [Test]
    public void A_custom_ValueType_function_is_usable_in_a_comparison_and_as_an_argument()
    {
        var engine = With(Double, EndsWith);
        Assert.That(engine.Select("$[?double(@.n) == 8]", Doc).Select(m => m.Index), Is.EqualTo(new[] { 0 }));
        Assert.That(engine.Select("$[?double(@.n) > 3]", Doc).Select(m => m.Index), Is.EqualTo(new[] { 0, 1 }));
        // Nothing (a non-integer input) equals Nothing, and nothing else.
        Assert.That(engine.Select("$[?double(@.n) == double(@.nope)]", Doc).Select(m => m.Index), Is.EqualTo(new[] { 2 }));
        Assert.That(engine.Select("$[?length(@.name) == 10]", Doc).Select(m => m.Index), Is.EqualTo(new[] { 0 }));
    }

    [Test]
    public void A_custom_NodesType_function_composes_with_count_and_existence_tests()
    {
        var engine = With(FirstChild);
        Assert.That(engine.Select("$[?first_child(@.tags)]", Doc).Select(m => m.Index), Is.EqualTo(new[] { 0 }));
        Assert.That(engine.Select("$[?count(first_child(@.tags)) == 1]", Doc).Select(m => m.Index), Is.EqualTo(new[] { 0 }));
        Assert.That(engine.Select("$[?count(first_child(@.tags)) == 0]", Doc).Select(m => m.Index), Is.EqualTo(new[] { 1, 2 }));
    }

    [Test]
    public void A_LogicalType_parameter_accepts_a_comparison_or_a_query_converted_by_existence()
    {
        var engine = With(AnyTrue);
        Assert.That(engine.Select("$[?any_true(@.n == 2, @.tags[0] == 'x')]", Doc).Select(m => m.Index), Is.EqualTo(new[] { 0, 1 }));
        Assert.That(engine.Select("$[?any_true(@.tags[0], @.zzz)]", Doc).Select(m => m.Index), Is.EqualTo(new[] { 0 }));
    }

    [TestCase("$[?ends_with(@.name)]", "wrong number of arguments")]
    [TestCase("$[?ends_with(@.name, @.tags[*])]", "a non-singular query where a value is expected")]
    [TestCase("$[?ends_with(@.name, 1 == 1)]", "a logical expression where a value is expected")]
    [TestCase("$[?double(@.n)]", "a ValueType function used as a test")]
    [TestCase("$[?double(@.n) == @.tags[*]]", "a non-singular query in a comparison")]
    [TestCase("$[?count(1)]", "a literal where nodes are expected")]
    [TestCase("$[?count(@.n == 1)]", "a logical where nodes are expected")]
    [TestCase("$[?length(@.*)]", "a non-singular query where a value is expected")]
    [TestCase("$[?value(@.n) == 1 == 2]", "chained comparison")]
    [TestCase("$[?match(@.name, 'x') == true]", "a LogicalType function compared")]
    public void Ill_typed_function_use_is_rejected_when_the_query_is_parsed(string query, string why)
    {
        var engine = With(EndsWith, Double);
        var ex = Assert.Throws<JsonPathException>(() => engine.Parse(query), why)!;
        Assert.That(ex.Kind, Is.EqualTo(JsonPathErrorKind.Syntax));
        Assert.That(ex.Position, Is.GreaterThanOrEqualTo(0));
    }

    [Test]
    public void An_unregistered_function_is_a_parse_error_naming_it()
    {
        var ex = Assert.Throws<JsonPathException>(() => With().Parse("$[?ends_with(@.name, 'x')]"))!;
        Assert.That(ex.Message, Does.Contain("ends_with"));
    }

    [Test]
    public void Built_in_function_names_cannot_be_replaced_and_names_are_unique()
    {
        var shadow = new JsonPathFunction("length", [JsonPathFunctionType.Value], JsonPathFunctionType.Value, _ => JsonPathFunctionResult.FromValue(JsonPathValue.Nothing));
        Assert.Throws<ArgumentException>(() => new JsonPathFunctionRegistry().Register(shadow));
        Assert.Throws<ArgumentException>(() => new JsonPathFunctionRegistry().Register(EndsWith).Register(EndsWith));
    }

    [TestCase("")]
    [TestCase("Upper")]
    [TestCase("1abc")]
    [TestCase("a-b")]
    [TestCase("a b")]
    public void Function_names_follow_the_RFC_grammar(string name)
    {
        Assert.Throws<ArgumentException>(() => new JsonPathFunction(name, [], JsonPathFunctionType.Logical, _ => JsonPathFunctionResult.FromLogical(true)));
    }

    [Test]
    public void A_function_that_returns_the_wrong_kind_is_reported()
    {
        var bad = new JsonPathFunction("bad", [JsonPathFunctionType.Value], JsonPathFunctionType.Logical, _ => JsonPathFunctionResult.FromNode(null));
        var ex = Assert.Throws<JsonPathException>(() => With(bad).Select("$[?bad(@.n)]", Doc))!;
        Assert.That(ex.Kind, Is.EqualTo(JsonPathErrorKind.Evaluation));
    }

    [Test]
    public void Registrations_are_snapshotted_when_the_engine_is_created()
    {
        var registry = new JsonPathFunctionRegistry().Register(EndsWith);
        var engine = new JsonPathEngine(new JsonPathOptions { Dialect = JsonPathDialect.Rfc9535, Functions = registry });
        registry.Register(Double);
        Assert.Throws<JsonPathException>(() => engine.Parse("$[?double(@.n) == 8]"));
    }

    [Test]
    public void Built_ins_follow_the_RFC_result_types()
    {
        var engine = new JsonPathEngine(new JsonPathOptions { Dialect = JsonPathDialect.Rfc9535 });
        var doc = JsonNode.Parse("""[ {"s": "héllo😀", "a": [1,2,3], "o": {"x":1,"y":2}, "n": 5}, {"s": "", "a": [], "o": {}} ]""");
        // length: string -> code points, array/object -> size, other -> Nothing
        Assert.That(engine.Select("$[?length(@.s) == 6]", doc).Select(m => m.Index), Is.EqualTo(new[] { 0 }));
        Assert.That(engine.Select("$[?length(@.a) == 3]", doc).Select(m => m.Index), Is.EqualTo(new[] { 0 }));
        Assert.That(engine.Select("$[?length(@.o) == 2]", doc).Select(m => m.Index), Is.EqualTo(new[] { 0 }));
        Assert.That(engine.Select("$[?length(@.n) == length(@.n)]", doc).Select(m => m.Index), Is.EqualTo(new[] { 0, 1 }), "Nothing == Nothing");
        // count and value
        Assert.That(engine.Select("$[?count(@.a[*]) == 3]", doc).Select(m => m.Index), Is.EqualTo(new[] { 0 }));
        Assert.That(engine.Select("$[?value(@.o.x) == 1]", doc).Select(m => m.Index), Is.EqualTo(new[] { 0 }));
        Assert.That(engine.Select("$[?value(@.a[*]) == 1]", doc), Is.Empty, "value() of several nodes is Nothing");
        // search vs match
        Assert.That(engine.Select("$[?search(@.s, 'l+')]", doc).Select(m => m.Index), Is.EqualTo(new[] { 0 }));
        Assert.That(engine.Select("$[?match(@.s, 'l+')]", doc), Is.Empty);
        Assert.That(engine.Select("$[?match(@.s, 'h.llo.')]", doc).Select(m => m.Index), Is.EqualTo(new[] { 0 }), "'.' consumes a whole surrogate pair");
    }
}
