namespace Correla.IndustryFlows.Dtc;

/// <summary>
/// Three-level classification for a <see cref="Finding"/>.
/// The host application decides which severity levels are blocking.
/// </summary>
public enum Severity
{
    /// <summary>Informational — no action required.</summary>
    Info = 0,

    /// <summary>Non-fatal issue — the file was parsed but may require attention.</summary>
    Warning = 1,

    /// <summary>Fatal issue — the file cannot be accepted as-is.</summary>
    Error = 2,
}

