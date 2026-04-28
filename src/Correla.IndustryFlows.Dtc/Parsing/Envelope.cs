namespace Correla.IndustryFlows.Dtc.Parsing;

/// <summary>
/// Metadata extracted from the ZHV (file header) line at the start of every DTC file.
/// Reading the envelope first allows the runtime to load the correct flow schema
/// before parsing the body.
/// </summary>
/// <param name="FlowId">DTC flow identifier (e.g. <c>D0010</c>).</param>
/// <param name="FlowVersion">Catalogue version of the flow (e.g. <c>002</c>).</param>
/// <param name="Sender">Industry role code of the sending participant.</param>
/// <param name="Recipient">Industry role code of the receiving participant.</param>
public sealed record Envelope(
    string FlowId,
    string FlowVersion,
    string Sender,
    string Recipient);

