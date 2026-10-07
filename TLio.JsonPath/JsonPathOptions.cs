namespace TLio.JsonPath;

/// <summary>
/// Configuration of a <see cref="JsonPathEngine"/>. Options are read once, when the engine is
/// created; changing the object afterwards has no effect on an existing engine.
/// </summary>
public sealed class JsonPathOptions
{
    /// <summary>The language queries are read in. Default <see cref="JsonPathDialect.Newtonsoft"/>.</summary>
    public JsonPathDialect Dialect { get; init; } = JsonPathDialect.Newtonsoft;

    /// <summary>
    /// Upper bound on the time any single regular-expression evaluation (<c>=~</c>, <c>match()</c>,
    /// <c>search()</c>) may take. Default two seconds; <see cref="Timeout.InfiniteTimeSpan"/> disables
    /// the guard. Exceeding it raises <see cref="JsonPathException"/> of kind <see cref="JsonPathErrorKind.Limit"/>.
    /// </summary>
    public TimeSpan RegexTimeout { get; init; } = TimeSpan.FromSeconds(2);

    /// <summary>Longest query text accepted, in characters. Default 4096.</summary>
    public int MaxQueryLength { get; init; } = 4096;

    /// <summary>
    /// Deepest nesting accepted, both of filter expressions in a query and of the document while a
    /// descendant segment walks it. Default 512 (<see cref="System.Text.Json.Nodes.JsonNode"/> parsing itself stops at 64).
    /// </summary>
    public int MaxDepth { get; init; } = 512;

    /// <summary>
    /// Newtonsoft's <c>JsonSelectSettings.ErrorWhenNoMatch</c>: throw instead of returning an empty
    /// result when a step of the query finds nothing. Only meaningful in the Newtonsoft dialect; RFC 9535
    /// defines an empty nodelist as a result, never an error.
    /// </summary>
    public bool ErrorWhenNoMatch { get; init; }

    /// <summary>
    /// Newtonsoft.Json reads a string that looks like an ISO 8601 date into a date value
    /// (<c>DateParseHandling.DateTime</c>, the default), which changes how it compares. Setting this
    /// reproduces that for documents that were parsed as System.Text.Json strings. Default true, which is
    /// what makes the Newtonsoft dialect match Newtonsoft. Ignored by the RFC 9535 dialect.
    /// </summary>
    public bool EmulateNewtonsoftDates { get; init; } = true;

    /// <summary>
    /// RFC 9485's grammar makes <c>^</c> and <c>$</c> ordinary characters, but the JSONPath Compliance Test Suite — and
    /// every engine that hands the pattern to a regex library — expects <c>match(@, '^ab.*')</c> to anchor. By default
    /// (<c>false</c>) a <c>^</c> that opens a branch and a <c>$</c> that closes one are anchors; anywhere else they are
    /// literal. Set <c>true</c> for the literal-only reading of the RFC grammar. See the README's divergence table.
    /// </summary>
    public bool StrictIRegexp { get; init; }

    /// <summary>Custom function extensions (RFC 9535 §2.4). May be null. Snapshotted when the engine is created.</summary>
    public JsonPathFunctionRegistry? Functions { get; init; }

    /// <summary>Maximum number of parsed queries kept by the engine's cache. Default 1024; 0 disables caching.</summary>
    public int QueryCacheSize { get; init; } = 1024;
}
