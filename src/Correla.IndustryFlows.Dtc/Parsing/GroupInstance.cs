using System.Text.Json.Serialization;

/// <summary>
/// A single occurrence of a group in a parsed DTC file.
/// Groups nest hierarchically according to the flow schema;
/// children are appended in the order they appear in the file.
/// </summary>
public sealed class GroupInstance
{
    /// <summary>The three-character group code (e.g. <c>026</c>, <c>82B</c>).</summary>
    public required string GroupCode { get; set; }

    /// <summary>One-based line number of this record in the source file.</summary>
    public required int LineNumber { get; init; }

    /// <summary>The parent instance, or <c>null</c> for the synthetic root.</summary>
    /// <remarks>Excluded from JSON serialisation to avoid circular references.</remarks>
    [JsonIgnore]
    public GroupInstance? Parent { get; set; }

    /// <summary>
    /// Field values bound from the line, keyed by J-reference (e.g. <c>J0003</c>).
    /// Values are coerced to their DTC domain type (<c>string</c>, <c>int</c>, <c>bool</c>, etc.).
    /// </summary>
    public Dictionary<string, object> Fields { get; } = new();

    /// <summary>Child group instances nested under this one, in file order.</summary>
    public List<GroupInstance> Children { get; } = new();
}

