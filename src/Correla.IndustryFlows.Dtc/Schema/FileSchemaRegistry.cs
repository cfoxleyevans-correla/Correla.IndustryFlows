using System.Text.Json.Nodes;

namespace Correla.IndustryFlows.Dtc.Schema;

/// <summary>
/// Loads the DTC schema bundle from a directory on disk.
/// <c>manifest.json</c> and <c>data-items.json</c> are read eagerly at construction.
/// Individual flow files are loaded lazily on the first <see cref="TryGet"/> call
/// for that flow and cached for the lifetime of the registry.
/// All public members are thread-safe after construction.
/// </summary>
public sealed class FileSchemaRegistry : ISchemaRegistry
{
    private readonly string _bundleRoot;
    private readonly IReadOnlyCollection<ManifestEntry> _manifest;

    // Maps (flowId, flowVersion) → lazy loader. Thread-safe: populated once at construction.
    private readonly IReadOnlyDictionary<string, Lazy<FlowSchema>> _loaders;

    // Data items loaded eagerly from data-items.json.
    private readonly IReadOnlyDictionary<string, DataItem> _dataItems;

    /// <summary>
    /// Initialises the registry by reading the manifest and data-item files.
    /// Individual flow schema files are not yet loaded.
    /// </summary>
    /// <param name="bundleRoot">
    /// Absolute path to the directory containing <c>manifest.json</c>.
    /// </param>
    /// <exception cref="InvalidOperationException">
    /// Thrown when <c>manifest.json</c> or <c>data-items.json</c> cannot be read.
    /// </exception>
    public FileSchemaRegistry(string bundleRoot)
    {
        _bundleRoot = bundleRoot;

        _manifest = LoadManifest(bundleRoot);
        _dataItems = LoadDataItems(bundleRoot);
        _loaders = BuildLoaders(_manifest, bundleRoot);
    }

    /// <inheritdoc/>
    public IReadOnlyCollection<ManifestEntry> Manifest => _manifest;

    /// <inheritdoc/>
    public bool TryGet(string flowId, string flowVersion, out FlowSchema? schema)
    {
        var key = MakeKey(flowId, flowVersion);

        if (!_loaders.TryGetValue(key, out var lazy))
        {
            schema = null;
            return false;
        }

        // Lazy<T> is thread-safe by default (LazyThreadSafetyMode.ExecutionAndPublication).
        schema = lazy.Value;
        return true;
    }

    /// <inheritdoc/>
    public bool TryGetDataItem(string jRef, out DataItem? item) =>
        _dataItems.TryGetValue(jRef, out item);

    // ---- Private helpers ----

    /// <summary>Reads and deserialises manifest.json.</summary>
    private static IReadOnlyCollection<ManifestEntry> LoadManifest(string bundleRoot)
    {
        var path = Path.Combine(bundleRoot, "manifest.json");
        var json = File.ReadAllText(path);
        var doc = JsonNode.Parse(json)
            ?? throw new InvalidOperationException($"Failed to parse {path}.");

        var flows = doc["flows"]?.AsArray()
            ?? throw new InvalidOperationException("manifest.json missing 'flows' array.");

        var entries = new List<ManifestEntry>(flows.Count);

        foreach (var node in flows)
        {
            entries.Add(new ManifestEntry(
                FlowId: node!["flowId"]!.GetValue<string>(),
                FlowVersion: node["flowVersion"]!.GetValue<string>(),
                FlowName: node["flowName"]?.GetValue<string>() ?? string.Empty,
                File: node["file"]!.GetValue<string>()));
        }

        return entries.AsReadOnly();
    }

    /// <summary>Reads and deserialises data-items.json into a lookup dictionary.</summary>
    private static IReadOnlyDictionary<string, DataItem> LoadDataItems(string bundleRoot)
    {
        var path = Path.Combine(bundleRoot, "data-items.json");
        var json = File.ReadAllText(path);
        var doc = JsonNode.Parse(json)?.AsObject()
            ?? throw new InvalidOperationException($"Failed to parse {path}.");

        var items = new Dictionary<string, DataItem>(doc.Count, StringComparer.Ordinal);

        foreach (var (key, node) in doc)
        {
            items[key] = ParseDataItem(node!);
        }

        return items;
    }

