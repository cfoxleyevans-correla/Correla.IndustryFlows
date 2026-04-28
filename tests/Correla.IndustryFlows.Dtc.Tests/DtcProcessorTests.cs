using Correla.IndustryFlows.Dtc.Schema;
using Correla.IndustryFlows.Dtc.Validation;
using Correla.IndustryFlows.Dtc.Validation.Predicates;

namespace Correla.IndustryFlows.Dtc.Tests;

/// <summary>
/// Integration tests for <see cref="DtcProcessor"/>.
/// Uses the real schema bundle on disk and exercises the full three-phase pipeline.
/// </summary>
public sealed class DtcProcessorTests
{
    private static string BundlePath()
    {
        var dir = AppContext.BaseDirectory;
        var current = new DirectoryInfo(dir);
        while (current is not null && !current.GetFiles("*.sln").Any())
        {
            current = current.Parent;
        }

        return Path.Combine(current!.FullName, "docs", "elec", "15.4", "schemas");
    }

    private static DtcProcessor BuildProcessor()
    {
        var registry = new FileSchemaRegistry(BundlePath());
        var predicates = new List<IPredicate>
        {
            new MpanCheckDigitPredicate(),
            new AmsidCheckDigitPredicate(),
            new DtcDateTimePredicate(),
            new DtcMidnightHhPredicate(),
            new UniqueWithinGroupPredicate(),
        };
        var engine = new RuleEngine(new PredicateRegistry(predicates));

        return new DtcProcessor(registry, engine);
    }

    private static Stream ToStream(string text) =>
        new MemoryStream(System.Text.Encoding.UTF8.GetBytes(text));

    [Fact]
    public async Task ProcessAsync_ValidD0010_SuccessResult()
    {
        var processor = BuildProcessor();
        var file =
            "ZHV|FILE-001|D0010|002|NHHDA|UDMS|\r\n" +
            "026|2000000000015|V|\r\n" +   // valid MPAN check digit
            "028|S95A123456|R|\r\n" +
            "030|01|20260415000000|045231||||T|R|\r\n" +
            "032|NC|T|\r\n" +
            "ZPT|5|\r\n";

        var result = await processor.ProcessAsync(ToStream(file));

        Assert.True(result.Success);
        Assert.NotNull(result.Envelope);
        Assert.Equal("D0010", result.Envelope.FlowId);
        Assert.Equal("002", result.Envelope.FlowVersion);
        Assert.NotNull(result.Parsed);
    }

    [Fact]
    public async Task ProcessAsync_UnknownFlowId_FailureResult()
    {
        var processor = BuildProcessor();
        var file = "ZHV|FILE-001|D9999|001|NHHDA|UDMS|\r\n";

        var result = await processor.ProcessAsync(ToStream(file));

        Assert.False(result.Success);
        Assert.NotNull(result.FailureReason);
    }

    [Fact]
    public async Task ProcessAsync_EmptyStream_FailureResult()
    {
        var processor = BuildProcessor();

        var result = await processor.ProcessAsync(ToStream(string.Empty));

        Assert.False(result.Success);
        Assert.NotNull(result.FailureReason);
    }

    [Fact]
    public async Task ProcessAsync_StructuralViolation_SuccessWithFindings()
    {
        var processor = BuildProcessor();
        // 026 is present but has no 028 child — should emit a missing-required-child finding eventually.
        // The file is parseable (Success=true) with findings.
        var file =
            "ZHV|FILE-001|D0010|002|NHHDA|UDMS|\r\n" +
            "026|2000000000015|V|\r\n" +
            "ZPT|2|\r\n";

        var result = await processor.ProcessAsync(ToStream(file));

        Assert.True(result.Success); // file is processable
        Assert.NotNull(result.Findings);
    }

    [Fact]
    public async Task ProcessAsync_ConcurrentCalls_AllSucceed()
    {
        var processor = BuildProcessor();
        var file =
            "ZHV|FILE-001|D0010|002|NHHDA|UDMS|\r\n" +
            "026|2000000000015|V|\r\n" +
            "028|S95A123456|R|\r\n" +
            "030|01|20260415000000|045231||||T|R|\r\n" +
            "032|NC|T|\r\n" +
            "ZPT|5|\r\n";

        var tasks = Enumerable.Range(0, 20)
            .Select(_ => processor.ProcessAsync(ToStream(file)));

        var results = await Task.WhenAll(tasks);

        Assert.All(results, r => Assert.True(r.Success));
    }

    [Fact]
    public async Task ProcessAsync_CancellationToken_Cancels()
    {
        var processor = BuildProcessor();
        var file = "ZHV|FILE-001|D0010|002|NHHDA|UDMS|\r\n026|2000000000015|V|\r\n";
        using var cts = new CancellationTokenSource();
        cts.Cancel();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() =>
            processor.ProcessAsync(ToStream(file), ct: cts.Token));
    }
}

