namespace Correla.IndustryFlows.Dtc.Validation;

/// <summary>
/// Resolves named <see cref="IPredicate"/> implementations by name.
/// Implementations are provided via dependency injection.
/// </summary>
public interface IPredicateRegistry
{
    /// <summary>
    /// Attempts to find a predicate by its registered name.
    /// </summary>
    /// <param name="name">The predicate name as it appears in the rule pack (e.g. <c>mpanCheckDigit</c>).</param>
    /// <param name="predicate">The resolved predicate, or <c>null</c> when not found.</param>
    /// <returns><c>true</c> when the predicate was found.</returns>
    bool TryGet(string name, out IPredicate? predicate);
}

