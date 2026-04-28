using System.Globalization;
using System.Text.RegularExpressions;
using Correla.IndustryFlows.Dtc.Schema;

namespace Correla.IndustryFlows.Dtc.Parsing;

/// <summary>
/// Converts a raw pipe-delimited field string into a typed .NET value
/// according to the DTC logical format declared in its <see cref="DataItem"/>.
/// Returns a <c>(Value, Error)</c> tuple — <c>Error</c> is non-null on failure.
/// </summary>
public static class FieldCoercer
{
    // Matches NUM formats: "NUM(9)" or "NUM(9 ,1)" (with optional decimal spec).
    private static readonly Regex NumFormat =
        new(@"^NUM\((\d+)(?:\s*,\s*(\d+))?\)$", RegexOptions.Compiled | RegexOptions.IgnoreCase);

    // Matches CHAR formats: "CHAR(10)".
    private static readonly Regex CharFormat =
        new(@"^CHAR\((\d+)\)$", RegexOptions.Compiled | RegexOptions.IgnoreCase);

    // Matches INT formats: "INT(3)".
    private static readonly Regex IntFormat =
        new(@"^INT\((\d+)\)$", RegexOptions.Compiled | RegexOptions.IgnoreCase);

    // Matches DATETIME formats: "DATETIME" or "DATETIME(14)".
    private static readonly Regex DateTimeFormat =
        new(@"^DATETIME(\(\d+\))?$", RegexOptions.Compiled | RegexOptions.IgnoreCase);

    // Matches DATE formats: "DATE" or "DATE(8)".
    private static readonly Regex DateFormat =
        new(@"^DATE(\(\d+\))?$", RegexOptions.Compiled | RegexOptions.IgnoreCase);

    // Matches TIME formats: "TIME" or "TIME(6)".
    private static readonly Regex TimeFormat =
        new(@"^TIME(\(\d+\))?$", RegexOptions.Compiled | RegexOptions.IgnoreCase);

    // Matches BOOLEAN formats: "BOOLEAN" or "BOOLEAN(1)".
    private static readonly Regex BoolFormat =
        new(@"^BOOLEAN(\(\d+\))?$", RegexOptions.Compiled | RegexOptions.IgnoreCase);

    // Matches TIMESTAMP formats: "TIMESTAMP" or "TIMESTAMP(14)".
    private static readonly Regex TimestampFormat =
        new(@"^TIMESTAMP(\(\d+\))?$", RegexOptions.Compiled | RegexOptions.IgnoreCase);

    /// <summary>
    /// Coerces <paramref name="raw"/> into the appropriate .NET type declared
    /// by <paramref name="item"/>'s logical format.
    /// </summary>
    /// <returns>
    /// <c>(Value, null)</c> on success; <c>(null, errorMessage)</c> on failure.
    /// </returns>
    public static (object? Value, string? Error) Coerce(string raw, DataItem item)
    {
        var fmt = item.LogicalFormat;

        // DATETIME must be tested before DATE because "DATETIME" starts with "DATE".
        if (DateTimeFormat.IsMatch(fmt) || TimestampFormat.IsMatch(fmt))
        {
            return CoerceDateTime(raw, item.Ref);
        }

        if (DateFormat.IsMatch(fmt))
        {
            return CoerceDate(raw, item.Ref);
        }

        if (TimeFormat.IsMatch(fmt))
        {
            return CoerceTime(raw, item.Ref);
        }

        if (BoolFormat.IsMatch(fmt))
        {
            return CoerceBool(raw, item.Ref);
        }

        var numMatch = NumFormat.Match(fmt);
        if (numMatch.Success)
        {
            int maxLen = int.Parse(numMatch.Groups[1].Value, System.Globalization.CultureInfo.InvariantCulture);
            string? decimalPlaces = numMatch.Groups[2].Success ? numMatch.Groups[2].Value : null;

            return decimalPlaces is not null
                ? CoerceNumDecimal(raw, item.Ref, maxLen, int.Parse(decimalPlaces, System.Globalization.CultureInfo.InvariantCulture))
                : CoerceNumString(raw, item.Ref, maxLen);
        }

        var charMatch = CharFormat.Match(fmt);
        if (charMatch.Success)
        {
            int maxLen = int.Parse(charMatch.Groups[1].Value, System.Globalization.CultureInfo.InvariantCulture);
            return CoerceChar(raw, item.Ref, maxLen, item.ValidSet);
        }

        var intMatch = IntFormat.Match(fmt);
        if (intMatch.Success)
        {
            int maxLen = int.Parse(intMatch.Groups[1].Value, System.Globalization.CultureInfo.InvariantCulture);
            return CoerceInt(raw, item.Ref, maxLen);
        }

        // Fallback for unknown formats: accept as string with no validation.
        return (raw, null);
    }

    // ---- Type-specific coercers ----

    /// <summary>
    /// Coerces a NUM(n) field as a string, preserving leading zeros.
    /// Validates that all characters are digits and that the length is within the limit.
    /// </summary>
    private static (object?, string?) CoerceNumString(string raw, string jRef, int maxLen)
    {
        if (!IsAllDigits(raw))
        {
            return (null, $"{jRef}: expected numeric digits, got '{raw}'.");
        }

        if (raw.Length > maxLen)
        {
            return (null, $"{jRef}: value '{raw}' exceeds maximum length {maxLen}.");
        }

        return (raw, null);
    }

