using Correla.IndustryFlows.Dtc.Schema;

namespace Correla.IndustryFlows.Dtc.Tests;

/// <summary>
/// Unit tests for <see cref="FlatFileParser"/>. Uses an in-memory D0010
/// schema fragment so no I/O is required.
/// </summary>
public sealed class FlatFileParserTests
{
    private static Stream ToStream(string text) =>
        new MemoryStream(System.Text.Encoding.UTF8.GetBytes(text));

    // ---- Minimal D0010-like schema for parser tests ----
    // Groups: 026 (root) → 028 → 030 → 032
    private static FlowSchema BuildTestSchema()
    {
        // data items used in binding
        return new FlowSchema
        {
            FlowId = "D0010",
            FlowVersion = "002",
            FlowName = "Test",
            Status = "Test",
            Ownership = "Test",
            Description = "Test",
            Routes = [],
            Notes = string.Empty,
            Rules = [],
            Groups = new Dictionary<string, GroupDefinition>
            {
                ["026"] = new GroupDefinition
                {
                    Name = "MPAN Cores",
                    Parent = null,
                    Level = 1,
                    Cardinality = new Cardinality(1, null),
                    Condition = string.Empty,
                    Fields =
                    [
                        new FieldDefinition("J0003", "MPAN Core", "NUM(13)", Required: true),
                        new FieldDefinition("J0022", "BSC Validation Status", "CHAR(1)", Required: true),
                    ],
                },
                ["028"] = new GroupDefinition
                {
                    Name = "Meter Reading Types",
                    Parent = "026",
                    Level = 2,
                    Cardinality = new Cardinality(1, null),
                    Condition = string.Empty,
                    Fields =
                    [
                        new FieldDefinition("J0004", "Meter Id", "CHAR(10)", Required: true),
                        new FieldDefinition("J0171", "Reading Type", "CHAR(1)", Required: true),
                    ],
                },
                ["030"] = new GroupDefinition
                {
                    Name = "Register Readings",
                    Parent = "028",
                    Level = 3,
                    Cardinality = new Cardinality(0, null),
                    Condition = string.Empty,
                    Fields =
                    [
                        new FieldDefinition("J0010", "Meter Register Id", "CHAR(2)", Required: true),
                        new FieldDefinition("J0045", "Meter Reading Flag", "BOOLEAN", Required: false),
                    ],
                },
                ["032"] = new GroupDefinition
                {
                    Name = "Validation Result",
                    Parent = "030",
                    Level = 4,
                    Cardinality = new Cardinality(1, 1),
                    Condition = string.Empty,
                    Fields =
                    [
                        new FieldDefinition("J0047", "Status", "BOOLEAN", Required: true),
                    ],
                },
            },
        };
    }

    // Minimal catalogue with just the items needed by the test schema.
    private static Dictionary<string, DataItem> BuildTestCatalogue() => new()
    {
        ["J0003"] = new DataItem { Ref = "J0003", Name = "MPAN Core", Domain = "Num", LogicalFormat = "NUM(13)", PhysicalLength = "13", Notes = "", ValidSet = new ValidSet { Kind = "none" } },
        ["J0022"] = new DataItem { Ref = "J0022", Name = "BSC Validation", Domain = "Code", LogicalFormat = "CHAR(1)", PhysicalLength = "1", Notes = "", ValidSet = new ValidSet { Kind = "enum", EnumValues = [new("F", "Fail"), new("U", "Unvalidated"), new("V", "Valid")] } },
        ["J0004"] = new DataItem { Ref = "J0004", Name = "Meter Id", Domain = "Char", LogicalFormat = "CHAR(10)", PhysicalLength = "10", Notes = "", ValidSet = new ValidSet { Kind = "none" } },
        ["J0171"] = new DataItem { Ref = "J0171", Name = "Reading Type", Domain = "Code", LogicalFormat = "CHAR(1)", PhysicalLength = "1", Notes = "", ValidSet = new ValidSet { Kind = "none" } },
        ["J0010"] = new DataItem { Ref = "J0010", Name = "Register Id", Domain = "Char", LogicalFormat = "CHAR(2)", PhysicalLength = "2", Notes = "", ValidSet = new ValidSet { Kind = "none" } },
        ["J0045"] = new DataItem { Ref = "J0045", Name = "Reading Flag", Domain = "Code", LogicalFormat = "BOOLEAN", PhysicalLength = "1", Notes = "", ValidSet = new ValidSet { Kind = "none" } },
        ["J0047"] = new DataItem { Ref = "J0047", Name = "Status", Domain = "Code", LogicalFormat = "BOOLEAN", PhysicalLength = "1", Notes = "", ValidSet = new ValidSet { Kind = "none" } },
    };

