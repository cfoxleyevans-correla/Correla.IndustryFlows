using Correla.IndustryFlows.Dtc.Parsing;
using Correla.IndustryFlows.Dtc.Schema;

namespace Correla.IndustryFlows.Dtc.Validation;

/// <summary>
/// Validates a parsed <see cref="DtcFile"/> against its <see cref="FlowSchema"/>.
/// Checks cardinality constraints (min/max group occurrences) within each parent context.
/// Type, enum, and required-field validation is performed during parsing by
/// <see cref="FlatFileParser"/>; this validator owns post-parse structural rules only.
/// </summary>
public static class SchemaValidator
{
    /// <summary>
    /// Walks the parsed tree and emits findings for cardinality violations.
    /// </summary>
    /// <param name="file">The fully-parsed DTC file.</param>
    /// <param name="schema">The flow schema that governs this file.</param>
    /// <returns>A (possibly empty) list of cardinality findings.</returns>
    public static IReadOnlyList<Finding> Validate(DtcFile file, FlowSchema schema)
    {
        var findings = new List<Finding>();

        // Validate the root's children, then recurse into each group instance.
        ValidateChildren(file.Root, schema, findings);

        return findings.AsReadOnly();
    }

    /// <summary>
    /// For each expected child-group code under <paramref name="parent"/>,
    /// counts actual occurrences and emits SCHEMA-CARD-MIN / SCHEMA-CARD-MAX findings.
    /// Then recurses into each child instance.
    /// </summary>
    private static void ValidateChildren(GroupInstance parent, FlowSchema schema, List<Finding> findings)
    {
        // Find all group codes that declare this parent (or null for root children).
        var expectedParentCode = parent.GroupCode == "ROOT" ? null : parent.GroupCode;

        var expectedChildCodes = schema.Groups
            .Where(kv => kv.Value.Parent == expectedParentCode)
            .Select(kv => kv.Key)
            .ToList();

        foreach (var childCode in expectedChildCodes)
        {
            var def = schema.Groups[childCode];
            var count = parent.Children.Count(c => c.GroupCode == childCode);

            // Minimum cardinality check.
            if (count < def.Cardinality.Min)
            {
                findings.Add(new Finding(
                    "SCHEMA-CARD-MIN", Severity.Error,
                    BuildPath(parent, childCode),
                    $"Group '{childCode}' ({def.Name}) requires at least {def.Cardinality.Min} occurrence(s) under '{parent.GroupCode}' but found {count}."));
            }

            // Maximum cardinality check (null max means unbounded).
            if (def.Cardinality.Max.HasValue && count > def.Cardinality.Max.Value)
            {
                findings.Add(new Finding(
                    "SCHEMA-CARD-MAX", Severity.Error,
                    BuildPath(parent, childCode),
                    $"Group '{childCode}' ({def.Name}) allows at most {def.Cardinality.Max} occurrence(s) under '{parent.GroupCode}' but found {count}."));
            }
        }

        // Recurse into each child instance.
        foreach (var child in parent.Children)
        {
            ValidateChildren(child, schema, findings);
        }
    }

    /// <summary>Builds a path string for a finding.</summary>
    private static string BuildPath(GroupInstance parent, string childCode) =>
        $"{parent.GroupCode}.{childCode}";
}

