namespace Correla.IndustryFlows.Dtc.Validation.Predicates;

/// <summary>
/// Validates that the field value is unique across all siblings of the same group code
/// within the same parent instance.
/// Uniqueness is checked by comparing the raw string value of the same field
/// across sibling instances.
/// </summary>
public sealed class UniqueWithinGroupPredicate : IPredicate
{
    /// <inheritdoc/>
    public string Name => "uniqueWithinGroup";

    /// <inheritdoc/>
    public bool Evaluate(string value, GroupInstance instance)
    {
        var parent = instance.Parent;
        if (parent is null)
        {
            return true; // No siblings possible.
        }

        // Find the field name that holds this value by searching the instance's Fields.
        // The predicate is evaluated for a specific field; find which one matches the value.
        var matchingKeys = instance.Fields
            .Where(kv => kv.Value?.ToString() == value)
            .Select(kv => kv.Key)
            .ToList();

        if (matchingKeys.Count == 0)
        {
            return true;
        }

        var fieldRef = matchingKeys[0];

        // Count how many siblings have the same field value.
        var duplicateCount = parent.Children
            .Where(c => c != instance && c.GroupCode == instance.GroupCode)
            .Count(c => c.Fields.TryGetValue(fieldRef, out var v) && v?.ToString() == value);

        return duplicateCount == 0;
    }
}

