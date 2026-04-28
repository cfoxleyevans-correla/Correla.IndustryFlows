namespace Correla.IndustryFlows.Dtc.Validation;

/// <summary>
/// In-memory registry of <see cref="IPredicate"/> implementations, keyed by name.
/// Populated at startup from all registered <see cref="IPredicate"/> services.
/// </summary>
public sealed class PredicateRegistry : IPredicateRegistry
{
    private readonly IReadOnlyDictionary<string, IPredicate> _predicates;

    /// <summary>
    /// Initialises the registry from the supplied predicate implementations.
    /// Duplicate names are silently overwritten by the last registered implementation.
    /// </summary>
    public PredicateRegistry(IEnumerable<IPredicate> predicates)
    {
        _predicates = predicates.ToDictionary(p => p.Name, StringComparer.Ordinal);
    }

    /// <inheritdoc/>
    public bool TryGet(string name, out IPredicate? predicate) =>
        _predicates.TryGetValue(name, out predicate);
}

