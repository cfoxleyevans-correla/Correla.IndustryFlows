namespace Correla.IndustryFlows.Dtc.Schema;

/// <summary>
/// A single row in the manifest index — maps a (flowId, flowVersion) pair
/// to the relative path of the per-flow JSON file within the bundle.
/// </summary>
public sealed record ManifestEntry(
    string FlowId,
    string FlowVersion,
    string FlowName,
    string File);

