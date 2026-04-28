using Correla.IndustryFlows.Dtc.DependencyInjection;
using Microsoft.Extensions.DependencyInjection;

namespace Correla.IndustryFlows.Dtc.Tests;

/// <summary>
/// Round-trip fixture tests for D0010. Each fixture file exercises a known
/// scenario; tests assert only <c>(RuleId, Path)</c> tuples — never message strings.
/// </summary>
public sealed class D0010FixtureTests
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

    private static string FixturePath(string name) =>
        Path.Combine(AppContext.BaseDirectory, "Fixtures", "D0010", name);

    private static DtcProcessor BuildProcessor()
    {
        var services = new ServiceCollection();
        services.AddDtcParser(opts =>
        {
            opts.BundlePath = BundlePath();
            opts.RegisterDefaultPredicates = true;
        });

        return services.BuildServiceProvider().GetRequiredService<DtcProcessor>();
    }

    // ---- happy.txt — valid MPAN, no suspect flag, valid reading type ----

    [Fact]
    public async Task D0010_Happy_Success()
    {
        var processor = BuildProcessor();

        await using var stream = File.OpenRead(FixturePath("happy.txt"));
        var result = await processor.ProcessAsync(stream);

        Assert.True(result.Success, result.FailureReason);
        Assert.Equal("D0010", result.Envelope!.FlowId);
    }

    [Fact]
    public async Task D0010_Happy_NoErrorFindings()
    {
        var processor = BuildProcessor();

        await using var stream = File.OpenRead(FixturePath("happy.txt"));
        var result = await processor.ProcessAsync(stream);

        var errors = result.Findings.Where(f => f.Severity == Severity.Error).ToList();
        Assert.Empty(errors);
    }

    // ---- structural.txt — BSC Validation Status 'X' is not in [F, U, V]; missing 032 ----

    [Fact]
    public async Task D0010_Structural_ContainsSchemaEnumFinding()
    {
        var processor = BuildProcessor();

        await using var stream = File.OpenRead(FixturePath("structural.txt"));
        var result = await processor.ProcessAsync(stream);

        // J0022 value 'X' not in enum → SCHEMA-TYPE (coercer emits error for out-of-enum CHAR)
        Assert.Contains(result.Findings, f => f.RuleId == "SCHEMA-TYPE");
    }

    [Fact]
    public async Task D0010_Structural_MissingMandatoryChild_EmitsCardMin()
    {
        // structural.txt has 030 but no 032; 032 has min=1
        var processor = BuildProcessor();

        await using var stream = File.OpenRead(FixturePath("structural.txt"));
        var result = await processor.ProcessAsync(stream);

        Assert.Contains(result.Findings, f => f.RuleId == "SCHEMA-CARD-MIN");
    }

    // ---- rules.txt — invalid MPAN check digit (D0010-007) + suspect flag without reason (D0010-001) ----

    [Fact]
    public async Task D0010_Rules_InvalidMpan_EmitsD0010_007()
    {
        var processor = BuildProcessor();

        await using var stream = File.OpenRead(FixturePath("rules.txt"));
        var result = await processor.ProcessAsync(stream);

        Assert.Contains(result.Findings, f => f.RuleId == "D0010-007");
    }

    [Fact]
    public async Task D0010_Rules_Success_FileIsProcessable()
    {
        // Even a rule-failing file must return Success=true (it's parseable).
        var processor = BuildProcessor();

        await using var stream = File.OpenRead(FixturePath("rules.txt"));
        var result = await processor.ProcessAsync(stream);

        Assert.True(result.Success);
    }
}