    [Fact]
    public async Task ParseAsync_SingleMpanWithMeterAndTwoRegisters_ProducesCorrectTree()
    {
        // ZHV is already consumed by EnvelopeReader; body only here.
        var body =
            "026|1234567890121|V|\r\n" +
            "028|S95A123456|R|\r\n" +
            "030|01|T|\r\n" +
            "032|T|\r\n" +
            "030|02|F|\r\n" +
            "032|T|\r\n" +
            "ZPT|6|\r\n";

        var schema = BuildTestSchema();
        var catalogue = BuildTestCatalogue();
        var (file, findings) = await FlatFileParser.ParseAsync(ToStream(body), schema, catalogue);

        // Tree shape: ROOT → 026 → 028 → [030 → 032, 030 → 032]
        Assert.NotNull(file);
        Assert.DoesNotContain(findings, f => f.RuleId.StartsWith("PARSE-", StringComparison.Ordinal));

        var mpan = file.Root.Children.Single();
        Assert.Equal("026", mpan.GroupCode);

        var meter = mpan.Children.Single();
        Assert.Equal("028", meter.GroupCode);

        Assert.Equal(2, meter.Children.Count);
        Assert.All(meter.Children, c => Assert.Equal("030", c.GroupCode));

        var reg1 = meter.Children[0];
        Assert.Single(reg1.Children);
        Assert.Equal("032", reg1.Children[0].GroupCode);
    }

    [Fact]
    public async Task ParseAsync_FieldsBoundByPosition()
    {
        var body = "026|1234567890121|V|\r\n028|S95A123456|R|\r\nZPT|3|\r\n";
        var (file, _) = await FlatFileParser.ParseAsync(ToStream(body), BuildTestSchema(), BuildTestCatalogue());

        var mpan = file!.Root.Children[0];
        Assert.Equal("1234567890121", mpan.Fields["J0003"]);
        Assert.Equal("V", mpan.Fields["J0022"]);

        var meter = mpan.Children[0];
        Assert.Equal("S95A123456", meter.Fields["J0004"]);
    }

    [Fact]
    public async Task ParseAsync_EnvelopeRows_SkippedSilently()
    {
        var body =
            "ZHV|FILE-001|D0010|002|NHHDA|UDMS|\r\n" +
            "026|1234567890121|V|\r\n" +
            "028|S95A123456|R|\r\n" +
            "ZPT|3|\r\n";

        var (file, findings) = await FlatFileParser.ParseAsync(ToStream(body), BuildTestSchema(), BuildTestCatalogue());

        Assert.NotNull(file);
        Assert.DoesNotContain(findings, f => f.RuleId == "PARSE-001");
    }

    [Fact]
    public async Task ParseAsync_UnknownGroupCode_EmitsParse001()
    {
        var body = "026|1234567890121|V|\r\nXXX|garbage|\r\n028|S95A123456|R|\r\n";

        var (_, findings) = await FlatFileParser.ParseAsync(ToStream(body), BuildTestSchema(), BuildTestCatalogue());

        Assert.Contains(findings, f => f.RuleId == "PARSE-001");
    }

    [Fact]
    public async Task ParseAsync_MismatchedParent_EmitsParse002()
    {
        // 032 expects parent 030, but here there is no open 030.
        var body = "026|1234567890121|V|\r\n028|S95A123456|R|\r\n032|T|\r\n";

        var (_, findings) = await FlatFileParser.ParseAsync(ToStream(body), BuildTestSchema(), BuildTestCatalogue());

        Assert.Contains(findings, f => f.RuleId == "PARSE-002");
    }

    [Fact]
    public async Task ParseAsync_AbsentRequiredField_EmitsSchemaReq()
    {
        // 026 has 2 required fields; provide only 1 (truncated line).
        var body = "026|1234567890121|\r\n028|S95A123456|R|\r\n";

        var (_, findings) = await FlatFileParser.ParseAsync(ToStream(body), BuildTestSchema(), BuildTestCatalogue());

        Assert.Contains(findings, f => f.RuleId == "SCHEMA-REQ");
    }

    [Fact]
    public async Task ParseAsync_EmptyOptionalField_SilentSkip()
    {
        // 030 field[1] (J0045, BOOLEAN, optional) is absent.
        var body =
            "026|1234567890121|V|\r\n" +
            "028|S95A123456|R|\r\n" +
            "030|01|\r\n" +
            "032|T|\r\n";

        var (_, findings) = await FlatFileParser.ParseAsync(ToStream(body), BuildTestSchema(), BuildTestCatalogue());

        // No SCHEMA-REQ for J0045 (it's optional).
        Assert.DoesNotContain(findings, f => f.RuleId == "SCHEMA-REQ");
    }

    [Fact]
    public async Task ParseAsync_LineNumbers_AreOneBased()
    {
        var body = "026|1234567890121|V|\r\nXXX|bad|\r\n";

        var (_, findings) = await FlatFileParser.ParseAsync(ToStream(body), BuildTestSchema(), BuildTestCatalogue());

        var parse001 = findings.First(f => f.RuleId == "PARSE-001");
        Assert.Equal(2, parse001.LineNumber);
    }
}

