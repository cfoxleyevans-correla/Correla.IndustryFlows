namespace Correla.IndustryFlows.Dtc.Schema;

/// <summary>
/// A permitted code and its human-readable label in an enumerated valid set.
/// </summary>
/// <param name="Code">The exact code value as it appears in the flat file.</param>
/// <param name="Label">Human-readable description of the code.</param>
public sealed record EnumValue(string Code, string Label);

/// <summary>
/// The valid-set constraint for a <see cref="DataItem"/>.
/// Three kinds exist: <c>enum</c> (explicit list), <c>constraint</c> (free-text rule),
/// and <c>none</c> (no valid-set information — rely on format/domain only).
/// </summary>
public sealed class ValidSet
{
    /// <summary>
    /// Kind discriminator: <c>"enum"</c>, <c>"constraint"</c>, or <c>"none"</c>.
    /// </summary>
    public required string Kind { get; init; }

    /// <summary>
    /// Permitted values when <see cref="Kind"/> is <c>"enum"</c>; <c>null</c> otherwise.
    /// </summary>
    public IReadOnlyList<EnumValue>? EnumValues { get; init; }

    /// <summary>
    /// Free-text rule description when <see cref="Kind"/> is <c>"constraint"</c>; <c>null</c> otherwise.
    /// </summary>
    public string? ConstraintText { get; init; }
}

