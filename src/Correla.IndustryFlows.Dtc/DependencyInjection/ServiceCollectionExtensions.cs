using Correla.IndustryFlows.Dtc.Schema;
using Correla.IndustryFlows.Dtc.Validation;
using Correla.IndustryFlows.Dtc.Validation.Predicates;
using Microsoft.Extensions.DependencyInjection;

namespace Correla.IndustryFlows.Dtc.DependencyInjection;

/// <summary>
/// Extension methods for registering the DTC runtime into an
/// <see cref="IServiceCollection"/>.
/// </summary>
public static class ServiceCollectionExtensions
{
    /// <summary>
    /// Registers <see cref="DtcProcessor"/>, <see cref="ISchemaRegistry"/>,
    /// <see cref="IPredicateRegistry"/>, and optional built-in predicates as singletons.
    /// </summary>
    /// <param name="services">The service collection to configure.</param>
    /// <param name="configure">Configuration callback for <see cref="DtcParserOptions"/>.</param>
    /// <returns>The service collection for chaining.</returns>
    public static IServiceCollection AddDtcParser(
        this IServiceCollection services,
        Action<DtcParserOptions> configure)
    {
        var options = new DtcParserOptions();
        configure(options);

        // Resolve the bundle path — relative paths are relative to the application base.
        var bundlePath = Path.IsPathRooted(options.BundlePath)
            ? options.BundlePath
            : Path.Combine(AppContext.BaseDirectory, options.BundlePath);

        // Schema registry — singleton so the lazy-load cache is shared across requests.
        services.AddSingleton<ISchemaRegistry>(_ => new FileSchemaRegistry(bundlePath));

        if (options.RegisterDefaultPredicates)
        {
            services.AddSingleton<IPredicate, MpanCheckDigitPredicate>();
            services.AddSingleton<IPredicate, AmsidCheckDigitPredicate>();
            services.AddSingleton<IPredicate, DtcDateTimePredicate>();
            services.AddSingleton<IPredicate, DtcMidnightHhPredicate>();
            services.AddSingleton<IPredicate, UniqueWithinGroupPredicate>();
        }

        // Predicate registry aggregates all IPredicate registrations.
        services.AddSingleton<IPredicateRegistry>(sp =>
            new PredicateRegistry(sp.GetServices<IPredicate>()));

        // Rule engine.
        services.AddSingleton<RuleEngine>();

        // Primary façade.
        services.AddSingleton<DtcProcessor>();

        return services;
    }
}

