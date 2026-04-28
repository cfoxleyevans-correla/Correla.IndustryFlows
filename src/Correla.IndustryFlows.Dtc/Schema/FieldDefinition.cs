namespace Correla.IndustryFlows.Dtc.Schema;

/// <summary>
/// A single field position within a group definition.
/// Fields are positional — the index in this list maps to the pipe-delimited
/// field position in the flat file (after the group code).
/// </summary>
/// <param name="Ref">J-reference for the corresponding Data Item (e.g. <c>J0003</c>).</param>
/// <param name="Name">Human-readable field name from the catalogue.</param>
/// <param name="Format">DTC format string (e.g. <c>NUM(13)</c>, <c>CHAR(1)</c>).</param>
/// <param name="Required">Whether the field must be present and non-empty.</param>
public sealed record FieldDefinition(
    string Ref,
    string Name,
    string Format,
    bool Required);

