// The value semantics of Newtonsoft.Json 13.0.4's query comparisons — JValue.Compare, JValue.Equals,
// JValue.CompareTo, MathUtils.ApproxEquals and the BooleanQueryExpression equality helpers — over
// Prim instead of JValue (MIT licensed, Copyright (c) 2007 James Newton-King). Note what is *not* here
// by design: any notion of "sensible" comparison. `@.price < 'abc'` throws, `1 == 1.0` holds, a string
// that looks like an ISO date compares as a date, and `'5' > 4` converts the string. Those are the
// behaviour being preserved.
#nullable disable
using System.Globalization;
using System.Numerics;

namespace TLio.JsonPath.Internal.Newtonsoft;

/// <summary>JTokenType, restricted to what a JSON document can contain.</summary>
internal enum NType : byte
{
    Integer,
    Float,
    String,
    Boolean,
    Null,
    Date,
}

internal static class NewtonsoftValues
{
    public static NType TypeOf(in Prim p) => p.Kind switch
    {
        PrimKind.Long or PrimKind.BigInt => NType.Integer,
        PrimKind.Double => NType.Float,
        PrimKind.String => NType.String,
        PrimKind.Bool => NType.Boolean,
        PrimKind.Date => NType.Date,
        _ => NType.Null,
    };

    private static JsonPathException Conversion(string message) => new(message, JsonPathErrorKind.Conversion);

    // ── Convert.ToXxx, as Newtonsoft's comparison code applies it to the boxed value ─────────

    private static long ToInt64(in Prim p)
    {
        switch (p.Kind)
        {
            case PrimKind.Long: return p.L;
            case PrimKind.Bool: return p.L;
            case PrimKind.Double:
            {
                // Convert.ToInt64(double): round half to even, OverflowException when out of range.
                var r = Math.Round(p.D, MidpointRounding.ToEven);
                if (double.IsNaN(r) || r >= 9.2233720368547758E+18 || r < -9.2233720368547758E+18)
                    throw Conversion("Value was either too large or too small for an Int64.");
                return (long)r;
            }
            case PrimKind.String:
                if (!long.TryParse(p.Str, NumberStyles.Integer, CultureInfo.InvariantCulture, out var l))
                    throw Conversion($"The input string '{p.Str}' was not in a correct format.");
                return l;
            default:
                throw Conversion("Unable to cast object to Int64.");
        }
    }

    private static double ToDouble(in Prim p)
    {
        switch (p.Kind)
        {
            case PrimKind.Long: return p.L;
            case PrimKind.Double: return p.D;
            case PrimKind.Bool: return p.L;
            case PrimKind.String:
                if (!double.TryParse(p.Str, NumberStyles.Float | NumberStyles.AllowThousands, CultureInfo.InvariantCulture, out var d))
                    throw Conversion($"The input string '{p.Str}' was not in a correct format.");
                return d;
            default:
                throw Conversion("Unable to cast object to Double.");
        }
    }

    private static bool ToBoolean(in Prim p)
    {
        switch (p.Kind)
        {
            case PrimKind.Bool: return p.L != 0;
            case PrimKind.Long: return p.L != 0;
            case PrimKind.Double: return p.D != 0;
            case PrimKind.String:
                if (!bool.TryParse(p.Str, out var b))
                    throw Conversion($"String '{p.Str}' was not recognized as a valid Boolean.");
                return b;
            default:
                throw Conversion("Unable to cast object to Boolean.");
        }
    }

    private static DateTime ToDateTime(in Prim p)
    {
        switch (p.Kind)
        {
            case PrimKind.Date: return (DateTime)p.O;
            case PrimKind.String:
                if (!DateTime.TryParse(p.Str, CultureInfo.InvariantCulture, out var dt))
                    throw Conversion($"String '{p.Str}' was not recognized as a valid DateTime.");
                return dt;
            default:
                throw Conversion("Unable to cast object to DateTime.");
        }
    }

    /// <summary><c>ConvertUtils.ToBigInteger</c>.</summary>
    private static BigInteger ToBigInteger(in Prim p)
    {
        switch (p.Kind)
        {
            case PrimKind.BigInt: return p.Big;
            case PrimKind.Long: return new BigInteger(p.L);
            case PrimKind.Double:
                if (double.IsNaN(p.D) || double.IsInfinity(p.D)) throw Conversion("Value was either too large or too small for a BigInteger.");
                return new BigInteger(p.D);
            case PrimKind.String:
                if (!BigInteger.TryParse(p.Str, NumberStyles.Integer, CultureInfo.InvariantCulture, out var b))
                    throw Conversion($"The value '{p.Str}' could not be parsed.");
                return b;
            default:
                throw Conversion("Cannot convert value to BigInteger.");
        }
    }

    // ── JValue.Compare / CompareTo / Equals ──────────────────────────────────────────────────

    private static int CompareBigInteger(BigInteger i1, in Prim i2)
    {
        var result = i1.CompareTo(ToBigInteger(i2));
        if (result != 0) return result;

        // converting a fractional number to a BigInteger loses the fraction: check it when the integers are equal
        if (i2.Kind == PrimKind.Double)
        {
            var d = i2.D;
            return 0d.CompareTo(Math.Abs(d - Math.Truncate(d)));
        }

        return result;
    }

