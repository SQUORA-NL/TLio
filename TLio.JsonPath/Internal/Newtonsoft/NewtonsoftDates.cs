// Portions of this file are derived from Newtonsoft.Json (DateTimeUtils.cs, DateTimeParser.cs),
// Copyright (c) 2007 James Newton-King, MIT licensed:
//
//   Permission is hereby granted, free of charge, to any person obtaining a copy of this software and
//   associated documentation files (the "Software"), to deal in the Software without restriction,
//   including without limitation the rights to use, copy, modify, merge, publish, distribute,
//   sublicense, and/or sell copies of the Software, and to permit persons to whom the Software is
//   furnished to do so, subject to the following conditions: The above copyright notice and this
//   permission notice shall be included in all copies or substantial portions of the Software.
//   THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND.
//
// The algorithms are reproduced rather than approximated on purpose: Newtonsoft turns a JSON
// string that looks like a date into a *date value*, and that changes how it compares (see
// NewtonsoftValues). To behave identically on a System.Text.Json document — where such a value is
// still a plain string — the same recognition has to happen at comparison time, bug for bug.
#nullable disable
using System.Globalization;

namespace TLio.JsonPath.Internal.Newtonsoft;

/// <summary>Recognises the strings <c>JsonTextReader</c> reads as dates (<c>DateParseHandling.DateTime</c>, <c>DateTimeZoneHandling.RoundtripKind</c>) and writes them back the way <c>JValue</c> does.</summary>
internal static class NewtonsoftDates
{
    private const long InitialJavaScriptDateTicks = 621355968000000000;

    /// <summary>
    /// <c>DateTimeUtils.TryParseDateTime</c>: an ISO 8601 string of 19–40 characters with a 'T' at index 10,
    /// or a Microsoft <c>/Date(ms±hhmm)/</c> string. A time zone designator moves the value into local time,
    /// so the result (and anything compared with it) depends on the machine's time zone — exactly as in Newtonsoft.
    /// </summary>
    public static bool TryParse(string s, out DateTime dt)
    {
        if (s.Length > 0)
        {
            if (s[0] == '/')
            {
                if (s.Length >= 9 && s.StartsWith("/Date(", StringComparison.Ordinal) && s.EndsWith(")/", StringComparison.Ordinal))
                    return TryParseMicrosoft(s, out dt);
            }
            else if (s.Length >= 19 && s.Length <= 40 && char.IsDigit(s[0]) && s[10] == 'T')
            {
                return TryParseIso(s, out dt);
            }
        }

        dt = default;
        return false;
    }

    private static bool TryParseIso(string text, out DateTime dt)
    {
        var parser = new IsoParser();
        if (!parser.Parse(text))
        {
            dt = default;
            return false;
        }

        var d = CreateDateTime(parser);
        long ticks;
        switch (parser.Zone)
        {
            case Zone.Utc:
                d = new DateTime(d.Ticks, DateTimeKind.Utc);
                break;
            case Zone.LocalWestOfUtc:
            {
                var offset = new TimeSpan(parser.ZoneHour, parser.ZoneMinute, 0);
                ticks = d.Ticks + offset.Ticks;
                if (ticks <= DateTime.MaxValue.Ticks)
                {
                    d = new DateTime(ticks, DateTimeKind.Utc).ToLocalTime();
                }
                else
                {
                    ticks += TimeZoneInfo.Local.GetUtcOffset(d).Ticks;
                    if (ticks > DateTime.MaxValue.Ticks) ticks = DateTime.MaxValue.Ticks;
                    d = new DateTime(ticks, DateTimeKind.Local);
                }

                break;
            }
            case Zone.LocalEastOfUtc:
            {
                var offset = new TimeSpan(parser.ZoneHour, parser.ZoneMinute, 0);
                ticks = d.Ticks - offset.Ticks;
                if (ticks >= DateTime.MinValue.Ticks)
                {
                    d = new DateTime(ticks, DateTimeKind.Utc).ToLocalTime();
                }
                else
                {
                    ticks += TimeZoneInfo.Local.GetUtcOffset(d).Ticks;
                    if (ticks < DateTime.MinValue.Ticks) ticks = DateTime.MinValue.Ticks;
                    d = new DateTime(ticks, DateTimeKind.Local);
                }

                break;
            }
        }

        dt = d; // DateTimeZoneHandling.RoundtripKind: EnsureDateTime leaves the value alone.
        return true;
    }

    private static DateTime CreateDateTime(IsoParser p)
    {
        bool is24Hour;
        if (p.Hour == 24)
        {
            is24Hour = true;
            p.Hour = 0;
        }
        else
        {
            is24Hour = false;
        }

        var d = new DateTime(p.Year, p.Month, p.Day, p.Hour, p.Minute, p.Second);
        d = d.AddTicks(p.Fraction);
        if (is24Hour) d = d.AddDays(1);
        return d;
    }

