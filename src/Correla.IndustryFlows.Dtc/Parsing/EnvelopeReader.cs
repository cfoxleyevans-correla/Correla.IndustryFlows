namespace Correla.IndustryFlows.Dtc.Parsing;

/// <summary>
/// Reads the ZHV (file header) record from a DTC flat file and returns an
/// <see cref="Envelope"/> without consuming any of the body lines.
/// </summary>
public static class EnvelopeReader
{
    // Minimum number of pipe-delimited fields required on the ZHV line.
    // Indices: 0=ZHV, 1=FileRef, 2=FlowId, 3=FlowVersion, 4=Sender, 5=Recipient
    private const int MinZhvFields = 6;

    /// <summary>
    /// Reads the first non-empty line from <paramref name="stream"/>, validates
    /// that it is a well-formed ZHV record, and returns the extracted envelope.
    /// The stream position is left immediately after the ZHV line so the body
    /// can be read by a subsequent call without buffering conflicts.
    /// </summary>
    /// <param name="stream">Readable DTC file stream. Must support sequential reading.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>The envelope extracted from the ZHV line.</returns>
    /// <exception cref="InvalidDtcFileException">
    /// The stream is empty, the first non-empty line is not a ZHV record, or the
    /// ZHV record has too few fields.
    /// </exception>
    public static async Task<Envelope> ReadAsync(Stream stream, CancellationToken ct = default)
    {
        // Read byte-by-byte so the stream position stays exactly after the ZHV
        // line — no StreamReader look-ahead that would consume body content.
        string? line = await ReadLineAsync(stream, ct);

        // Skip leading blank lines — some generators emit a blank preamble.
        while (line is not null && string.IsNullOrWhiteSpace(line))
        {
            line = await ReadLineAsync(stream, ct);
        }

        if (line is null)
        {
            throw new InvalidDtcFileException("The stream contains no content; expected a ZHV header line.");
        }

        var parts = line.Split('|');

        if (parts[0] != "ZHV")
        {
            throw new InvalidDtcFileException(
                $"Expected the first record to be 'ZHV' but found '{parts[0]}'.");
        }

        if (parts.Length < MinZhvFields)
        {
            throw new InvalidDtcFileException(
                $"ZHV line has {parts.Length} field(s) but at least {MinZhvFields} are required.");
        }

        return new Envelope(
            FlowId: parts[2],
            FlowVersion: parts[3],
            Sender: parts[4],
            Recipient: parts[5]);
    }

    /// <summary>
    /// Reads bytes from the stream one at a time until a newline or end-of-stream,
    /// returning the decoded line without the line-ending characters.
    /// Returns <c>null</c> at end-of-stream with no bytes read.
    /// </summary>
    private static async Task<string?> ReadLineAsync(Stream stream, CancellationToken ct)
    {
        var buffer = new byte[1];
        var chars = new System.Text.StringBuilder();
        bool any = false;

        while (true)
        {
            int read = await stream.ReadAsync(buffer, ct);
            if (read == 0)
            {
                return any ? chars.ToString() : null;
            }

            any = true;
            char c = (char)buffer[0];

            if (c == '\n')
            {
                break;
            }

            // Carriage return is consumed silently (CRLF files).
            if (c != '\r')
            {
                chars.Append(c);
            }
        }

        return chars.ToString();
    }
}