    /// <summary>Deserialises a single data-item node.</summary>
    private static DataItem ParseDataItem(JsonNode node) =>
        new()
        {
            Ref = node["ref"]!.GetValue<string>(),
            Name = node["name"]!.GetValue<string>(),
            Domain = node["domain"]?.GetValue<string>() ?? string.Empty,
            LogicalFormat = node["logicalFormat"]?.GetValue<string>() ?? string.Empty,
            PhysicalLength = node["physicalLength"]?.GetValue<string>() ?? string.Empty,
            ValidSet = ParseValidSet(node["validSet"]),
            Notes = node["notes"]?.GetValue<string>() ?? string.Empty,
        };

    /// <summary>Deserialises a validSet node into a <see cref="ValidSet"/>.</summary>
    private static ValidSet ParseValidSet(JsonNode? node)
    {
        if (node is null)
        {
            return new ValidSet { Kind = "none" };
        }

        var kind = node["kind"]?.GetValue<string>() ?? "none";

        if (kind == "enum")
        {
            var valuesNode = node["values"]?.AsArray();
            var values = valuesNode is null
                ? []
                : valuesNode
                    .Select(v => new EnumValue(
                        Code: v!["code"]!.GetValue<string>(),
                        Label: v["label"]?.GetValue<string>() ?? string.Empty))
                    .ToList()
                    .AsReadOnly();

            return new ValidSet { Kind = "enum", EnumValues = values };
        }

        if (kind == "constraint")
        {
            return new ValidSet
            {
                Kind = "constraint",
                ConstraintText = node["text"]?.GetValue<string>() ?? string.Empty,
            };
        }

        return new ValidSet { Kind = "none" };
    }

    /// <summary>Builds one Lazy loader per manifest entry — flows are parsed on first access.</summary>
    private static IReadOnlyDictionary<string, Lazy<FlowSchema>> BuildLoaders(
        IReadOnlyCollection<ManifestEntry> manifest,
        string bundleRoot)
    {
        var loaders = new Dictionary<string, Lazy<FlowSchema>>(manifest.Count, StringComparer.Ordinal);

        foreach (var entry in manifest)
        {
            // Capture the entry for the lambda — loop variable capture safety.
            var captured = entry;
            var key = MakeKey(entry.FlowId, entry.FlowVersion);

            loaders[key] = new Lazy<FlowSchema>(
                () => LoadFlow(Path.Combine(bundleRoot, captured.File)),
                LazyThreadSafetyMode.ExecutionAndPublication);
        }

        return loaders;
    }

    /// <summary>Reads and deserialises a per-flow schema file.</summary>
    private static FlowSchema LoadFlow(string path)
    {
        var json = File.ReadAllText(path);
        var node = JsonNode.Parse(json)
            ?? throw new InvalidOperationException($"Failed to parse flow file: {path}.");

        var groupsNode = node["groups"]?.AsObject()
            ?? throw new InvalidOperationException($"Flow file '{path}' is missing 'groups'.");

        var groups = new Dictionary<string, GroupDefinition>(groupsNode.Count, StringComparer.Ordinal);

        foreach (var (code, groupNode) in groupsNode)
        {
            groups[code] = ParseGroup(groupNode!);
        }

        var routes = ParseRoutes(node["routes"]?.AsArray());

        return new FlowSchema
        {
            FlowId = node["flowId"]!.GetValue<string>(),
            FlowVersion = node["flowVersion"]!.GetValue<string>(),
            FlowName = node["flowName"]?.GetValue<string>() ?? string.Empty,
            Status = node["status"]?.GetValue<string>() ?? string.Empty,
            Ownership = node["ownership"]?.GetValue<string>() ?? string.Empty,
            Description = node["description"]?.GetValue<string>() ?? string.Empty,
            Routes = routes,
            Groups = groups,
            Rules = LoadRulePack(path),
            Notes = node["notes"]?.GetValue<string>() ?? string.Empty,
        };
    }

