namespace Correla.IndustryFlows.Dtc;

/// <summary>
/// A single validation or parse problem detected while processing a DTC file.
/// Findings are categorised by <see cref="RuleId"/> prefix:
/// <list type="bullet">
///   <item><term>PARSE-</term><description>Structural flat-file problems (unknown group code, bad parent).</description></item>
///   <item><term>SCHEMA-</term><description>Schema-driven validation failures (required field, type, cardinality).</description></item>
///   <item><term>RULE-</term><description>Generic rule-pack failures.</description></item>
///   <item><term>D{NNNN}-</term><description>Flow-specific rule failures defined in the rule pack.</description></item>
/// </list>
/// </summary>
/// <param name="RuleId">Identifies the rule that fired (e.g. <c>PARSE-001</c>, <c>D0010-007</c>).</param>
/// <param name="Severity">Classification of this finding.</param>
/// <param name="Path">Location in the parsed tree using snake_case group names and zero-based indices (e.g. <c>mpan_groups[0].meters[1]</c>).</param>
/// <param name="Message">Human-readable description of the problem.</param>
/// <param name="LineNumber">One-based line number in the source file, when available.</param>
public sealed record Finding(
    string RuleId,
    Severity Severity,
    string Path,
    string Message,
    int? LineNumber = null);