    /// <summary>
    /// Coerces a NUM(n ,m) field as a decimal, shifting the decimal point m places from the right.
    /// </summary>
    private static (object?, string?) CoerceNumDecimal(string raw, string jRef, int maxLen, int decimalPlaces)
    {
        if (!IsAllDigits(raw))
        {
            return (null, $"{jRef}: expected numeric digits for decimal value, got '{raw}'.");
        }

        if (raw.Length > maxLen)
        {
            return (null, $"{jRef}: value '{raw}' exceeds maximum length {maxLen}.");
        }

        // Shift the decimal point: 045231 with decimalPlaces=1 → 4523.1
        var divisor = (decimal)Math.Pow(10, decimalPlaces);
        var value = decimal.Parse(raw, CultureInfo.InvariantCulture) / divisor;

        return (value, null);
    }

    /// <summary>Coerces a CHAR(n) field as a string, with optional enum validation.</summary>
    private static (object?, string?) CoerceChar(string raw, string jRef, int maxLen, ValidSet validSet)
    {
        if (raw.Length > maxLen)
        {
            return (null, $"{jRef}: value '{raw}' exceeds maximum length {maxLen}.");
        }

        if (validSet.Kind == "enum" && validSet.EnumValues is not null)
        {
            var allowed = validSet.EnumValues.Select(v => v.Code).ToHashSet(StringComparer.Ordinal);
            if (!allowed.Contains(raw))
            {
                return (null, $"{jRef}: value '{raw}' is not in the permitted set [{string.Join(", ", allowed.OrderBy(c => c))}].");
            }
        }

        return (raw, null);
    }

    /// <summary>Coerces a DATE field as <see cref="DateOnly"/> from ccyymmdd.</summary>
    private static (object?, string?) CoerceDate(string raw, string jRef)
    {
        if (raw.Length != 8 || !IsAllDigits(raw))
        {
            return (null, $"{jRef}: expected DATE in ccyymmdd format, got '{raw}'.");
        }

        int year = int.Parse(raw[..4], CultureInfo.InvariantCulture);
        int month = int.Parse(raw[4..6], CultureInfo.InvariantCulture);
        int day = int.Parse(raw[6..8], CultureInfo.InvariantCulture);

        try
        {
            return (new DateOnly(year, month, day), null);
        }
        catch (ArgumentOutOfRangeException)
        {
            return (null, $"{jRef}: invalid DATE value '{raw}'.");
        }
    }

    /// <summary>Coerces a DATETIME field as <see cref="DateTime"/> from ccyymmddhhmmss.</summary>
    private static (object?, string?) CoerceDateTime(string raw, string jRef)
    {
        if (raw.Length != 14 || !IsAllDigits(raw))
        {
            return (null, $"{jRef}: expected DATETIME in ccyymmddhhmmss format (14 digits), got '{raw}'.");
        }

        int year = int.Parse(raw[..4], CultureInfo.InvariantCulture);
        int month = int.Parse(raw[4..6], CultureInfo.InvariantCulture);
        int day = int.Parse(raw[6..8], CultureInfo.InvariantCulture);
        int hour = int.Parse(raw[8..10], CultureInfo.InvariantCulture);
        int min = int.Parse(raw[10..12], CultureInfo.InvariantCulture);
        int sec = int.Parse(raw[12..14], CultureInfo.InvariantCulture);

        try
        {
            return (new DateTime(year, month, day, hour, min, sec, DateTimeKind.Unspecified), null);
        }
        catch (ArgumentOutOfRangeException)
        {
            return (null, $"{jRef}: invalid DATETIME value '{raw}'.");
        }
    }

    /// <summary>Coerces a TIME field as <see cref="TimeOnly"/> from hhmmss.</summary>
    private static (object?, string?) CoerceTime(string raw, string jRef)
    {
        if (raw.Length != 6 || !IsAllDigits(raw))
        {
            return (null, $"{jRef}: expected TIME in hhmmss format (6 digits), got '{raw}'.");
        }

        int hour = int.Parse(raw[..2], System.Globalization.CultureInfo.InvariantCulture);
        int min = int.Parse(raw[2..4], System.Globalization.CultureInfo.InvariantCulture);
        int sec = int.Parse(raw[4..6], System.Globalization.CultureInfo.InvariantCulture);

        try
        {
            return (new TimeOnly(hour, min, sec), null);
        }
        catch (ArgumentOutOfRangeException)
        {
            return (null, $"{jRef}: invalid TIME value '{raw}'.");
        }
    }

    /// <summary>Coerces a BOOLEAN field — accepts only 'T' (true) or 'F' (false).</summary>
    private static (object?, string?) CoerceBool(string raw, string jRef)
    {
        if (raw == "T")
        {
            return (true, null);
        }

        if (raw == "F")
        {
            return (false, null);
        }

        return (null, $"{jRef}: expected BOOLEAN 'T' or 'F', got '{raw}'.");
    }

    /// <summary>Coerces an INT(n) field as <see cref="int"/>.</summary>
    private static (object?, string?) CoerceInt(string raw, string jRef, int maxLen)
    {
        if (raw.Length > maxLen)
        {
            return (null, $"{jRef}: value '{raw}' exceeds maximum length {maxLen}.");
        }

        if (!int.TryParse(raw, out int result))
        {
            return (null, $"{jRef}: expected integer, got '{raw}'.");
        }

        return (result, null);
    }

    /// <summary>Returns true when every character in <paramref name="s"/> is a decimal digit.</summary>
    private static bool IsAllDigits(string s)
    {
        foreach (char c in s)
        {
            if (c < '0' || c > '9')
            {
                return false;
            }
        }

        return s.Length > 0;
    }
}

