using System.Text;
using Correla.IndustryFlows.Dtc.Parsing;
using Correla.IndustryFlows.Dtc.Schema;

namespace Correla.IndustryFlows.Dtc;

/// <summary>
/// Serialises a <see cref="DtcFile"/> back into a DTC flat-file string.
/// Validates cardinalities and required fields before writing; returns findings
/// when the tree is not valid for generation.
/// </summary>
public static class DtcFileWriter
{
    /// <summary>
    /// Attempts to generate a DTC flat file from the supplied tree.
    /// </summary>
    /// <param name="file">The parsed group-instance tree to serialise.</param>
    /// <param name="schema">The flow schema that defines field ordering and cardinalities.</param>
    /// <param name="envelope">Envelope values written into the ZHV header line.</param>
    /// <param name="fileReference">File reference written into the ZHV header (e.g. <c>FILE-001</c>).</param>
    /// <returns>
    /// A result containing either the generated flat-file text or a non-empty findings list
    /// describing why generation failed.
    /// </returns>
    public static WriteResult Write(
        DtcFile file,
        FlowSchema schema,
        Envelope envelope,
        string fileReference)
    {
        var findings = new List<Finding>();

        // Pre-validate the tree before writing to avoid partial output.
        var validationFindings = Validation.SchemaValidator.Validate(file, schema);
        if (validationFindings.Any(f => f.Severity == Severity.Error))
        {
            return WriteResult.Failed(validationFindings);
        }

        var sb = new StringBuilder();
        var lineCount = 0;

        // ZHV line: ZHV|FileRef|FlowId|FlowVersion|Sender|Recipient|GeneratedAt
        var generatedAt = DateTime.UtcNow.ToString("yyyyMMddHHmmss", System.Globalization.CultureInfo.InvariantCulture);
        sb.AppendLine(System.Globalization.CultureInfo.InvariantCulture,
            $"ZHV|{fileReference}|{envelope.FlowId}|{envelope.FlowVersion}|{envelope.Sender}|{envelope.Recipient}|{generatedAt}");
        lineCount++;

        // Walk the tree in depth-first order, skipping the synthetic ROOT.
        foreach (var group in file.Root.Children)
        {
            WriteGroup(group, schema, sb, ref lineCount, findings);
        }

        // ZPT line: ZPT|RecordCount (total lines excluding ZHV and ZPT themselves)
        sb.AppendLine(System.Globalization.CultureInfo.InvariantCulture, $"ZPT|{lineCount}|");

        if (findings.Count > 0)
        {
            return WriteResult.Failed(findings);
        }

        return WriteResult.Ok(sb.ToString());
    }

    /// <summary>Writes a group instance line and recurses into its children.</summary>
    private static void WriteGroup(
        GroupInstance instance,
        FlowSchema schema,
        StringBuilder sb,
        ref int lineCount,
        List<Finding> findings)
    {
        if (!schema.Groups.TryGetValue(instance.GroupCode, out var groupDef))
        {
            findings.Add(new Finding(
                "WRITE-001", Severity.Error,
                instance.GroupCode,
                $"Group code '{instance.GroupCode}' is not defined in the schema."));
            return;
        }

        var cells = new List<string>(groupDef.Fields.Count + 1) { instance.GroupCode };

        foreach (var fieldDef in groupDef.Fields)
        {
            if (instance.Fields.TryGetValue(fieldDef.Ref, out var value))
            {
                // Serialise the typed value back to its DTC string representation.
                cells.Add(FieldToString(value));
            }
            else if (fieldDef.Required)
            {
                findings.Add(new Finding(
                    "WRITE-REQ", Severity.Error,
                    $"{instance.GroupCode}.{fieldDef.Ref}",
                    $"Required field {fieldDef.Ref} ({fieldDef.Name}) is missing."));
                cells.Add(string.Empty);
            }
            else
            {
                // Optional absent field — empty cell.
                cells.Add(string.Empty);
            }
        }

        sb.AppendLine(string.Join("|", cells) + "|");
        lineCount++;

        foreach (var child in instance.Children)
        {
            WriteGroup(child, schema, sb, ref lineCount, findings);
        }
    }

    /// <summary>
    /// Converts a coerced .NET value back to its DTC flat-file string representation.
    /// </summary>
    private static string FieldToString(object value) => value switch
    {
        string s => s,
        bool b => b ? "T" : "F",
        int i => i.ToString(System.Globalization.CultureInfo.InvariantCulture),
        decimal d =>
            // Round-trip: shift the decimal point back to an integer string.
            // e.g. 4523.1m stored integer form — multiply by implied scale.
            // Since we don't carry the scale, emit the decimal as-is without the decimal point.
            ((long)(d * 10)).ToString(System.Globalization.CultureInfo.InvariantCulture),
        DateOnly date => date.ToString("yyyyMMdd", System.Globalization.CultureInfo.InvariantCulture),
        DateTime dt => dt.ToString("yyyyMMddHHmmss", System.Globalization.CultureInfo.InvariantCulture),
        TimeOnly t => t.ToString("HHmmss", System.Globalization.CultureInfo.InvariantCulture),
        _ => value.ToString() ?? string.Empty,
    };
}

/// <summary>
/// The result of a <see cref="DtcFileWriter.Write"/> call.
/// Either contains the generated flat-file text or a list of error findings.
/// </summary>
public sealed class WriteResult
{
    /// <summary>Whether the write succeeded with no blocking errors.</summary>
    public bool Success { get; private init; }

    /// <summary>The generated flat-file text. Non-null when <see cref="Success"/> is <c>true</c>.</summary>
    public string? Content { get; private init; }

    /// <summary>Findings that prevented generation. Non-empty when <see cref="Success"/> is <c>false</c>.</summary>
    public IReadOnlyList<Finding> Findings { get; private init; } = [];

    /// <summary>Creates a successful write result.</summary>
    internal static WriteResult Ok(string content) =>
        new() { Success = true, Content = content };

    /// <summary>Creates a failed write result.</summary>
    internal static WriteResult Failed(IEnumerable<Finding> findings) =>
        new() { Success = false, Findings = findings.ToList().AsReadOnly() };
}