    private static bool TryParseMicrosoft(string text, out DateTime dt)
    {
        // /Date(1234567890000)/  /Date(1234567890000+0100)/
        var kind = DateTimeKind.Utc;
        var index = text.IndexOf('+', 7, text.Length - 8);
        if (index == -1) index = text.IndexOf('-', 7, text.Length - 8);
        if (index != -1)
        {
            kind = DateTimeKind.Local;
            var negative = text[index] == '-';
            if (index + 3 > text.Length || !TryInt(text, index + 1, 2, out _)) { dt = default; return false; }
            if (text.Length - index > 5 && !TryInt(text, index + 3, 2, out _)) { dt = default; return false; }
        }
        else
        {
            index = text.Length - 2;
        }

        if (!long.TryParse(text.AsSpan(6, index - 6), NumberStyles.AllowLeadingSign, CultureInfo.InvariantCulture, out var ms))
        {
            dt = default;
            return false;
        }

        var utc = new DateTime((ms * 10000) + InitialJavaScriptDateTicks, DateTimeKind.Utc);
        dt = kind == DateTimeKind.Local ? utc.ToLocalTime() : utc;
        return true;
    }

    private static bool TryInt(string s, int start, int length, out int value)
    {
        value = 0;
        if (start + length > s.Length) return false;
        for (var i = 0; i < length; i++)
        {
            var c = s[start + i];
            if (c is < '0' or > '9') return false;
            value = value * 10 + (c - '0');
        }

        return true;
    }

    /// <summary><c>DateTimeUtils.WriteDateTimeString</c> with <c>DateFormatHandling.IsoDateFormat</c>: what <c>EqualsWithStringCoercion</c> compares a query string against.</summary>
    public static string ToIsoString(DateTime value)
    {
        var chars = new char[64];
        var pos = WriteDefaultIsoDate(chars, 0, value);
        switch (value.Kind)
        {
            case DateTimeKind.Local:
                pos = WriteOffset(chars, pos, TimeZoneInfo.Local.GetUtcOffset(value));
                break;
            case DateTimeKind.Utc:
                chars[pos++] = 'Z';
                break;
        }

        return new string(chars, 0, pos);
    }

    private static int WriteDefaultIsoDate(char[] chars, int start, DateTime dt)
    {
        var length = 19;
        CopyInt(chars, start, dt.Year, 4);
        chars[start + 4] = '-';
        CopyInt(chars, start + 5, dt.Month, 2);
        chars[start + 7] = '-';
        CopyInt(chars, start + 8, dt.Day, 2);
        chars[start + 10] = 'T';
        CopyInt(chars, start + 11, dt.Hour, 2);
        chars[start + 13] = ':';
        CopyInt(chars, start + 14, dt.Minute, 2);
        chars[start + 16] = ':';
        CopyInt(chars, start + 17, dt.Second, 2);
        var fraction = (int)(dt.Ticks % 10000000L);
        if (fraction != 0)
        {
            var digits = 7;
            while (fraction % 10 == 0)
            {
                digits--;
                fraction /= 10;
            }

            chars[start + 19] = '.';
            CopyInt(chars, start + 20, fraction, digits);
            length += digits + 1;
        }

        return start + length;
    }

    private static int WriteOffset(char[] chars, int start, TimeSpan offset)
    {
        chars[start++] = offset.Ticks >= 0L ? '+' : '-';
        CopyInt(chars, start, Math.Abs(offset.Hours), 2);
        start += 2;
        chars[start++] = ':';
        CopyInt(chars, start, Math.Abs(offset.Minutes), 2);
        start += 2;
        return start;
    }

    private static void CopyInt(char[] chars, int start, int value, int digits)
    {
        while (digits-- != 0)
        {
            chars[start + digits] = (char)((value % 10) + 48);
            value /= 10;
        }
    }

    private enum Zone
    {
        Unspecified,
        Utc,
        LocalWestOfUtc,
        LocalEastOfUtc,
    }

    /// <summary>Newtonsoft's hand-written ISO 8601 recogniser (<c>DateTimeParser</c>), transcribed.</summary>
    private struct IsoParser
    {
        public int Year, Month, Day, Hour, Minute, Second, Fraction, ZoneHour, ZoneMinute;
        public Zone Zone;
        private string _text;
        private int _end;

