namespace Correla.IndustryFlows.Dtc.DependencyInjection;

/// <summary>
/// Configuration options for the DTC parser runtime.
/// Supplied to <see cref="ServiceCollectionExtensions.AddDtcParser"/>.
/// </summary>
public sealed class DtcParserOptions
{
    /// <summary>
    /// Absolute or relative path to the directory containing <c>manifest.json</c>.
    /// Relative paths are resolved against <see cref="AppContext.BaseDirectory"/>.
    /// </summary>
    public string BundlePath { get; set; } = string.Empty;

    /// <summary>
    /// When <c>true</c> (the default), individual flow schema files are loaded
    /// on first access rather than at startup. Set to <c>false</c> to pre-load
    /// all schemas at registration time.
    /// </summary>
    public bool LazyLoadFlows { get; set; } = true;

    /// <summary>
    /// When <c>true</c> (the default), the five built-in predicates
    /// (mpanCheckDigit, amsidCheckDigit, dtcDateTime, dtcMidnightHh, uniqueWithinGroup)
    /// are registered automatically.
    /// </summary>
    public bool RegisterDefaultPredicates { get; set; } = true;
}

