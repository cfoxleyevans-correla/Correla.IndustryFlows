namespace Correla.IndustryFlows.Dtc.Parsing;

/// <summary>
/// Thrown when a DTC file cannot be processed because its structure is fundamentally
/// invalid — for example, a missing or malformed ZHV header line.
/// </summary>
public sealed class InvalidDtcFileException : Exception
{
    /// <summary>Initialises the exception with a descriptive message.</summary>
    public InvalidDtcFileException(string message) : base(message) { }
}

