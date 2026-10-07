namespace TLio.JsonPath;

/// <summary>What went wrong, coarsely — the category is what differential tests compare.</summary>
public enum JsonPathErrorKind
{
    /// <summary>The query text is not valid in the selected dialect. Raised when the query is parsed.</summary>
    Syntax,

    /// <summary>The query is valid but could not be evaluated (for example a zero slice step, or a bad regex).</summary>
    Evaluation,

    /// <summary>A value could not be converted for a comparison (Newtonsoft dialect: <c>@.price &lt; 'abc'</c>).</summary>
    Conversion,

    /// <summary>A configured limit — query length, nesting depth, regex timeout — was exceeded.</summary>
    Limit,

    /// <summary>A single result was requested and the query matched more than one node (Newtonsoft's "Path returned multiple tokens").</summary>
    MultipleResults,

    /// <summary>The query matched nothing and the options say that is an error (<see cref="JsonPathOptions.ErrorWhenNoMatch"/>).</summary>
    NoMatch,
}

/// <summary>
/// Thrown for every failure of this library. An invalid query fails at parse time and carries the
/// zero-based <see cref="Position"/> of the offending character; an empty result is never an error
/// unless <see cref="JsonPathOptions.ErrorWhenNoMatch"/> says so.
/// </summary>
public class JsonPathException : Exception
{
    /// <summary>Creates the exception.</summary>
    public JsonPathException(string message, JsonPathErrorKind kind = JsonPathErrorKind.Syntax, int position = -1, Exception? inner = null)
        : base(position >= 0 && kind == JsonPathErrorKind.Syntax ? $"{message} (at position {position})" : message, inner)
    {
        Kind = kind;
        Position = position;
    }

    /// <summary>The category of failure.</summary>
    public JsonPathErrorKind Kind { get; }

    /// <summary>Zero-based index into the query text, or -1 when the failure is not tied to a position.</summary>
    public int Position { get; }
}
