namespace Correla.IndustryFlows.Dtc.Tests;

/// <summary>
/// Unit tests for <see cref="EnvelopeReader"/>.
/// Covers happy-path and all failure modes before the production type exists.
/// </summary>
public sealed class EnvelopeReaderTests
{
    // Helper: wrap a string as a UTF-8 stream the reader can consume.
    private static Stream ToStream(string text) =>
        new MemoryStream(System.Text.Encoding.UTF8.GetBytes(text));

    [Fact]
    public async Task ReadAsync_ValidZhv_ReturnsCorrectEnvelope()
    {
        // ZHV|<file-ref>|<flowId>|<flowVersion>|<sender>|<recipient>|<extra>
        var stream = ToStream("ZHV|FILE-12345|D0010|002|NHHDA|UDMS|20260415\r\n026|1234567890121|V|\r\n");

        var envelope = await EnvelopeReader.ReadAsync(stream);

        Assert.Equal("D0010", envelope.FlowId);
        Assert.Equal("002", envelope.FlowVersion);
        Assert.Equal("NHHDA", envelope.Sender);
        Assert.Equal("UDMS", envelope.Recipient);
    }

    [Fact]
    public async Task ReadAsync_LeadingBlankLines_SkipsAndFindsZhv()
    {
        // Files sometimes have a leading blank line before ZHV.
        var stream = ToStream("\r\nZHV|FILE-001|D0086|001|NHHDC|SUPPLIER|\r\n");

        var envelope = await EnvelopeReader.ReadAsync(stream);

        Assert.Equal("D0086", envelope.FlowId);
    }

    [Fact]
    public async Task ReadAsync_EmptyStream_ThrowsInvalidDtcFileException()
    {
        var stream = ToStream("");

        await Assert.ThrowsAsync<InvalidDtcFileException>(() =>
            EnvelopeReader.ReadAsync(stream));
    }

    [Fact]
    public async Task ReadAsync_FirstLineNotZhv_ThrowsInvalidDtcFileException()
    {
        // File starts with a data line rather than a ZHV header.
        var stream = ToStream("026|1234567890121|V|\r\n");

        await Assert.ThrowsAsync<InvalidDtcFileException>(() =>
            EnvelopeReader.ReadAsync(stream));
    }

    [Fact]
    public async Task ReadAsync_ZhvWithTooFewFields_ThrowsInvalidDtcFileException()
    {
        // ZHV requires at least 6 pipe-delimited fields (indices 0–5).
        var stream = ToStream("ZHV|FILE-001|D0010|002|\r\n");

        await Assert.ThrowsAsync<InvalidDtcFileException>(() =>
            EnvelopeReader.ReadAsync(stream));
    }

    [Fact]
    public async Task ReadAsync_StreamPositionAfterCall_IsAfterFirstLine()
    {
        // The reader must not consume the body — remaining content must still be readable.
        var content = "ZHV|FILE-001|D0010|002|NHHDA|UDMS|\r\n026|1234567890121|V|\r\n";
        var stream = ToStream(content);

        await EnvelopeReader.ReadAsync(stream);

        // Reader should be positioned after the first line; body lines remain.
        using var remaining = new StreamReader(stream);
        var nextLine = await remaining.ReadLineAsync();
        Assert.Equal("026|1234567890121|V|", nextLine);
    }

    [Fact]
    public async Task ReadAsync_CancellationRequested_ThrowsOperationCancelledException()
    {
        var stream = ToStream("ZHV|FILE-001|D0010|002|NHHDA|UDMS|\r\n");
        using var cts = new CancellationTokenSource();
        cts.Cancel();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() =>
            EnvelopeReader.ReadAsync(stream, cts.Token));
    }
}

