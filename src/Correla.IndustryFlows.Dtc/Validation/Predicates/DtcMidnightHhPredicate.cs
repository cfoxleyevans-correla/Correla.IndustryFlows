namespace Correla.IndustryFlows.Dtc.Validation.Predicates;

/// <summary>
/// Enforces the DTC midnight half-hourly convention.
/// When the sender is an HHDC and the value's time portion is 00:00:00,
/// the field fails — HHDC midnight readings must use 23:59:59 of the same day
/// per the catalogue's note for J0016.
/// The predicate reads the <c>senderRole</c> key from the parent group's metadata
/// via the processing context embedded in the instance chain.
/// If no senderRole is available the predicate passes (non-HHDC contexts are exempt).
/// </summary>
public sealed class DtcMidnightHhPredicate : IPredicate
{
    /// <inheritdoc/>
    public string Name => "dtcMidnightHh";

    /// <inheritdoc/>
    public bool Evaluate(string value, GroupInstance instance)
    {
        // Only meaningful for 14-char DATETIME values.
        if (value.Length != 14)
        {
            return true;
        }

        // Check whether the time portion is midnight (hhmmss = 000000).
        var timePart = value[8..];
        if (timePart != "000000")
        {
            return true; // Not midnight — no issue regardless of sender role.
        }

        // Walk the instance chain looking for a stored senderRole field.
        // DtcProcessor stores the resolved role on the root when available.
        var root = FindRoot(instance);
        if (!root.Fields.TryGetValue("__senderRole", out var roleObj))
        {
            return true; // No sender role context — pass.
        }

        var role = roleObj?.ToString() ?? string.Empty;

        // HHDC midnight readings must use 23:59:59 — midnight is invalid.
        return !role.Equals("HHDC", StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>Walks up to the synthetic root instance.</summary>
    private static GroupInstance FindRoot(GroupInstance instance)
    {
        var current = instance;
        while (current.Parent is not null)
        {
            current = current.Parent;
        }

        return current;
    }
}

