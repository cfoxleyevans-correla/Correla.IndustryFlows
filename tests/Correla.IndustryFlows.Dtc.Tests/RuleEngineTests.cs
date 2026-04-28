using System.Text.Json;
using Correla.IndustryFlows.Dtc.Schema;
using Correla.IndustryFlows.Dtc.Validation;
using Correla.IndustryFlows.Dtc.Validation.Predicates;

namespace Correla.IndustryFlows.Dtc.Tests;

/// <summary>
/// Tests for <see cref="RuleEngine"/> operators, built-in predicates,
/// and <see cref="PredicateRegistry"/>.
/// </summary>
public sealed class RuleEngineTests
{
    // ---- MPAN check digit predicate ----

    [Fact]
    public void MpanCheckDigit_ValidMpan_ReturnsTrue()
    {
        // 123456789012 → weighted sum 1084 → 1084%11=6 → 6%10=6 → check digit 6
        var predicate = new MpanCheckDigitPredicate();
        var root = MakeRoot();

        Assert.True(predicate.Evaluate("1234567890126", root));
    }

    [Fact]
    public void MpanCheckDigit_BadCheckDigit_ReturnsFalse()
    {
        var predicate = new MpanCheckDigitPredicate();

        Assert.False(predicate.Evaluate("1234567890121", MakeRoot())); // check should be 6
    }

    [Fact]
    public void MpanCheckDigit_WrongLength_ReturnsFalse()
    {
        Assert.False(new MpanCheckDigitPredicate().Evaluate("12345", MakeRoot()));
    }

    // ---- DtcDateTime predicate ----

    [Fact]
    public void DtcDateTime_ValidValue_ReturnsTrue() =>
        Assert.True(new DtcDateTimePredicate().Evaluate("20260415093000", MakeRoot()));

    [Fact]
    public void DtcDateTime_TooShort_ReturnsFalse() =>
        Assert.False(new DtcDateTimePredicate().Evaluate("202604150930", MakeRoot()));

    [Fact]
    public void DtcDateTime_InvalidMonth_ReturnsFalse() =>
        Assert.False(new DtcDateTimePredicate().Evaluate("20261332000000", MakeRoot()));

    // ---- DtcMidnightHh predicate ----

    [Fact]
    public void DtcMidnightHh_HhdcWithMidnight_ReturnsFalse()
    {
        var root = MakeRoot();
        root.Fields["__senderRole"] = "HHDC";
        var child = new GroupInstance { GroupCode = "030", LineNumber = 2, Parent = root };
        root.Children.Add(child);

        Assert.False(new DtcMidnightHhPredicate().Evaluate("20260415000000", child));
    }

    [Fact]
    public void DtcMidnightHh_NonHhdc_ReturnsTrue()
    {
        var root = MakeRoot();
        root.Fields["__senderRole"] = "NHHDA";
        var child = new GroupInstance { GroupCode = "030", LineNumber = 2, Parent = root };
        root.Children.Add(child);

        Assert.True(new DtcMidnightHhPredicate().Evaluate("20260415000000", child));
    }

    [Fact]
    public void DtcMidnightHh_HhdcNonMidnight_ReturnsTrue()
    {
        var root = MakeRoot();
        root.Fields["__senderRole"] = "HHDC";
        var child = new GroupInstance { GroupCode = "030", LineNumber = 2, Parent = root };
        root.Children.Add(child);

        Assert.True(new DtcMidnightHhPredicate().Evaluate("20260415093000", child));
    }

    // ---- UniqueWithinGroup predicate ----

    [Fact]
    public void UniqueWithinGroup_NoDuplicates_ReturnsTrue()
    {
        var parent = MakeRoot();
        var inst1 = new GroupInstance { GroupCode = "030", LineNumber = 1, Parent = parent };
        inst1.Fields["J0010"] = "01";
        parent.Children.Add(inst1);

        var inst2 = new GroupInstance { GroupCode = "030", LineNumber = 2, Parent = parent };
        inst2.Fields["J0010"] = "02";
        parent.Children.Add(inst2);

        Assert.True(new UniqueWithinGroupPredicate().Evaluate("01", inst1));
    }

    [Fact]
    public void UniqueWithinGroup_Duplicates_ReturnsFalse()
    {
        var parent = MakeRoot();
        var inst1 = new GroupInstance { GroupCode = "030", LineNumber = 1, Parent = parent };
        inst1.Fields["J0010"] = "01";
        parent.Children.Add(inst1);

        var inst2 = new GroupInstance { GroupCode = "030", LineNumber = 2, Parent = parent };
        inst2.Fields["J0010"] = "01"; // duplicate
        parent.Children.Add(inst2);

        Assert.False(new UniqueWithinGroupPredicate().Evaluate("01", inst2));
    }

    // ---- RuleEngine operator tests ----

