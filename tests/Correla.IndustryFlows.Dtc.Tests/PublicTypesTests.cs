namespace Correla.IndustryFlows.Dtc.Tests;

/// <summary>
/// Verifies that all public result types compile, construct, and expose
/// their properties correctly. These are the primary contract types between
/// the DTC runtime and its callers.
/// </summary>
public sealed class PublicTypesTests
{
    [Fact]
    public void Finding_ConstructsAndExposesAllProperties()
    {
        // Arrange / Act
        var finding = new Finding("PARSE-001", Severity.Error, "root[0]", "Unknown group code XYZ", LineNumber: 3);

        // Assert
        Assert.Equal("PARSE-001", finding.RuleId);
        Assert.Equal(Severity.Error, finding.Severity);
        Assert.Equal("root[0]", finding.Path);
        Assert.Equal("Unknown group code XYZ", finding.Message);
        Assert.Equal(3, finding.LineNumber);
    }

    [Fact]
    public void Finding_LineNumberIsOptional()
    {
        var finding = new Finding("SCHEMA-REQ", Severity.Warning, "fields[0]", "Required field absent");

        Assert.Null(finding.LineNumber);
    }

    [Fact]
    public void Severity_HasThreeLevels()
    {
        // Verify the expected enum values exist
        Assert.Equal(0, (int)Severity.Info);
        Assert.Equal(1, (int)Severity.Warning);
        Assert.Equal(2, (int)Severity.Error);
    }

    [Fact]
    public void Envelope_ConstructsAndExposesAllProperties()
    {
        var envelope = new Envelope("D0010", "002", "NHHDA", "UDMS");

        Assert.Equal("D0010", envelope.FlowId);
        Assert.Equal("002", envelope.FlowVersion);
        Assert.Equal("NHHDA", envelope.Sender);
        Assert.Equal("UDMS", envelope.Recipient);
    }

    [Fact]
    public void ProcessingResult_SuccessVariant_ExposesExpectedValues()
    {
        var envelope = new Envelope("D0010", "002", "NHHDA", "UDMS");
        var findings = new List<Finding>();

        var result = new ProcessingResult(
            Success: true,
            Envelope: envelope,
            Parsed: null,
            Findings: findings,
            FailureReason: null);

        Assert.True(result.Success);
        Assert.Equal(envelope, result.Envelope);
        Assert.Null(result.Parsed);
        Assert.Empty(result.Findings);
        Assert.Null(result.FailureReason);
    }

    [Fact]
    public void ProcessingResult_FailureVariant_ExposesReason()
    {
        var result = new ProcessingResult(
            Success: false,
            Envelope: null,
            Parsed: null,
            Findings: Array.Empty<Finding>(),
            FailureReason: "Missing ZHV line");

        Assert.False(result.Success);
        Assert.Null(result.Envelope);
        Assert.Equal("Missing ZHV line", result.FailureReason);
    }

    [Fact]
    public void ProcessingContext_AllPropertiesAreOptional()
    {
        // Default construction must work with no arguments
        var ctx = new ProcessingContext();

        Assert.Null(ctx.SenderRoleOverride);
        Assert.Null(ctx.FileReceivedAt);
        Assert.Null(ctx.Extra);
    }

    [Fact]
    public void ProcessingContext_AcceptsAllProperties()
    {
        var extra = new Dictionary<string, string> { ["key"] = "value" };
        var at = DateTimeOffset.UtcNow;
        var ctx = new ProcessingContext(SenderRoleOverride: "HHDC", FileReceivedAt: at, Extra: extra);

        Assert.Equal("HHDC", ctx.SenderRoleOverride);
        Assert.Equal(at, ctx.FileReceivedAt);
        Assert.Equal("value", ctx.Extra!["key"]);
    }
}