    /// <summary><c>MathUtils.ApproxEquals</c>: doubles that differ by less than a relative epsilon are equal.</summary>
    private static bool ApproxEquals(double d1, double d2)
    {
        const double epsilon = 2.2204460492503131E-16;
        if (d1 == d2) return true;
        var tolerance = ((Math.Abs(d1) + Math.Abs(d2)) + 10.0) * epsilon;
        var difference = d1 - d2;
        return -tolerance < difference && tolerance > difference;
    }

    private static int CompareFloat(in Prim a, in Prim b)
    {
        var d1 = ToDouble(a);
        var d2 = ToDouble(b);
        if (ApproxEquals(d1, d2)) return 0;
        return d1.CompareTo(d2);
    }

    /// <summary><c>JValue.Compare(JTokenType, object, object)</c>.</summary>
    public static int Compare(NType type, in Prim a, in Prim b)
    {
        var aNull = a.Kind == PrimKind.Null;
        var bNull = b.Kind == PrimKind.Null;
        if (aNull && bNull) return 0;
        if (bNull) return 1;
        if (aNull) return -1;

        switch (type)
        {
            case NType.Integer:
                if (a.Kind == PrimKind.BigInt) return CompareBigInteger(a.Big, b);
                if (b.Kind == PrimKind.BigInt) return -CompareBigInteger(b.Big, a);
                if (a.Kind == PrimKind.Double || b.Kind == PrimKind.Double) return CompareFloat(a, b);
                return ToInt64(a).CompareTo(ToInt64(b));

            case NType.Float:
                if (a.Kind == PrimKind.BigInt) return CompareBigInteger(a.Big, b);
                if (b.Kind == PrimKind.BigInt) return -CompareBigInteger(b.Big, a);
                return CompareFloat(a, b);

            case NType.String:
                return string.CompareOrdinal(ConvertToString(a), ConvertToString(b));

            case NType.Boolean:
                return ToBoolean(a).CompareTo(ToBoolean(b));

            case NType.Date:
                // A string compared with a date takes the date's type (CompareTo), so `a` can be the string: Newtonsoft
                // then unboxes it as a DateTimeOffset and fails with an InvalidCastException.
                if (a.Kind != PrimKind.Date) throw Conversion("Unable to cast object of type 'System.String' to type 'System.DateTimeOffset'.");
                return ((DateTime)a.O).CompareTo(ToDateTime(b));

            default:
                throw Conversion("Unexpected value type: " + type);
        }
    }

    private static string ConvertToString(in Prim p) => p.Kind switch
    {
        PrimKind.String => p.Str,
        PrimKind.Long => p.L.ToString(CultureInfo.InvariantCulture),
        PrimKind.Double => p.D.ToString(CultureInfo.InvariantCulture),
        PrimKind.Bool => p.L != 0 ? "True" : "False",
        PrimKind.BigInt => p.Big.ToString(CultureInfo.InvariantCulture),
        PrimKind.Date => ((DateTime)p.O).ToString(CultureInfo.InvariantCulture),
        _ => null,
    };

    /// <summary><c>JValue.CompareTo(JValue)</c>: a string compared with a non-string takes the other side's type, so it is converted.</summary>
    public static int CompareTo(in Prim a, in Prim b)
    {
        var at = TypeOf(a);
        var bt = TypeOf(b);
        var comparisonType = at == NType.String && at != bt ? bt : at;
        return Compare(comparisonType, a, b);
    }

    /// <summary><c>JValue.Equals(JValue)</c>: same JTokenType and Compare == 0.</summary>
    public static bool ValuesEquals(in Prim a, in Prim b)
    {
        var at = TypeOf(a);
        return at == TypeOf(b) && Compare(at, a, b) == 0;
    }

    /// <summary><c>BooleanQueryExpression.EqualsWithStringCoercion</c> — the <c>==</c> and <c>!=</c> operators.</summary>
    public static bool EqualsWithStringCoercion(in Prim value, in Prim queryValue)
    {
        if (ValuesEquals(value, queryValue)) return true;

        // Handle comparing an integer with a float, e.g. 1 and 1.0
        var vt = TypeOf(value);
        var qt = TypeOf(queryValue);
        if ((vt == NType.Integer && qt == NType.Float) || (vt == NType.Float && qt == NType.Integer))
            return Compare(vt, value, queryValue) == 0;

        if (qt != NType.String) return false;

        // The only coercion that applies to JSON-derived values: a Date is compared as its ISO 8601 text.
        if (vt == NType.Date)
            return string.Equals(NewtonsoftDates.ToIsoString((DateTime)value.O), queryValue.Str, StringComparison.Ordinal);

        return false;
    }

    /// <summary><c>BooleanQueryExpression.EqualsWithStrictMatch</c> — the <c>===</c> and <c>!==</c> operators.</summary>
    public static bool EqualsWithStrictMatch(in Prim value, in Prim queryValue)
    {
        var vt = TypeOf(value);
        var qt = TypeOf(queryValue);
        if ((vt == NType.Integer && qt == NType.Float) || (vt == NType.Float && qt == NType.Integer))
            return Compare(vt, value, queryValue) == 0;

        // we handle floats and integers the exact same way, so they are pseudo equivalent
        if (vt != qt) return false;
        return ValuesEquals(value, queryValue);
    }
}
