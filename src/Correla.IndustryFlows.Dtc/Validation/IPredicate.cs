namespace Correla.IndustryFlows.Dtc.Validation;

/// <summary>
/// A named boolean predicate that evaluates a raw field value in context.
/// Implement this interface and register in <see cref="IPredicateRegistry"/>
/// to add custom built-in predicates used by the rule pack's <c>satisfies</c> operator.
/// </summary>
public interface IPredicate
{
    /// <summary>Unique name used in rule pack JSON (e.g. <c>mpanCheckDigit</c>).</summary>
    string Name { get; }

    /// <summary>
    /// Evaluates the predicate against <paramref name="value"/>.
    /// </summary>
    /// <param name="value">Raw field value as it was read from the file (before coercion).</param>
    /// <param name="instance">The group instance that owns the field being evaluated.</param>
    /// <returns><c>true</c> when the value satisfies the predicate; <c>false</c> otherwise.</returns>
    bool Evaluate(string value, GroupInstance instance);
}

