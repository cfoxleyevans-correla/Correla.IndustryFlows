using System.Globalization;

namespace Correla.IndustryFlows.Dtc.Validation.Predicates;

/// <summary>
/// Validates that a value conforms to the DTC 14-character DATETIME format
/// (<c>ccyymmddhhmmss</c>). Returns false when the string cannot be parsed.
/// </summary>
public sealed class DtcDateTimePredicate : IPredicate
{
    /// <inheritdoc/>
    public string Name => "dtcDateTime";

    /// <inheritdoc/>
    public bool Evaluate(string value, GroupInstance instance)
    {
        if (value.Length != 14)
        {
            return false;
        }

        foreach (char c in value)
        {
            if (c < '0' || c > '9')
            {
                return false;
            }
        }

        int year = int.Parse(value[..4], CultureInfo.InvariantCulture);
        int month = int.Parse(value[4..6], CultureInfo.InvariantCulture);
        int day = int.Parse(value[6..8], CultureInfo.InvariantCulture);
        int hour = int.Parse(value[8..10], CultureInfo.InvariantCulture);
        int min = int.Parse(value[10..12], CultureInfo.InvariantCulture);
        int sec = int.Parse(value[12..14], CultureInfo.InvariantCulture);

        try
        {
            _ = new DateTime(year, month, day, hour, min, sec);
            return true;
        }
        catch (ArgumentOutOfRangeException)
        {
            return false;
        }
    }
}

