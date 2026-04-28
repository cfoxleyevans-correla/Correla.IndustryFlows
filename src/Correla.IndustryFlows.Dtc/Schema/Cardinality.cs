namespace Correla.IndustryFlows.Dtc.Schema;

/// <summary>
/// Minimum and maximum occurrence count for a group within its parent context.
/// <c>null</c> for <see cref="Max"/> means unbounded (the catalogue's <c>*</c> notation).
/// </summary>
/// <param name="Min">Minimum number of occurrences required (0 = optional).</param>
/// <param name="Max">Maximum allowed occurrences, or <c>null</c> for unbounded.</param>
public sealed record Cardinality(int Min, int? Max);

