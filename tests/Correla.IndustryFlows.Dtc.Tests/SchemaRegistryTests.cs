using Correla.IndustryFlows.Dtc.Schema;

namespace Correla.IndustryFlows.Dtc.Tests;

/// <summary>
/// Tests for <see cref="FileSchemaRegistry"/> and the schema model.
/// The smoke test exercises the entire bundle on disk; unit tests exercise
/// the registry in isolation against a known flow.
/// </summary>
public sealed class SchemaRegistryTests
{
    // Locate the bundle relative to the running test assembly.
    // Test output: tests/Correla.IndustryFlows.Dtc.Tests/bin/Debug/net10.0/
    // Bundle:      docs/elec/15.4/schemas/
    private static string BundlePath()
    {
        var dir = AppContext.BaseDirectory;
        // Walk up until we find the solution root (contains the .sln file).
        var current = new DirectoryInfo(dir);
        while (current is not null && !current.GetFiles("*.sln").Any())
        {
            current = current.Parent;
        }
        Assert.NotNull(current); // Guard: solution root must exist on disk.
        return Path.Combine(current.FullName, "docs", "elec", "15.4", "schemas");
    }

    private static FileSchemaRegistry CreateRegistry() => new(BundlePath());

    [Fact]
    public void Manifest_ContainsAtLeast206Flows()
    {
        var registry = CreateRegistry();

        Assert.True(registry.Manifest.Count >= 206,
            $"Expected ≥206 manifest entries, got {registry.Manifest.Count}.");
    }

    [Fact]
    public void TryGet_KnownFlow_ReturnsTrue()
    {
        var registry = CreateRegistry();

        bool found = registry.TryGet("D0010", "002", out var schema);

        Assert.True(found);
        Assert.NotNull(schema);
    }

    [Fact]
    public void TryGet_KnownFlow_SchemaHasExpectedGroups()
    {
        var registry = CreateRegistry();
        registry.TryGet("D0010", "002", out var schema);

        // D0010 has 7 groups: 026 027 028 029 030 032 033
        Assert.Equal(7, schema!.Groups.Count);
        Assert.True(schema.Groups.ContainsKey("026"));
        Assert.True(schema.Groups.ContainsKey("030"));
    }

    [Fact]
    public void TryGet_KnownFlow_GroupHasCorrectMetadata()
    {
        var registry = CreateRegistry();
        registry.TryGet("D0010", "002", out var schema);

        var g026 = schema!.Groups["026"];
        Assert.Equal("MPAN Cores", g026.Name);
        Assert.Null(g026.Parent);
        Assert.Equal(1, g026.Level);
        Assert.Equal(1, g026.Cardinality.Min);
        Assert.Null(g026.Cardinality.Max);

        var g030 = schema.Groups["030"];
        Assert.Equal("028", g030.Parent);
        Assert.Equal(3, g030.Level);
    }

    [Fact]
    public void TryGet_KnownFlow_FieldsArePopulated()
    {
        var registry = CreateRegistry();
        registry.TryGet("D0010", "002", out var schema);

        var fields = schema!.Groups["026"].Fields;
        Assert.Equal(2, fields.Count);
        Assert.Equal("J0003", fields[0].Ref);
        Assert.True(fields[0].Required);
    }

    [Fact]
    public void TryGet_UnknownFlow_ReturnsFalse()
    {
        var registry = CreateRegistry();

        bool found = registry.TryGet("D9999", "001", out var schema);

        Assert.False(found);
        Assert.Null(schema);
    }

    [Fact]
    public void TryGet_CalledTwiceForSameFlow_ReturnsSameInstance()
    {
        // Registry caches loaded schemas — same object reference expected.
        var registry = CreateRegistry();
        registry.TryGet("D0010", "002", out var first);
        registry.TryGet("D0010", "002", out var second);

        Assert.Same(first, second);
    }

    [Fact]
    public void DataItems_ContainsKnownItem()
    {
        var registry = CreateRegistry();

        bool found = registry.TryGetDataItem("J0171", out var item);

        Assert.True(found);
        Assert.Equal("J0171", item!.Ref);
        Assert.Equal("enum", item.ValidSet.Kind);
    }

    [Fact]
    public void DataItems_EnumValidSet_HasExpectedValues()
    {
        var registry = CreateRegistry();
        registry.TryGetDataItem("J0171", out var item);

        var values = item!.ValidSet.EnumValues;
        Assert.NotNull(values);
        Assert.Equal(18, values.Count);
        Assert.Contains(values, v => v.Code == "A");
        Assert.Contains(values, v => v.Code == "R");
    }

    [Fact]
    public void DataItems_ConstraintValidSet_HasText()
    {
        // J0003 (MPAN Core) has a constraint valid set, not an enum.
        var registry = CreateRegistry();
        registry.TryGetDataItem("J0003", out var item);

        Assert.Equal("constraint", item!.ValidSet.Kind);
        Assert.False(string.IsNullOrWhiteSpace(item.ValidSet.ConstraintText));
    }

    // ---- Bundle smoke test ----

    [Fact]
    public void SmokeTest_AllManifestFilesExistOnDisk()
    {
        var registry = CreateRegistry();
        var bundlePath = BundlePath();

        foreach (var entry in registry.Manifest)
        {
            var fullPath = Path.Combine(bundlePath, entry.File);
            Assert.True(File.Exists(fullPath),
                $"Manifest entry {entry.FlowId} v{entry.FlowVersion} points to '{entry.File}' which does not exist.");
        }
    }

    [Fact]
    public void SmokeTest_EachFlowHasAtLeastOneRootGroup()
    {
        var registry = CreateRegistry();

        foreach (var entry in registry.Manifest)
        {
            bool loaded = registry.TryGet(entry.FlowId, entry.FlowVersion, out var schema);
            Assert.True(loaded, $"Failed to load {entry.FlowId} v{entry.FlowVersion}.");

            bool hasRoot = schema!.Groups.Values.Any(g => g.Parent is null);
            Assert.True(hasRoot,
                $"{entry.FlowId} v{entry.FlowVersion} has no root-level group (parent == null).");
        }
    }

    [Fact]
    public void SmokeTest_AllParentReferencesExistInSameFlow()
    {
        var registry = CreateRegistry();

        foreach (var entry in registry.Manifest)
        {
            registry.TryGet(entry.FlowId, entry.FlowVersion, out var schema);

            foreach (var (code, def) in schema!.Groups)
            {
                if (def.Parent is null)
                {
                    continue;
                }

                Assert.True(schema.Groups.ContainsKey(def.Parent),
                    $"{entry.FlowId} group '{code}' declares parent '{def.Parent}' which does not exist in the same flow.");
            }
        }
    }
}

