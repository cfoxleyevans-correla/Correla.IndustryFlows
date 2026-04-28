namespace Correla.IndustryFlows.Dtc.Schema;

/// <summary>
/// Definition of a repeatable group within a DTC flow.
/// Groups nest hierarchically; the <see cref="Parent"/> code identifies the
/// enclosing group (or <c>null</c> for root-level groups).
/// </summary>
public sealed class GroupDefinition
{
    /// <summary>Human-readable name from the catalogue (e.g. <c>MPAN Cores</c>).</summary>
    public required string Name { get; init; }

    /// <summary>
    /// Group code of the parent, or <c>null</c> if this group sits at the root level.
    /// Group codes are opaque strings — never assumed to be numeric.
    /// </summary>
    public required string? Parent { get; init; }

    /// <summary>Nesting depth, where 1 is root-level (matches the catalogue's L column).</summary>
    public required int Level { get; init; }

    /// <summary>Minimum and maximum occurrence constraints for this group within its parent.</summary>
    public required Cardinality Cardinality { get; init; }

    /// <summary>
    /// Free-text conditional note from the catalogue (e.g. "Only when meter type = S").
    /// Empty string when no condition applies.
    /// </summary>
    public required string Condition { get; init; }

    /// <summary>
    /// Ordered field definitions for this group. Index 0 corresponds to
    /// the first pipe-delimited field after the group code in the flat file.
    /// </summary>
    public required IReadOnlyList<FieldDefinition> Fields { get; init; }
}

