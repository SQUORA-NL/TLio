namespace TLio.Extensions.TimeDate;

/// <summary>
/// The unit vocabulary shared by <c>=dateadd(...)</c> and <c>=datediff(...)</c>.
/// </summary>
internal enum DateUnit
{
    Years,
    Months,
    Weeks,
    Days,
    Hours,
    Minutes,
    Seconds
}

/// <summary>
/// Parses the optional <c>unit</c> argument of <c>=datediff(...)</c> and <c>=dateadd(...)</c>.
///
/// One parser, one vocabulary: a unit spelling accepted by either function is accepted by both,
/// case-insensitively and in singular or plural. An unrecognised unit fails the function rather
/// than falling back to days — a typo'd unit is the mistake a script author actually makes, so
/// <see cref="Accepted"/> is spelled out in the error message.
/// </summary>
internal static class DateUnits
{
    /// <summary>The accepted spellings, for error messages.</summary>
    public const string Accepted =
        "year(s), month(s), week(s), day(s), hour(s), minute(s), second(s)";

    public static bool TryParse(string? text, out DateUnit unit)
    {
        unit = DateUnit.Days;
        if (string.IsNullOrWhiteSpace(text)) return false;

        switch (text.Trim().ToLowerInvariant())
        {
            case "year":   case "years":   unit = DateUnit.Years;   return true;
            case "month":  case "months":  unit = DateUnit.Months;  return true;
            case "week":   case "weeks":   unit = DateUnit.Weeks;   return true;
            case "day":    case "days":    unit = DateUnit.Days;    return true;
            case "hour":   case "hours":   unit = DateUnit.Hours;   return true;
            case "minute": case "minutes": unit = DateUnit.Minutes; return true;
            case "second": case "seconds": unit = DateUnit.Seconds; return true;
            default: return false;
        }
    }
}
