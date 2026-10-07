namespace TLio.JsonPath;

/// <summary>
/// The JSONPath language a query is read in. Newtonsoft.Json's JSONPath predates RFC 9535 and
/// disagrees with it in a number of places, so one rule set cannot serve both; the dialect says
/// which one applies. It is configured once, on <see cref="JsonPathOptions"/>, never per call.
/// </summary>
public enum JsonPathDialect
{
    /// <summary>
    /// Bit-for-bit the behaviour of Newtonsoft.Json 13.0.x <c>SelectToken</c> / <c>SelectTokens</c>:
    /// the same syntax, the same nodes in the same order, the same errors. This is the default so
    /// that scripts written against TLio's Newtonsoft adapter keep working unchanged.
    /// </summary>
    Newtonsoft = 0,

    /// <summary>Strict RFC 9535 (with function extensions and RFC 9485 I-Regexp). Nothing else is accepted.</summary>
    Rfc9535 = 1,

    /// <summary>
    /// A strict superset of <see cref="Newtonsoft"/>: a query that Newtonsoft.Json accepts gives
    /// the Newtonsoft result; a query it does not accept is read as RFC 9535. See the README for
    /// the precise rule.
    /// </summary>
    Extended = 2,
}
