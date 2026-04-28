namespace Correla.IndustryFlows.Dtc.Schema;

/// <summary>
/// A declarative validation rule loaded from a flow's rule pack JSON.
/// Rules are evaluated by the <c>RuleEngine</c> in Phase 3 of processing.
/// Full vocabulary is defined in §6 of the implementation specification.
/// </summary>
public sealed class Rule
{
    /// <summary>Rule identifier (e.g. <c>D0010-001</c>).</summary>
    public required string Id { get; init; }

    /// <summary>Severity of the finding emitted when this rule fails.</summary>
    public required string Severity { get; init; }

    /// <summary>Human-readable description of what the rule checks.</summary>
    public required string Message { get; init; }

    /// <summary>Group code at which the rule fires — once per instance of that group.</summary>
    public required string Scope { get; init; }

    /// <summary>
    /// Optional precondition. When <c>null</c> the rule always fires.
    /// Expressed as a JSON element; deserialized by the rule engine.
    /// </summary>
    public System.Text.Json.JsonElement? When { get; init; }

    /// <summary>
    /// Assertion to evaluate. Expressed as a JSON element;
    /// deserialized by the rule engine.
    /// </summary>
    public required System.Text.Json.JsonElement Expect { get; init; }
}

