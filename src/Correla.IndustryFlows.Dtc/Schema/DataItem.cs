namespace Correla.IndustryFlows.Dtc.Schema;

/// <summary>
/// A Data Item from the DTC Data Item Catalogue.
/// Keyed by J-reference (e.g. <c>J0003</c>, <c>J0171</c>).
/// Used by the parser to coerce and validate field values.
/// </summary>
public sealed class DataItem
{
    /// <summary>J-reference identifier (e.g. <c>J0171</c>).</summary>
    public required string Ref { get; init; }

    /// <summary>Human-readable name from the catalogue.</summary>
    public required string Name { get; init; }

    /// <summary>High-level domain classification (e.g. <c>Code</c>, <c>DateTime</c>).</summary>
    public required string Domain { get; init; }

    /// <summary>DTC logical format string (e.g. <c>CHAR(1)</c>, <c>NUM(13)</c>).</summary>
    public required string LogicalFormat { get; init; }

    /// <summary>Maximum physical length as a string (as stored in the catalogue).</summary>
    public required string PhysicalLength { get; init; }

    /// <summary>Valid-set constraint — may be enum, free-text, or none.</summary>
    public required ValidSet ValidSet { get; init; }

    /// <summary>Catalogue notes, if any.</summary>
    public required string Notes { get; init; }
}

