namespace Correla.IndustryFlows.Dtc.Schema;

/// <summary>
/// An allowed routing path for a flow — the From and To participant roles
/// that may exchange this flow under the current catalogue version.
/// </summary>
/// <param name="From">Sending participant role code.</param>
/// <param name="To">Receiving participant role code.</param>
/// <param name="Version">DTC version under which this route is valid.</param>
public sealed record FlowRoute(string From, string To, string Version);

/// <summary>
/// The complete schema for a single DTC flow version, loaded from the bundle.
/// Groups are keyed by their group code (an opaque string such as <c>026</c> or <c>82B</c>).
/// </summary>
public sealed class FlowSchema
{
    /// <summary>DTC flow identifier (e.g. <c>D0010</c>).</summary>
    public required string FlowId { get; init; }

    /// <summary>Catalogue version of this flow (e.g. <c>002</c>).</summary>
    public required string FlowVersion { get; init; }

    /// <summary>Human-readable flow name.</summary>
    public required string FlowName { get; init; }

    /// <summary>Operational status as recorded in the catalogue.</summary>
    public required string Status { get; init; }

    /// <summary>Owning organisation (e.g. <c>MRA</c>).</summary>
    public required string Ownership { get; init; }

    /// <summary>Free-text description of the flow's purpose.</summary>
    public required string Description { get; init; }

    /// <summary>Permitted participant-role routing pairs for this flow.</summary>
    public required IReadOnlyList<FlowRoute> Routes { get; init; }

    /// <summary>
    /// Group definitions keyed by group code.
    /// Group codes are opaque strings — do not assume numeric ordering.
    /// </summary>
    public required IReadOnlyDictionary<string, GroupDefinition> Groups { get; init; }

    /// <summary>
    /// Rule-pack rules merged from the companion rules file at load time.
    /// Empty when no rule pack exists for this flow.
    /// </summary>
    public required IReadOnlyList<Rule> Rules { get; init; }

    /// <summary>Catalogue notes for the flow.</summary>
    public required string Notes { get; init; }
}

