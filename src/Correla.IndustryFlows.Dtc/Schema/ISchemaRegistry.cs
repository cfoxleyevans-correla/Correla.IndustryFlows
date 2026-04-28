namespace Correla.IndustryFlows.Dtc.Schema;

/// <summary>
/// Provides access to DTC flow schemas and the data-item catalogue.
/// Implementations load schemas from a bundle directory (see <see cref="FileSchemaRegistry"/>)
/// or from any other source (useful for testing with in-memory fakes).
/// </summary>
public interface ISchemaRegistry
{
    /// <summary>All entries declared in <c>manifest.json</c>.</summary>
    IReadOnlyCollection<ManifestEntry> Manifest { get; }

    /// <summary>
    /// Attempts to retrieve the schema for the given flow and version.
    /// </summary>
    /// <param name="flowId">DTC flow identifier (e.g. <c>D0010</c>).</param>
    /// <param name="flowVersion">Flow version string (e.g. <c>002</c>).</param>
    /// <param name="schema">The resolved schema, or <c>null</c> when not found.</param>
    /// <returns><c>true</c> when the schema was found; <c>false</c> otherwise.</returns>
    bool TryGet(string flowId, string flowVersion, out FlowSchema? schema);

    /// <summary>
    /// Attempts to retrieve a data item by its J-reference.
    /// </summary>
    /// <param name="jRef">J-reference identifier (e.g. <c>J0171</c>).</param>
    /// <param name="item">The resolved data item, or <c>null</c> when not found.</param>
    /// <returns><c>true</c> when the item was found; <c>false</c> otherwise.</returns>
    bool TryGetDataItem(string jRef, out DataItem? item);
}

