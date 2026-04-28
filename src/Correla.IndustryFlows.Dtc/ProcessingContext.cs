namespace Correla.IndustryFlows.Dtc;

/// <summary>
/// Caller-supplied context that influences processing behaviour.
/// All properties are optional; omitting them leaves the runtime operating on the
/// values declared in the file itself.
/// </summary>
/// <param name="SenderRoleOverride">
/// Overrides the sender role from the envelope (useful in test scenarios where
/// the file's sender field does not reflect the actual submitting role).
/// </param>
/// <param name="FileReceivedAt">
/// Timestamp at which the file was received. Used by time-sensitive rules.
/// Defaults to <c>DateTimeOffset.UtcNow</c> when not supplied.
/// </param>
/// <param name="Extra">
/// Arbitrary key-value pairs passed through to rule-pack predicates that need
/// host-specific context not available in the file itself.
/// </param>
public sealed record ProcessingContext(
    string? SenderRoleOverride = null,
    DateTimeOffset? FileReceivedAt = null,
    IReadOnlyDictionary<string, string>? Extra = null);

