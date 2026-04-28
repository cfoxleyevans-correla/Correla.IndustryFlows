using Correla.IndustryFlows.Dtc.Parsing;
using Correla.IndustryFlows.Dtc.Schema;
using Correla.IndustryFlows.Dtc.Validation;

namespace Correla.IndustryFlows.Dtc;

/// <summary>
/// The primary entry point for DTC file processing.
/// Orchestrates envelope detection, schema-driven parsing, structural validation,
/// and rule-pack evaluation in three sequential phases.
/// Thread-safe: register as a singleton.
/// </summary>
public sealed class DtcProcessor
{
    private readonly ISchemaRegistry _registry;
    private readonly RuleEngine _ruleEngine;

    /// <summary>
    /// Initialises the processor with the required collaborators.
    /// All collaborators are expected to be singletons.
    /// </summary>
    public DtcProcessor(ISchemaRegistry registry, RuleEngine ruleEngine)
    {
        _registry = registry;
        _ruleEngine = ruleEngine;
    }

    /// <summary>
    /// Processes a DTC file stream end-to-end.
    /// Returns a <see cref="ProcessingResult"/> for any well-formed file whose flow
    /// is known to the registry. Never throws for file-level problems — those are
    /// captured as findings or in <see cref="ProcessingResult.FailureReason"/>.
    /// </summary>
    /// <param name="input">Readable DTC file stream from the ZHV line onwards.</param>
    /// <param name="context">Optional caller-supplied context (sender role override, timestamps, extras).</param>
    /// <param name="ct">Cancellation token.</param>
    public async Task<ProcessingResult> ProcessAsync(
        Stream input,
        ProcessingContext? context = null,
        CancellationToken ct = default)
    {
        // ---- Phase 1: Envelope detection ----
        Envelope? envelope;

        try
        {
            envelope = await EnvelopeReader.ReadAsync(input, ct);
        }
        catch (InvalidDtcFileException ex)
        {
            return Failure(null, ex.Message);
        }

        // ---- Registry lookup ----
        if (!_registry.TryGet(envelope.FlowId, envelope.FlowVersion, out var schema) || schema is null)
        {
            return Failure(envelope,
                $"No schema found for flow '{envelope.FlowId}' version '{envelope.FlowVersion}'.");
        }

        // Build the data-item lookup needed by the parser.
        // The registry is the single source of truth for data-item catalogue data.
        var catalogue = BuildCatalogueLookup(schema);

        // ---- Phase 2: Flat-file parsing ----
        var (file, parseFindings) = await FlatFileParser.ParseAsync(input, schema, catalogue, ct);

        // Store the resolved sender role in the root for predicate/rule evaluation.
        var effectiveSenderRole = context?.SenderRoleOverride ?? envelope.Sender;
        file.Root.Fields["__senderRole"] = effectiveSenderRole;

        // ---- Phase 3a: Structural validation ----
        var structuralFindings = SchemaValidator.Validate(file, schema);

        // ---- Phase 3b: Rule-pack evaluation ----
        var ruleFindings = _ruleEngine.Evaluate(file, schema, context);

        var allFindings = parseFindings
            .Concat(structuralFindings)
            .Concat(ruleFindings)
            .ToList()
            .AsReadOnly();

        return new ProcessingResult(
            Success: true,
            Envelope: envelope,
            Parsed: file,
            Findings: allFindings,
            FailureReason: null);
    }

    // ---- Helpers ----

    /// <summary>
    /// Builds an in-memory catalogue lookup by gathering data items referenced by the schema.
    /// Falls back to an empty DataItem for any reference not found in the registry.
    /// </summary>
    private IReadOnlyDictionary<string, DataItem> BuildCatalogueLookup(FlowSchema schema)
    {
        var refs = schema.Groups.Values
            .SelectMany(g => g.Fields)
            .Select(f => f.Ref)
            .Distinct(StringComparer.Ordinal);

        var dict = new Dictionary<string, DataItem>(StringComparer.Ordinal);

        foreach (var jRef in refs)
        {
            if (_registry.TryGetDataItem(jRef, out var item) && item is not null)
            {
                dict[jRef] = item;
            }
        }

        return dict;
    }

    /// <summary>Builds a fatal-failure result with an empty findings list.</summary>
    private static ProcessingResult Failure(Envelope? envelope, string reason) =>
        new(Success: false, Envelope: envelope, Parsed: null,
            Findings: Array.Empty<Finding>(), FailureReason: reason);
}

