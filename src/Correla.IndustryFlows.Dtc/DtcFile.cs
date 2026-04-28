namespace Correla.IndustryFlows.Dtc;

/// <summary>
/// The fully-parsed representation of a DTC flat file.
/// Wraps the root of the parsed <see cref="GroupInstance"/> tree.
/// Populated by <c>FlatFileParser</c> in Phase 2.
/// </summary>
public sealed class DtcFile
{
    /// <summary>
    /// The synthetic root node whose children are the top-level group instances
    /// found in the file.
    /// </summary>
    public required GroupInstance Root { get; init; }
}