        private static readonly int[] Power10 = [-1, 10, 100, 1000, 10000, 100000, 1000000];
        private const int Lzyyyy = 4, Lzyyyy_ = 5, Lzyyyy_MM = 7, Lzyyyy_MM_ = 8, Lzyyyy_MM_dd = 10, Lzyyyy_MM_ddT = 11;
        private const int LzHH = 2, LzHH_ = 3, LzHH_mm = 5, LzHH_mm_ = 6, LzHH_mm_ss = 8, Lz_ = 1, Lz_zz = 3;
        private const short MaxFractionDigits = 7;

        public bool Parse(string text)
        {
            _text = text;
            _end = text.Length;
            return ParseDate(0) && ParseChar(Lzyyyy_MM_dd, 'T') && ParseTimeAndZone(Lzyyyy_MM_ddT);
        }

        private bool ParseDate(int start) =>
            Parse4Digit(start, out Year)
            && 1 <= Year
            && ParseChar(start + Lzyyyy, '-')
            && Parse2Digit(start + Lzyyyy_, out Month)
            && 1 <= Month
            && Month <= 12
            && ParseChar(start + Lzyyyy_MM, '-')
            && Parse2Digit(start + Lzyyyy_MM_, out Day)
            && 1 <= Day
            && Day <= DateTime.DaysInMonth(Year, Month);

        private bool ParseTimeAndZone(int start) => ParseTime(ref start) && ParseZone(start);

        private bool ParseTime(ref int start)
        {
            if (!(Parse2Digit(start, out Hour)
                  && Hour <= 24
                  && ParseChar(start + LzHH, ':')
                  && Parse2Digit(start + LzHH_, out Minute)
                  && Minute < 60
                  && ParseChar(start + LzHH_mm, ':')
                  && Parse2Digit(start + LzHH_mm_, out Second)
                  && Second < 60
                  && (Hour != 24 || (Minute == 0 && Second == 0))))
                return false;

            start += LzHH_mm_ss;
            if (ParseChar(start, '.'))
            {
                Fraction = 0;
                var numberOfDigits = 0;
                while (++start < _end && numberOfDigits < MaxFractionDigits)
                {
                    var digit = _text[start] - '0';
                    if (digit < 0 || digit > 9) break;
                    Fraction = (Fraction * 10) + digit;
                    numberOfDigits++;
                }

                if (numberOfDigits < MaxFractionDigits)
                {
                    if (numberOfDigits == 0) return false;
                    Fraction *= Power10[MaxFractionDigits - numberOfDigits];
                }

                if (Hour == 24 && Fraction != 0) return false;
            }

            return true;
        }

        private bool ParseZone(int start)
        {
            if (start < _end)
            {
                var ch = _text[start];
                if (ch == 'Z' || ch == 'z')
                {
                    Zone = Zone.Utc;
                    start++;
                }
                else
                {
                    if (start + 2 < _end && Parse2Digit(start + Lz_, out ZoneHour) && ZoneHour <= 99)
                    {
                        switch (ch)
                        {
                            case '-':
                                Zone = Zone.LocalWestOfUtc;
                                start += Lz_zz;
                                break;
                            case '+':
                                Zone = Zone.LocalEastOfUtc;
                                start += Lz_zz;
                                break;
                        }
                    }

                    if (start < _end)
                    {
                        if (ParseChar(start, ':'))
                        {
                            start += 1;
                            if (start + 1 < _end && Parse2Digit(start, out ZoneMinute) && ZoneMinute <= 99) start += 2;
                        }
                        else if (start + 1 < _end && Parse2Digit(start, out ZoneMinute) && ZoneMinute <= 99)
                        {
                            start += 2;
                        }
                    }
                }
            }

            return start == _end;
        }

        private bool Parse4Digit(int start, out int num)
        {
            if (start + 3 < _end)
            {
                int d1 = _text[start] - '0', d2 = _text[start + 1] - '0', d3 = _text[start + 2] - '0', d4 = _text[start + 3] - '0';
                if (0 <= d1 && d1 < 10 && 0 <= d2 && d2 < 10 && 0 <= d3 && d3 < 10 && 0 <= d4 && d4 < 10)
                {
                    num = (((((d1 * 10) + d2) * 10) + d3) * 10) + d4;
                    return true;
                }
            }

            num = 0;
            return false;
        }

        private bool Parse2Digit(int start, out int num)
        {
            if (start + 1 < _end)
            {
                int d1 = _text[start] - '0', d2 = _text[start + 1] - '0';
                if (0 <= d1 && d1 < 10 && 0 <= d2 && d2 < 10)
                {
                    num = (d1 * 10) + d2;
                    return true;
                }
            }

            num = 0;
            return false;
        }

        private bool ParseChar(int start, char ch) => start < _end && _text[start] == ch;
    }
}
