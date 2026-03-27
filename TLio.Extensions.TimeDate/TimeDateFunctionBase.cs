using System.Globalization;

namespace TLio.Extensions.TimeDate;

/// <summary>
/// Base class for all TimeDate extension functions.
///
/// Dates in JSON are represented as strings. This base class provides helpers to
/// resolve string arguments and parse them as DateTimeOffset values, mirroring
/// JLio's TimeDateHelper approach.
///
/// Supported formats (tried in order):
///   1. ISO 8601 / RFC 3339 (e.g. "2024-03-15T10:30:00Z", "2024-03-15")
///   2. Common date/time formats via DateTimeOffset.TryParse with InvariantCulture
///
/// Null-handling contract:
///   - Path not found → failure
///   - Found-but-null → failure (null is not a valid date)
/// </summary>
public abstract class TimeDateFunctionBase<TNode> : FunctionBase<TNode>
{
    public override string FunctionName => GetType().Name.ToLowerInvariant();

    private static readonly string[] DateFormats =
    {
        "yyyy-MM-ddTHH:mm:sszzz",
        "yyyy-MM-ddTHH:mm:ss",
        "yyyy-MM-ddTHH:mm:ssZ",
        "yyyy-MM-dd",
        "MM/dd/yyyy",
        "dd-MM-yyyy",
    };

    // ── Argument resolution ───────────────────────────────────────────────────

    /// <summary>
    /// Resolve one argument to a DateTimeOffset. Path-not-found or unparseable → false + log error.
    /// </summary>
    protected static bool TryGetDateArg(
        IFunctionSupportedValue<TNode> arg,
        out DateTimeOffset value,
        TNode currentNode, TNode dataContext,
        IExecutionContext<TNode> context, string funcName)
    {
        value = default;
        var result = arg.GetValue(currentNode, dataContext, context);
        if (!result.Success || result.Data.Count == 0)
        {
            context.LogError(funcName, $"{funcName}: argument path not found.");
            return false;
        }
        var node = result.Data.First!;
        if (context.NodeAdapter.IsNull(node))
        {
            context.LogError(funcName, $"{funcName}: date argument is null.");
            return false;
        }
        var str = context.NodeAdapter.TryGetString(node) ?? string.Empty;
        return TryParseDate(str, out value, funcName, context);
    }

    /// <summary>
    /// Resolve one argument and collect ALL its date values (handles arrays).
    /// Returns false on any path-not-found or parse error.
    /// </summary>
    protected static bool TryCollectDates(
        IFunctionSupportedValue<TNode> arg,
        List<DateTimeOffset> values,
        TNode currentNode, TNode dataContext,
        IExecutionContext<TNode> context, string funcName)
    {
        var result = arg.GetValue(currentNode, dataContext, context);
        if (!result.Success || result.Data.Count == 0)
        {
            context.LogError(funcName, $"{funcName}: argument path not found.");
            return false;
        }
        foreach (var node in result.Data)
            if (!CollectDatesFromNode(node, values, context, funcName))
                return false;
        return true;
    }

    private static bool CollectDatesFromNode(
        TNode node, List<DateTimeOffset> values,
        IExecutionContext<TNode> context, string funcName)
    {
        if (context.NodeAdapter.IsArray(node))
        {
            foreach (var el in context.NodeAdapter.GetArrayElements(node))
                if (!CollectDatesFromNode(el, values, context, funcName))
                    return false;
            return true;
        }
        if (context.NodeAdapter.IsNull(node))
        {
            context.LogError(funcName, $"{funcName}: null value in date collection.");
            return false;
        }
        var str = context.NodeAdapter.TryGetString(node) ?? string.Empty;
        if (!TryParseDate(str, out var dto, funcName, context)) return false;
        values.Add(dto);
        return true;
    }

    private static bool TryParseDate(
        string str, out DateTimeOffset value,
        string funcName, IExecutionContext<TNode> context)
    {
        value = default;
        // Try exact formats first so date-only strings like "2024-01-01" are treated as UTC
        // (AssumeUniversal), not local time. This also correctly handles "Z"-suffixed timestamps.
        foreach (var fmt in DateFormats)
            if (DateTimeOffset.TryParseExact(str, fmt, CultureInfo.InvariantCulture,
                DateTimeStyles.AssumeUniversal, out value))
                return true;

        // Fallback: roundtrip-aware parse for explicit-offset strings like "2024-01-01T00:00:00+05:00"
        if (DateTimeOffset.TryParse(str, CultureInfo.InvariantCulture,
            DateTimeStyles.RoundtripKind, out value))
            return true;

        context.LogError(funcName, $"{funcName}: cannot parse '{str}' as a date.");
        return false;
    }

    /// <summary>
    /// Format a DateTimeOffset back to the canonical ISO 8601 string used by TLio.
    /// If the value has no time component, returns date-only "yyyy-MM-dd".
    /// Otherwise returns full "yyyy-MM-ddTHH:mm:ssZ" (UTC).
    /// </summary>
    protected static string FormatDate(DateTimeOffset value)
    {
        var utc = value.UtcDateTime;
        return utc.TimeOfDay == TimeSpan.Zero
            ? utc.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture)
            : utc.ToString("yyyy-MM-ddTHH:mm:ssZ", CultureInfo.InvariantCulture);
    }
}