    /// <summary>Deserialises a group node.</summary>
    private static GroupDefinition ParseGroup(JsonNode node)
    {
        var cardNode = node["cardinality"];
        var cardinality = new Cardinality(
            Min: cardNode?["min"]?.GetValue<int>() ?? 0,
            Max: cardNode?["max"]?.GetValue<int?>());

        var fieldsNode = node["fields"]?.AsArray();
        var fields = fieldsNode is null
            ? (IReadOnlyList<FieldDefinition>)[]
            : fieldsNode
                .Select(f => new FieldDefinition(
                    Ref: f!["ref"]!.GetValue<string>(),
                    Name: f["name"]?.GetValue<string>() ?? string.Empty,
                    Format: f["format"]?.GetValue<string>() ?? string.Empty,
                    Required: f["required"]?.GetValue<bool>() ?? false))
                .ToList()
                .AsReadOnly();

        return new GroupDefinition
        {
            Name = node["name"]?.GetValue<string>() ?? string.Empty,
            Parent = node["parent"]?.GetValue<string?>(),
            Level = node["level"]?.GetValue<int>() ?? 1,
            Cardinality = cardinality,
            Condition = node["condition"]?.GetValue<string>() ?? string.Empty,
            Fields = fields,
        };
    }

    /// <summary>Deserialises the routes array.</summary>
    private static IReadOnlyList<FlowRoute> ParseRoutes(JsonArray? node)
    {
        if (node is null)
        {
            return [];
        }

        return node
            .Select(r => new FlowRoute(
                From: r!["from"]!.GetValue<string>(),
                To: r["to"]!.GetValue<string>(),
                Version: r["version"]?.GetValue<string>() ?? string.Empty))
            .ToList()
            .AsReadOnly();
    }

    /// <summary>Builds the dictionary key from a (flowId, flowVersion) pair.</summary>
    private static string MakeKey(string flowId, string flowVersion) =>
        $"{flowId}:{flowVersion}";

    /// <summary>
    /// Looks for a companion rule pack file at
    /// <c>rules/{flowId}_v{version}.rules.json</c> alongside the flow file.
    /// Returns an empty list when no rule pack exists.
    /// </summary>
    private static IReadOnlyList<Rule> LoadRulePack(string flowFilePath)
    {
        var dir = Path.GetDirectoryName(flowFilePath)!;
        var flowFileName = Path.GetFileNameWithoutExtension(flowFilePath); // e.g. D0010_v002
        var rulesPath = Path.Combine(Path.GetDirectoryName(dir)!, "rules", $"{flowFileName}.rules.json");

        if (!File.Exists(rulesPath))
        {
            return [];
        }

        var json = File.ReadAllText(rulesPath);
        var node = JsonNode.Parse(json);
        var rulesArray = node?["rules"]?.AsArray();

        if (rulesArray is null)
        {
            return [];
        }

        var rules = new List<Rule>(rulesArray.Count);

        foreach (var ruleNode in rulesArray)
        {
            if (ruleNode is null)
            {
                continue;
            }

            var expectElement = System.Text.Json.JsonSerializer
                .Deserialize<System.Text.Json.JsonElement>(ruleNode["expect"]!.ToJsonString());

            System.Text.Json.JsonElement? whenElement = ruleNode["when"] is not null
                ? System.Text.Json.JsonSerializer
                    .Deserialize<System.Text.Json.JsonElement>(ruleNode["when"]!.ToJsonString())
                : null;

            rules.Add(new Rule
            {
                Id = ruleNode["id"]!.GetValue<string>(),
                Severity = ruleNode["severity"]?.GetValue<string>() ?? "error",
                Message = ruleNode["message"]?.GetValue<string>() ?? string.Empty,
                Scope = ruleNode["scope"]!.GetValue<string>(),
                When = whenElement,
                Expect = expectElement,
            });
        }

        return rules.AsReadOnly();
    }
}

