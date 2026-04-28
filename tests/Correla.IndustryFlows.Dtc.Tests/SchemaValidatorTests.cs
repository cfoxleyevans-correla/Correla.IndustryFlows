using Correla.IndustryFlows.Dtc.Schema;
using Correla.IndustryFlows.Dtc.Validation;

namespace Correla.IndustryFlows.Dtc.Tests;

/// <summary>
/// Tests for <see cref="SchemaValidator"/>. Builds small trees in memory
/// and asserts the correct findings are (or are not) emitted.
/// </summary>
public sealed class SchemaValidatorTests
{
    private static GroupInstance MakeGroup(string code, int line, GroupInstance parent,
        Dictionary<string, object>? fields = null)
    {
        var g = new GroupInstance { GroupCode = code, LineNumber = line, Parent = parent };
        if (fields is not null)
        {
            foreach (var (k, v) in fields)
            {
                g.Fields[k] = v;
            }
        }

        return g;
    }

    private static FlowSchema BuildSchema() => new()
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
            // Root group — min 1 occurrence.
            ["026"] = new GroupDefinition
            {
                Name = "MPAN Cores",
                Parent = null,
                Level = 1,
                Cardinality = new Cardinality(1, null),
                Condition = string.Empty,
                Fields = [new FieldDefinition("J0003", "MPAN Core", "NUM(13)", true)],
            },
            // Child — min 1, max 1.
            ["032"] = new GroupDefinition
            {
                Name = "Validation Result",
                Parent = "026",
                Level = 2,
                Cardinality = new Cardinality(1, 1),
                Condition = string.Empty,
                Fields = [],
            },
        },
    };

    [Fact]
    public void Validate_ValidTree_NoFindings()
    {
        var root = MakeGroup("ROOT", 0, null!);
        var mpan = MakeGroup("026", 1, root, new() { ["J0003"] = "1234567890121" });
        root.Children.Add(mpan);
        var vr = MakeGroup("032", 2, mpan);
        mpan.Children.Add(vr);

        var findings = SchemaValidator.Validate(new DtcFile { Root = root }, BuildSchema());

        Assert.Empty(findings);
    }

    [Fact]
    public void Validate_MissingMandatoryRootGroup_EmitsCardMin()
    {
        // No 026 instances at all — violates min=1.
        var root = MakeGroup("ROOT", 0, null!);

        var findings = SchemaValidator.Validate(new DtcFile { Root = root }, BuildSchema());

        Assert.Contains(findings, f => f.RuleId == "SCHEMA-CARD-MIN" && f.Path.Contains("026"));
    }

    [Fact]
    public void Validate_MissingMandatoryChildGroup_EmitsCardMin()
    {
        // 026 exists but has no 032 child — violates min=1.
        var root = MakeGroup("ROOT", 0, null!);
        var mpan = MakeGroup("026", 1, root, new() { ["J0003"] = "1234567890121" });
        root.Children.Add(mpan);

        var findings = SchemaValidator.Validate(new DtcFile { Root = root }, BuildSchema());

        Assert.Contains(findings, f => f.RuleId == "SCHEMA-CARD-MIN" && f.Path.Contains("032"));
    }

    [Fact]
    public void Validate_TooManyOccurrences_EmitsCardMax()
    {
        // 032 has max=1 but we add 2 instances under the same 026.
        var root = MakeGroup("ROOT", 0, null!);
        var mpan = MakeGroup("026", 1, root, new() { ["J0003"] = "1234567890121" });
        root.Children.Add(mpan);
        mpan.Children.Add(MakeGroup("032", 2, mpan));
        mpan.Children.Add(MakeGroup("032", 3, mpan));

        var findings = SchemaValidator.Validate(new DtcFile { Root = root }, BuildSchema());

        Assert.Contains(findings, f => f.RuleId == "SCHEMA-CARD-MAX" && f.Path.Contains("032"));
    }
}

