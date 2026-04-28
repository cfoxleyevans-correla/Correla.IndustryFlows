using Correla.IndustryFlows.Dtc.Schema;

namespace Correla.IndustryFlows.Dtc.Parsing;

/// <summary>
/// Parses a DTC flat-file body (after the ZHV line has been consumed by
/// <see cref="EnvelopeReader"/>) into a tree of <see cref="GroupInstance"/> objects.
/// Uses a stack to reconstruct the group hierarchy from the flat file's ordering.
/// </summary>
public static class FlatFileParser
{
    // Group codes that are part of the envelope and are silently skipped.
    private static readonly HashSet<string> EnvelopeCodes =
        new(StringComparer.Ordinal) { "ZHV", "ZHD", "ZPT" };

    /// <summary>
    /// Reads lines from <paramref name="stream"/> and produces a <see cref="DtcFile"/>
    /// mirroring the file's group hierarchy.
    /// </summary>
    /// <param name="stream">Readable stream positioned after the ZHV line.</param>
    /// <param name="schema">Schema for the flow identified in the envelope.</param>
    /// <param name="catalogue">Data item catalogue for field coercion and validation.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>
    /// The parsed tree and a (possibly empty) list of structural findings.
    /// Never returns a null DtcFile — a tree with an empty root is the minimum output.
    /// </returns>
    public static async Task<(DtcFile File, IReadOnlyList<Finding> Findings)> ParseAsync(
        Stream stream,
        FlowSchema schema,
        IReadOnlyDictionary<string, DataItem> catalogue,
        CancellationToken ct = default)
    {
        var findings = new List<Finding>();

        // Synthetic root node that holds top-level groups.
        var root = new GroupInstance { GroupCode = "ROOT", LineNumber = 0, Parent = null };

        // Stack of currently open group instances.
        // stack[0] is always root; stack.Peek() is the deepest open group.
        var stack = new Stack<GroupInstance>();
        stack.Push(root);

        using var reader = new StreamReader(stream, leaveOpen: true);
        int lineNumber = 0;
        string? line;

        while ((line = await reader.ReadLineAsync(ct)) is not null)
        {
            lineNumber++;

            if (string.IsNullOrWhiteSpace(line))
            {
                continue;
            }

            var parts = line.TrimEnd('|').Split('|');
            var code = parts[0];

            // Skip envelope rows — they are handled by EnvelopeReader.
            if (EnvelopeCodes.Contains(code))
            {
                continue;
            }

            if (!schema.Groups.TryGetValue(code, out var groupDef))
            {
                findings.Add(new Finding(
                    "PARSE-001", Severity.Error,
                    $"line[{lineNumber}]",
                    $"Unknown group code '{code}'.",
                    lineNumber));
                continue;
            }

            // Pop the stack until the top matches this group's declared parent.
            // Root-level groups (parent == null) belong directly under the synthetic root.
            while (stack.Count > 1 && !IsParentMatch(stack.Peek(), groupDef.Parent))
            {
                stack.Pop();
            }

            // After popping, the top must be the declared parent.
            if (!IsParentMatch(stack.Peek(), groupDef.Parent))
            {
                findings.Add(new Finding(
                    "PARSE-002", Severity.Error,
                    $"line[{lineNumber}]",
                    $"Group '{code}' expects parent '{groupDef.Parent}' but none is open.",
                    lineNumber));
                continue;
            }

            var instance = new GroupInstance
            {
                GroupCode = code,
                LineNumber = lineNumber,
                Parent = stack.Peek(),
            };

            BindFields(instance, groupDef, parts, catalogue, findings, lineNumber);

            stack.Peek().Children.Add(instance);
            stack.Push(instance);
        }

        return (new DtcFile { Root = root }, findings.AsReadOnly());
    }

    /// <summary>
    /// Binds positional pipe fields onto the group instance using the schema's
    /// field definitions and data-item catalogue for coercion.
    /// </summary>
    private static void BindFields(
        GroupInstance instance,
        GroupDefinition groupDef,
        string[] parts,
        IReadOnlyDictionary<string, DataItem> catalogue,
        List<Finding> findings,
        int lineNumber)
    {
        // parts[0] is the group code; data fields start at index 1.
        for (int i = 0; i < groupDef.Fields.Count; i++)
        {
            var fieldDef = groupDef.Fields[i];
            var raw = i + 1 < parts.Length ? parts[i + 1] : string.Empty;

            if (string.IsNullOrEmpty(raw))
            {
                if (fieldDef.Required)
                {
                    findings.Add(new Finding(
                        "SCHEMA-REQ", Severity.Error,
                        BuildPath(instance, fieldDef.Ref),
                        $"Required field {fieldDef.Ref} ({fieldDef.Name}) is absent.",
                        lineNumber));
                }

                continue;
            }

            if (!catalogue.TryGetValue(fieldDef.Ref, out var dataItem))
            {
                findings.Add(new Finding(
                    "SCHEMA-UNK", Severity.Warning,
                    BuildPath(instance, fieldDef.Ref),
                    $"Data item '{fieldDef.Ref}' not found in catalogue.",
                    lineNumber));
                instance.Fields[fieldDef.Ref] = raw;
                continue;
            }

            var (value, error) = FieldCoercer.Coerce(raw, dataItem);

            if (error is not null)
            {
                findings.Add(new Finding(
                    "SCHEMA-TYPE", Severity.Error,
                    BuildPath(instance, fieldDef.Ref),
                    error,
                    lineNumber));
            }
            else if (value is not null)
            {
                instance.Fields[fieldDef.Ref] = value;
            }
        }
    }

    /// <summary>Builds a dot-notation path string for a finding.</summary>
    private static string BuildPath(GroupInstance instance, string jRef) =>
        $"{instance.GroupCode}[line {instance.LineNumber}].{jRef}";

    /// <summary>
    /// Returns true when the top-of-stack instance satisfies a group's declared parent requirement.
    /// A null parent means the group is root-level and must sit directly under the synthetic ROOT.
    /// </summary>
    private static bool IsParentMatch(GroupInstance top, string? requiredParent) =>
        requiredParent is null
            ? top.GroupCode == "ROOT"
            : top.GroupCode == requiredParent;
}

