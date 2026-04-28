using Correla.IndustryFlows.Dtc.Parsing;

namespace Correla.IndustryFlows.Dtc;

/// <summary>
/// The outcome of a <c>DtcProcessor.ProcessAsync</c> call.
/// <see cref="Success"/> is <c>false</c> only when envelope or schema lookup fails fatally.
/// All structural and rule-pack issues are recorded in <see cref="Findings"/> with
/// <see cref="Success"/> remaining <c>true</c>.
/// </summary>
/// <param name="Success">Whether the file was processed without a fatal failure.</param>
/// <param name="Envelope">Envelope read from the ZHV line, or <c>null</c> if envelope detection failed.</param>
/// <param name="Parsed">The parsed group-instance tree, or <c>null</c> if parsing did not reach completion.</param>
/// <param name="Findings">All issues detected during parse and validation.</param>
/// <param name="FailureReason">Human-readable reason for a fatal failure; <c>null</c> when <see cref="Success"/> is <c>true</c>.</param>
public sealed record ProcessingResult(
    bool Success,
    Envelope? Envelope,
    DtcFile? Parsed,
    IReadOnlyList<Finding> Findings,
    string? FailureReason);

