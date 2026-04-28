namespace Correla.IndustryFlows.Dtc.Validation.Predicates;

/// <summary>
/// Validates the modulus-11 check digit on a 13-digit MPAN Core.
/// Weights applied to the first 12 digits: 3, 5, 7, 13, 17, 19, 23, 29, 31, 37, 41, 43.
/// Check digit = (sum of weighted digits) mod 11 mod 10.
/// </summary>
public sealed class MpanCheckDigitPredicate : IPredicate
{
    /// <inheritdoc/>
    public string Name => "mpanCheckDigit";

    private static readonly int[] Weights = [3, 5, 7, 13, 17, 19, 23, 29, 31, 37, 41, 43];

    /// <inheritdoc/>
    public bool Evaluate(string value, GroupInstance instance)
    {
        if (value.Length != 13)
        {
            return false;
        }

        int sum = 0;

        for (int i = 0; i < 12; i++)
        {
            if (value[i] < '0' || value[i] > '9')
            {
                return false;
            }

            sum += (value[i] - '0') * Weights[i];
        }

        int expected = sum % 11 % 10;
        int actual = value[12] - '0';

        return expected == actual;
    }
}

