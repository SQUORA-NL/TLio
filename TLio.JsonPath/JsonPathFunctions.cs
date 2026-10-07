using System.Text.Json.Nodes;

namespace TLio.JsonPath;

/// <summary>
/// The three types of RFC 9535 §2.4.1 that function parameters and results are declared in.
/// </summary>
public enum JsonPathFunctionType
{
    /// <summary>A JSON value, or "Nothing" (no value).</summary>
    Value,

    /// <summary>True or false. A query given where a logical is expected is converted by testing that it is non-empty.</summary>
    Logical,

    /// <summary>A list of nodes — the result of a query.</summary>
    Nodes,
}

/// <summary>A JSON value or "Nothing" — RFC 9535's <c>ValueType</c>. A JSON null is a value; it is not Nothing.</summary>
public readonly struct JsonPathValue
{
    private JsonPathValue(JsonNode? node, bool isNothing)
    {
        Node = node;
        IsNothing = isNothing;
    }

    /// <summary>The absence of a value.</summary>
    public static JsonPathValue Nothing => new(null, true);

    /// <summary>Wraps a JSON value. <c>null</c> is the JSON null value, not Nothing.</summary>
    public static JsonPathValue Of(JsonNode? node) => new(node, false);

    /// <summary>True when there is no value at all.</summary>
    public bool IsNothing { get; }

    /// <summary>The value. A <c>null</c> here, with <see cref="IsNothing"/> false, is JSON null.</summary>
    public JsonNode? Node { get; }
}

/// <summary>The arguments a custom function is called with, already converted to its declared parameter types.</summary>
public sealed class JsonPathFunctionArguments
{
    private readonly object?[] _values;

    internal JsonPathFunctionArguments(object?[] values) => _values = values;

    /// <summary>Number of arguments (equals the declared parameter count).</summary>
    public int Count => _values.Length;

    /// <summary>Argument <paramref name="index"/> of a <see cref="JsonPathFunctionType.Value"/> parameter.</summary>
    public JsonPathValue Value(int index) => (JsonPathValue)_values[index]!;

    /// <summary>Argument <paramref name="index"/> of a <see cref="JsonPathFunctionType.Logical"/> parameter.</summary>
    public bool Logical(int index) => (bool)_values[index]!;

    /// <summary>Argument <paramref name="index"/> of a <see cref="JsonPathFunctionType.Nodes"/> parameter.</summary>
    public IReadOnlyList<JsonNode?> Nodes(int index) => (IReadOnlyList<JsonNode?>)_values[index]!;
}

/// <summary>What a custom function returns; build it with the factory that matches the declared return type.</summary>
public readonly struct JsonPathFunctionResult
{
    internal JsonPathFunctionResult(object? payload) => Payload = payload;

    internal object? Payload { get; }

    /// <summary>For a function declared to return <see cref="JsonPathFunctionType.Value"/>.</summary>
    public static JsonPathFunctionResult FromValue(JsonPathValue value) => new(value);

    /// <summary>For a function declared to return <see cref="JsonPathFunctionType.Value"/>: a JSON value (null is JSON null).</summary>
    public static JsonPathFunctionResult FromNode(JsonNode? node) => new(JsonPathValue.Of(node));

    /// <summary>For a function declared to return <see cref="JsonPathFunctionType.Logical"/>.</summary>
    public static JsonPathFunctionResult FromLogical(bool value) => new(value);

    /// <summary>For a function declared to return <see cref="JsonPathFunctionType.Nodes"/>.</summary>
    public static JsonPathFunctionResult FromNodes(IReadOnlyList<JsonNode?> nodes) => new(nodes);
}

/// <summary>
/// A function extension (RFC 9535 §2.4) with declared parameter and result types. The types are
/// checked when a query is parsed, so a query that passes a node list where a value is expected is
/// rejected up front rather than misbehaving at evaluation.
/// </summary>
public sealed class JsonPathFunction
{
    /// <summary>Declares a function.</summary>
    /// <param name="name">Lower-case letters, digits and underscore, starting with a letter (<c>function-name</c> in the RFC grammar).</param>
    /// <param name="parameterTypes">One entry per parameter.</param>
    /// <param name="returnType">The declared result type.</param>
    /// <param name="implementation">Called with arguments converted to <paramref name="parameterTypes"/>; must return the factory matching <paramref name="returnType"/>.</param>
    public JsonPathFunction(
        string name,
        IReadOnlyList<JsonPathFunctionType> parameterTypes,
        JsonPathFunctionType returnType,
        Func<JsonPathFunctionArguments, JsonPathFunctionResult> implementation)
    {
        ArgumentNullException.ThrowIfNull(name);
        ArgumentNullException.ThrowIfNull(parameterTypes);
        ArgumentNullException.ThrowIfNull(implementation);
        if (!IsValidName(name))
            throw new ArgumentException($"'{name}' is not a valid function name: expected [a-z][a-z0-9_]*.", nameof(name));

        Name = name;
        ParameterTypes = parameterTypes.ToArray();
        ReturnType = returnType;
        Implementation = implementation;
    }

    /// <summary>The name used in queries.</summary>
    public string Name { get; }

    /// <summary>Declared parameter types.</summary>
    public IReadOnlyList<JsonPathFunctionType> ParameterTypes { get; }

    /// <summary>Declared result type.</summary>
    public JsonPathFunctionType ReturnType { get; }

    internal Func<JsonPathFunctionArguments, JsonPathFunctionResult> Implementation { get; }

    internal static bool IsValidName(string name)
    {
        if (name.Length == 0 || name[0] is < 'a' or > 'z') return false;
        foreach (var c in name)
            if (!(c is >= 'a' and <= 'z' || c is >= '0' and <= '9' || c == '_')) return false;
        return true;
    }
}

/// <summary>The set of custom functions a <see cref="JsonPathEngine"/> knows, in addition to the built-ins <c>length</c>, <c>count</c>, <c>match</c>, <c>search</c> and <c>value</c>.</summary>
public sealed class JsonPathFunctionRegistry
{
    private readonly Dictionary<string, JsonPathFunction> _functions = new(StringComparer.Ordinal);

    internal static readonly string[] BuiltInNames = ["length", "count", "match", "search", "value"];

    /// <summary>Adds a function. Built-in names cannot be replaced, and a name can be registered once.</summary>
    public JsonPathFunctionRegistry Register(JsonPathFunction function)
    {
        ArgumentNullException.ThrowIfNull(function);
        if (Array.IndexOf(BuiltInNames, function.Name) >= 0)
            throw new ArgumentException($"'{function.Name}' is a built-in RFC 9535 function and cannot be replaced.", nameof(function));
        if (!_functions.TryAdd(function.Name, function))
            throw new ArgumentException($"A function named '{function.Name}' is already registered.", nameof(function));
        return this;
    }

    internal IReadOnlyDictionary<string, JsonPathFunction> Snapshot() => new Dictionary<string, JsonPathFunction>(_functions, StringComparer.Ordinal);
}