    [Theory]
    [InlineData("equals", """{"field":"J0003","equals":"V"}""", "V", true)]
    [InlineData("equals-miss", """{"field":"J0003","equals":"V"}""", "X", false)]
    [InlineData("notEquals", """{"field":"J0003","notEquals":"V"}""", "X", true)]
    [InlineData("in", """{"field":"J0003","in":["A","V"]}""", "V", true)]
    [InlineData("in-miss", """{"field":"J0003","in":["A","V"]}""", "X", false)]
    [InlineData("notIn", """{"field":"J0003","notIn":["A","V"]}""", "X", true)]
    [InlineData("present-true", """{"field":"J0003","present":true}""", "V", true)]
    [InlineData("matches", """{"field":"J0003","matches":"^[A-Z]$"}""", "V", true)]
    [InlineData("matches-miss", """{"field":"J0003","matches":"^[0-9]+$"}""", "V", false)]
    public void RuleEngine_Operators_ProduceExpectedResults(
        string _, string conditionJson, string fieldValue, bool shouldPass)
    {
        var instance = MakeRoot();
        instance.GroupCode = "026";
        instance.Fields["J0003"] = fieldValue;

        var rule = BuildRule("TEST-001", "026", whenJson: null, expectJson: conditionJson);
        var schema = BuildSchemaWithRules([rule]);
        var engine = BuildEngine([]);

        var findings = engine.Evaluate(new DtcFile { Root = WrapInRoot(instance) }, schema);

        if (shouldPass)
        {
            Assert.DoesNotContain(findings, f => f.RuleId == "TEST-001");
        }
        else
        {
            Assert.Contains(findings, f => f.RuleId == "TEST-001");
        }
    }

    [Fact]
    public void RuleEngine_WhenConditionFalse_RuleSkipped()
    {
        // when: J0045=F; expect: J0332 present — but J0045 is 'T', so rule should not fire.
        var instance = MakeRoot();
        instance.GroupCode = "030";
        instance.Fields["J0045"] = "T";

        var rule = BuildRule("D0010-001", "030",
            whenJson: """{"field":"J0045","equals":"F"}""",
            expectJson: """{"field":"J0332","present":true}""");
        var engine = BuildEngine([]);
        var findings = engine.Evaluate(
            new DtcFile { Root = WrapInRoot(instance) },
            BuildSchemaWithRules([rule]));

        Assert.DoesNotContain(findings, f => f.RuleId == "D0010-001");
    }

    [Fact]
    public void RuleEngine_WhenConditionTrue_ExpectFails_EmitsFinding()
    {
        // when: J0045=F; expect: J0332 present — J0045 is F but J0332 absent.
        var instance = MakeRoot();
        instance.GroupCode = "030";
        instance.Fields["J0045"] = "F";
        // J0332 is NOT in Fields.

        var rule = BuildRule("D0010-001", "030",
            whenJson: """{"field":"J0045","equals":"F"}""",
            expectJson: """{"field":"J0332","present":true}""");
        var engine = BuildEngine([]);
        var findings = engine.Evaluate(
            new DtcFile { Root = WrapInRoot(instance) },
            BuildSchemaWithRules([rule]));

        Assert.Contains(findings, f => f.RuleId == "D0010-001");
    }

    [Fact]
    public void RuleEngine_SatisfiesMpanCheckDigit_ValidMpan_NoFinding()
    {
        var instance = MakeRoot();
        instance.GroupCode = "026";
        instance.Fields["J0003"] = "1234567890126"; // valid check digit

        var rule = BuildRule("D0010-007", "026",
            whenJson: null,
            expectJson: """{"field":"J0003","satisfies":"mpanCheckDigit"}""");
        var engine = BuildEngine([new MpanCheckDigitPredicate()]);
        var findings = engine.Evaluate(
            new DtcFile { Root = WrapInRoot(instance) },
            BuildSchemaWithRules([rule]));

        Assert.DoesNotContain(findings, f => f.RuleId == "D0010-007");
    }

    [Fact]
    public void RuleEngine_SatisfiesMpanCheckDigit_BadMpan_EmitsFinding()
    {
        var instance = MakeRoot();
        instance.GroupCode = "026";
        instance.Fields["J0003"] = "1234567890121"; // invalid check digit

        var rule = BuildRule("D0010-007", "026",
            whenJson: null,
            expectJson: """{"field":"J0003","satisfies":"mpanCheckDigit"}""");
        var engine = BuildEngine([new MpanCheckDigitPredicate()]);
        var findings = engine.Evaluate(
            new DtcFile { Root = WrapInRoot(instance) },
            BuildSchemaWithRules([rule]));

        Assert.Contains(findings, f => f.RuleId == "D0010-007");
    }

    // ---- Helpers ----

    private static GroupInstance MakeRoot()
    {
        return new GroupInstance { GroupCode = "ROOT", LineNumber = 0, Parent = null };
    }

    private static GroupInstance WrapInRoot(GroupInstance child)
    {
        var root = new GroupInstance { GroupCode = "ROOT", LineNumber = 0, Parent = null };
        child.Parent = root;
        root.Children.Add(child);
        return root;
    }

    private static Rule BuildRule(string id, string scope, string? whenJson, string expectJson)
    {
        return new Rule
        {
            Id = id,
            Severity = "error",
            Message = id,
            Scope = scope,
            When = whenJson is null ? null : JsonDocument.Parse(whenJson).RootElement,
            Expect = JsonDocument.Parse(expectJson).RootElement,
        };
    }

    private static FlowSchema BuildSchemaWithRules(IReadOnlyList<Rule> rules) =>
        new()
        {
            FlowId = "D0010",
            FlowVersion = "002",
            FlowName = "Test",
            Status = "Test",
            Ownership = "Test",
            Description = "Test",
            Routes = [],
            Notes = string.Empty,
            Groups = new Dictionary<string, GroupDefinition>(),
            Rules = rules,
        };

    private static RuleEngine BuildEngine(IEnumerable<IPredicate> predicates) =>
        new(new PredicateRegistry(predicates));
}

