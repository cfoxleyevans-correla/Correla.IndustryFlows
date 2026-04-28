# DTC Parser & Validator — Implementation Specification

**For .NET runtime consuming schemas extracted from Elexon DTC v15.4**

Document version 1.0 · April 2026

---

## Table of contents

1. [Overview & goals](#1-overview--goals)
2. [High-level architecture](#2-high-level-architecture)
3. [Schema bundle](#3-schema-bundle)
4. [The DTC flat-file format](#4-the-dtc-flat-file-format)
5. [.NET project structure](#5-net-project-structure)
6. [Rule pack](#6-rule-pack)
7. [Parser behaviour — line by line](#7-parser-behaviour--line-by-line)
8. [Schema loading & dependency injection](#8-schema-loading--dependency-injection)
9. [Findings — what the runtime returns](#9-findings--what-the-runtime-returns)
10. [Testing strategy](#10-testing-strategy)
11. [Worked example — D0010 round-trip](#11-worked-example--d0010-round-trip)
12. [Acceptance criteria](#12-acceptance-criteria)
- [Appendix A — Glossary](#appendix-a--glossary)
- [Appendix B — Related deliverables](#appendix-b--related-deliverables)
- [Appendix C — Open questions for design review](#appendix-c--open-questions-for-design-review)

---

## 1. Overview & goals

This document specifies the runtime implementation of a generic, schema-driven parser and validator for files exchanged under the Elexon Data Transfer Catalogue (DTC) — currently version 15.4. The runtime consumes a JSON schema bundle produced by a separate extraction process from the four Elexon source documents (Domains, Data Item Catalogue, Data Flow Catalogue, Cross Reference).

The runtime is to be implemented in .NET (target: net8.0). Its goal is that adding a new flow — or absorbing a new DTC version — requires only a schema regeneration: zero code changes.

### 1.1 Why generic

There are around 200 active flows in DTC v15.4. Each has its own group hierarchy, field set, and cardinality rules. Hand-coding a parser per flow is a maintenance burden that compounds with every catalogue release. By describing the grammar in JSON and writing one schema-driven parser, every flow benefits from improvements in one place, and the system stays in sync with Elexon's documents automatically.

### 1.2 Out of scope

- Generation of outbound DTC files (this spec covers parse/validate only — generation will reuse the same schema model in a follow-up component).
- Transport (FTP, SFTP, MTAS, etc.) — the runtime accepts an arbitrary `Stream`.
- Persistence of parsed results — the runtime returns an in-memory object graph; how it's stored is the host application's concern.
- UI / reporting — the runtime exposes `Findings`; presentation is a host concern.

---

## 2. High-level architecture

The runtime has three logical phases. Each is composed from injected services so individual phases can be swapped or augmented in tests.

### 2.1 Phase 1 — Envelope detection

Every DTC file begins with a `ZHV` (file header) row that carries the flow ID, flow version, and sender/recipient identities. The first phase reads only this row, returning an `Envelope` record. Reading the body before knowing which schema applies would be wasted work.

### 2.2 Phase 2 — Schema-driven parsing

Given the envelope, the runtime resolves the appropriate `FlowSchema` from an `ISchemaRegistry`. The schema describes the group hierarchy: which groups exist, what their parent is, what fields they carry, and what cardinality applies. The parser walks the file line-by-line, using the schema as a grammar.

Output of this phase is a tree of `GroupInstance` objects mirroring the file's structure, plus a list of structural `Findings` (unknown group codes, parent mismatches, malformed lines).

### 2.3 Phase 3 — Validation

Two-layer validation, applied to the parsed tree:

- **Structural validation** — cardinalities (min/max occurrences of each group), required fields, type/length/enum conformance for each field. Driven by the same schema. No business knowledge.
- **Rule-pack validation** — cross-field and contextual rules ("if flag=F then reason code is mandatory"; "HHDC senders must use reading type I or F"; MPAN check digit; HH midnight `23:59:59` quirk). Each rule is data-described in JSON; predicates that can't be expressed declaratively are named built-ins, registered through `IPredicateRegistry`.

### 2.4 Component diagram

```
┌───────────────────────────────────────────────────────────────────┐
│                          DtcProcessor                             │
│   (façade — Stream in, ProcessingResult out)                      │
└──┬────────────────┬───────────────┬───────────────┬───────────────┘
   │                │               │               │
   ▼                ▼               ▼               ▼
┌────────┐    ┌──────────────┐  ┌──────────┐    ┌──────────┐
│Envelope│    │   Schema     │  │FlatFile  │    │ Rule     │
│Reader  │    │   Registry   │  │ Parser   │    │ Engine   │
└────────┘    │  (manifest + │  └────┬─────┘    └────┬─────┘
              │  flow files) │       │               │
              └──────┬───────┘       ▼               ▼
                     │          ┌─────────┐    ┌──────────┐
                     │          │ Field   │    │Predicate │
                     │          │ Coercer │    │ Registry │
                     ▼          └─────────┘    └──────────┘
              ┌──────────────┐
              │ Data Item    │
              │  Catalogue   │
              └──────────────┘
```

---

## 3. Schema bundle

The runtime is fed a directory containing the bundle produced by the `dtc-schema-extractor` skill. Layout:

```
<bundle-root>/
├── manifest.json
├── domains.json
├── data-items.json
├── cross-reference.json
└── flows/
    ├── D0001_v001.json
    ├── D0002_v001.json
    ├── ...
    └── D0010_v002.json
```

### 3.1 `manifest.json`

The runtime's first read. Maps `(flowId, flowVersion)` → relative file path.

```json
{
  "$schema": "https://example.com/dtc/flow-schema/v1/manifest",
  "dtcVersion": "15.4",
  "generatedAt": "2026-04-28T13:56:08Z",
  "flows": [
    {"flowId": "D0010", "flowVersion": "002",
     "flowName": "Meter Readings",
     "file": "flows/D0010_v002.json"}
  ]
}
```

### 3.2 Per-flow schema

The shape the runtime operates on. The `groups` field is keyed by group code (the literal string at the start of each DTC line: `026`, `82B`, `ZPD`, etc.). Codes are opaque strings — never assume they're numeric.

```json
{
  "$schema": "https://example.com/dtc/flow-schema/v1",
  "flowId": "D0010",
  "flowVersion": "002",
  "flowName": "Meter Readings",
  "status": "Operational",
  "ownership": "MRA",
  "description": "Cumulative readings and maximum demand readings.",
  "routes": [
    {"from": "NHHDC", "to": "Supplier", "version": "15.4"}
  ],
  "groups": {
    "026": {
      "name": "MPAN Cores",
      "parent": null,
      "level": 1,
      "cardinality": {"min": 1, "max": null},
      "condition": "",
      "fields": [
        {"ref": "J0003", "name": "MPAN Core",
         "format": "NUM(13)", "required": true},
        {"ref": "J0022", "name": "BSC Validation Status",
         "format": "CHAR(1)", "required": true}
      ]
    },
    "030": {
      "name": "Register Readings",
      "parent": "028",
      "level": 3,
      "cardinality": {"min": 0, "max": null},
      "fields": []
    }
  },
  "rules": [],
  "notes": "..."
}
```

Cardinality semantics:

| Catalogue range | min | max  | Meaning                       |
|-----------------|-----|------|-------------------------------|
| `1`             | 1   | 1    | Exactly one occurrence        |
| `0-1`           | 0   | 1    | At most one occurrence        |
| `1-*`           | 1   | null | One or more (unbounded)       |
| `0-*`           | 0   | null | Zero or more (unbounded)      |

### 3.3 `data-items.json`

Dictionary keyed by J-reference. The runtime uses each entry's `logicalFormat`, `physicalLength`, `validSet`, and `domain` when coercing and validating field values.

```json
{
  "J0171": {
    "ref": "J0171",
    "name": "Reading Type",
    "domain": "Code",
    "logicalFormat": "CHAR(1)",
    "physicalLength": "1",
    "validSet": {
      "kind": "enum",
      "values": [
        {"code": "A", "label": "Actual Change of Supplier Read"},
        {"code": "C", "label": "Customer own read"}
      ]
    },
    "notes": "Value O is only to be used in D0071 and D0300 flows."
  }
}
```

`validSet.kind` values:

- `"enum"` — `values` is an array of `{code, label}` objects. The runtime validates field values against codes.
- `"constraint"` — `text` describes a free-form rule. Treat as informational only; format/length checks still apply.
- `"none"` — no valid-set info; rely on format and domain alone.

---

## 4. The DTC flat-file format

DTC files are pipe-delimited (`|`) ASCII text. Each line is one record. The first field is a group code (3 characters, alphanumeric) that identifies which group definition the line belongs to. Subsequent fields are positional — they correspond, in order, to the `fields[]` array in that group's schema entry.

Worked example — a fragment of a D0010 file:

```
ZHV|FILE-12345|D0010|002|NHHDA|UDMS|...
026|1234567890121|V|
028|S95A123456|R|
030|01|20260415000000|045231||||T|N|
032||T|
030|02|20260415000000|023987||||T|N|
032||T|
ZPT|6|
```

Reading top-down:

- `ZHV` is the file header — used for envelope detection only.
- `026` opens an MPAN Cores group with two fields (MPAN Core = `1234567890121`, BSC Validation Status = `V`).
- `028` opens a Meter/Reading Types group, child of `026`.
- `030` opens a Register Readings group, child of `028`. It carries 7 ordered fields; empty fields between consecutive pipes are optional and absent.
- `032` opens a Validation Result group, child of `030`.
- Subsequent `030` starts a new sibling under the same `028`.
- `ZPT` is the file trailer — record count and checksum, used for envelope verification.

### 4.1 Datetime encoding

DTC datetimes use the layout `ccyymmddhhmmss` with no separators. A reading taken at midnight on 2 April 2026 is encoded as `20260402000000`. There is one exception worth flagging in HH contexts:

When an HHDC sends a midnight half-hourly reading on a D0010 to Supplier or Distributor, the time portion must be `235959` of the same day — not `000000` of the following day. This is documented in the catalogue's notes for J0016 and is enforced by rule pack predicate `dtcMidnightHh` (see §6).

### 4.2 Empty optional fields

Optional fields that are absent appear as empty strings between pipe delimiters. The parser must distinguish between "absent optional" (silent skip) and "absent required" (Finding emitted).

### 4.3 Group nesting through ordering

The flat file does not bracket groups explicitly. A child group is whichever group line appears next, provided the schema declares its parent is one of the currently-open groups. The parser maintains a stack of open groups; when it sees a line whose declared parent is on the stack, it pops back to that level and pushes the new instance.

---

## 5. .NET project structure

The recommended layout. Multi-project solution; the `Schemas` project ships embedded resources or references the bundle path via configuration.

```
DtcParser.sln
├── src/
│   ├── DtcParser.Core/                 (net8.0)
│   │   ├── DtcProcessor.cs             ← façade
│   │   ├── Schema/
│   │   │   ├── FlowSchema.cs
│   │   │   ├── GroupDefinition.cs
│   │   │   ├── FieldDefinition.cs
│   │   │   ├── DataItem.cs
│   │   │   ├── Domain.cs
│   │   │   ├── Cardinality.cs
│   │   │   ├── Rule.cs
│   │   │   ├── ISchemaRegistry.cs
│   │   │   └── FileSchemaRegistry.cs
│   │   ├── Parsing/
│   │   │   ├── Envelope.cs
│   │   │   ├── EnvelopeReader.cs
│   │   │   ├── DtcFile.cs
│   │   │   ├── GroupInstance.cs
│   │   │   ├── FlatFileParser.cs
│   │   │   └── FieldCoercer.cs
│   │   ├── Validation/
│   │   │   ├── Finding.cs
│   │   │   ├── Severity.cs
│   │   │   ├── SchemaValidator.cs
│   │   │   ├── RuleEngine.cs
│   │   │   └── Predicates/
│   │   │       ├── IPredicate.cs
│   │   │       ├── IPredicateRegistry.cs
│   │   │       ├── BuiltInPredicates.cs
│   │   │       ├── MpanCheckDigit.cs
│   │   │       └── DtcMidnightHh.cs
│   │   └── DependencyInjection/
│   │       └── ServiceCollectionExtensions.cs
│   │
│   ├── DtcParser.Schemas/              (resource library)
│   │   ├── manifest.json
│   │   ├── domains.json
│   │   ├── data-items.json
│   │   ├── cross-reference.json
│   │   └── flows/...
│   │
│   └── DtcParser.Cli/                  (optional thin host)
│       └── Program.cs
│
└── tests/
    ├── DtcParser.Core.Tests/
    │   ├── ParsingTests.cs
    │   ├── ValidationTests.cs
    │   ├── EnvelopeTests.cs
    │   └── Fixtures/                   ← sample D0010 files etc.
    └── DtcParser.IntegrationTests/
```

### 5.1 Public API surface

Calling code should only ever need `DtcProcessor` and the result types. Everything else is internal or injected through DI.

```csharp
public sealed class DtcProcessor
{
    public DtcProcessor(ISchemaRegistry registry,
                        IDataItemCatalogue items,
                        IRuleEngine ruleEngine);

    public ProcessingResult Process(Stream input,
                                    ProcessingContext? context = null);
    public Task<ProcessingResult> ProcessAsync(Stream input,
                                               ProcessingContext? context = null,
                                               CancellationToken ct = default);
}

public sealed record ProcessingResult(
    bool Success,
    Envelope? Envelope,
    DtcFile? Parsed,
    IReadOnlyList<Finding> Findings,
    string? FailureReason);

public sealed record ProcessingContext(
    string? SenderRoleOverride = null,
    DateTimeOffset? FileReceivedAt = null,
    IReadOnlyDictionary<string,string>? Extra = null);

public enum Severity { Info, Warning, Error }

public sealed record Finding(
    string RuleId,
    Severity Severity,
    string Path,
    string Message,
    int? LineNumber = null);
```

---

## 6. Rule pack

Cross-field and context-sensitive rules cannot be expressed by structural schema alone. The runtime supports a rule pack — a JSON document loaded alongside the per-flow schema — describing rules in a small declarative vocabulary.

### 6.1 Rule format

```json
{
  "flowId": "D0010",
  "flowVersion": "002",
  "rules": [
    {
      "id": "D0010-001",
      "severity": "error",
      "message": "Suspect reading (meterReadingFlag=F) requires a meterReadingReasonCode",
      "scope": "030",
      "when":   { "field": "J0045", "equals": "F" },
      "expect": { "child": "032", "field": "J0332", "present": true }
    },
    {
      "id": "D0010-006",
      "severity": "error",
      "message": "When sender is HHDC, readingType must be I or F",
      "scope": "028",
      "when":   { "context": "senderRole", "equals": "HHDC" },
      "expect": { "field": "J0171", "in": ["I", "F"] }
    },
    {
      "id": "D0010-007",
      "severity": "error",
      "message": "MPAN check digit invalid",
      "scope": "026",
      "expect": { "field": "J0003", "satisfies": "mpanCheckDigit" }
    }
  ]
}
```

### 6.2 Vocabulary

`scope` is the group code at which the rule fires — once per instance of that group.

`when` is an optional precondition; if absent or true, `expect` is checked.

`expect` is an assertion; if false, a `Finding` is emitted at the rule's severity.

Operators supported in `when` and `expect`:

| Operator     | Meaning                                                              |
|--------------|----------------------------------------------------------------------|
| `equals`     | Field equals literal value                                           |
| `notEquals`  | Field does not equal literal value                                   |
| `in`         | Field value is in a given list                                       |
| `notIn`      | Field value is not in a given list                                   |
| `present`    | Field is present (true) or absent (false)                            |
| `matches`    | Field matches a given .NET regex pattern                             |
| `satisfies`  | Field satisfies a named built-in predicate (see §6.3)                |

### 6.3 Built-in predicates

Predicates are .NET classes implementing `IPredicate`, registered into `IPredicateRegistry` by name. The runtime ships these as defaults:

| Name                  | Behaviour                                                                                  |
|-----------------------|--------------------------------------------------------------------------------------------|
| `mpanCheckDigit`      | Validates the modulus check digit on a 13-digit MPAN core                                  |
| `amsidCheckDigit`     | Equivalent for AMSID-Core (different weights)                                              |
| `dtcDateTime`         | Parses a 14-char `ccyymmddhhmmss`; returns failed coerce on bad input                      |
| `dtcMidnightHh`       | When sender is HHDC and reading_date_time time portion is `00:00:00`, expects `23:59:59` of same day |
| `uniqueWithinGroup`   | Field values are unique across siblings of the scoped group                                |

Custom predicates are registered at host startup:

```csharp
services.AddSingleton<IPredicate, MyCustomPredicate>();
services.AddSingleton<IPredicateRegistry, PredicateRegistry>();
```

### 6.4 Rule pack location

Rule packs live alongside per-flow schemas. The convention is `rules/D0010_v002.rules.json`. The schema registry merges the rules into the loaded `FlowSchema`'s `Rules` property at load time. If a rule pack is absent, the flow is parsed and structurally validated but no business rules apply.

---

## 7. Parser behaviour — line by line

Pseudocode for `FlatFileParser.Parse`, the heart of the runtime.

```
stack = [root]                     // root is a synthetic ROOT instance

for each line in input:
    if line is empty: continue
    parts = line.split('|')
    code  = parts[0]

    if code in {ZHV, ZHD, ZPT}:
        continue                   // envelope rows handled elsewhere

    if code not in schema.Groups:
        emit Finding(PARSE-001, "unknown group code " + code)
        continue

    groupDef = schema.Groups[code]

    // Pop until the top of the stack is the declared parent
    while stack.size > 1 and stack.top.code != groupDef.parent:
        stack.pop()

    if stack.top.code != groupDef.parent:
        emit Finding(PARSE-002, "group " + code +
                                " expects parent " + groupDef.parent)
        continue

    instance = new GroupInstance(code, lineNumber, parent=stack.top)
    bindFields(instance, groupDef, parts[1..], findings)

    stack.top.children.add(instance)
    stack.push(instance)

return ParseResult(root, findings)
```

### 7.1 Field binding

```
bindFields(instance, groupDef, values, findings):
    for i in 0..groupDef.fields.length:
        field   = groupDef.fields[i]
        raw     = i < values.length ? values[i] : ""

        if raw is empty:
            if field.required:
                emit Finding(SCHEMA-REQ, field + " is required")
            continue

        dataItem = catalogue[field.ref]
        if dataItem is null:
            emit Finding(SCHEMA-UNK, "unknown data item " + field.ref)
            continue

        coerced, error = FieldCoercer.coerce(raw, dataItem)
        if error: emit Finding(SCHEMA-TYPE, error)
        else:     instance.fields[field.name] = coerced
```

### 7.2 Field coercion rules

| DTC type      | Coercion behaviour                                                                                              |
|---------------|------------------------------------------------------------------------------------------------------------------|
| `NUM(n)`      | Parse as `long`; reject non-digits; check length ≤ n; preserve leading zeros by storing as string when needed for keys |
| `CHAR(n)`     | String; reject if length > n; if data item has enum valid set, validate value is in set                          |
| `DATE(n)`     | Parse `ccyymmdd` as `DateOnly`; reject malformed                                                                  |
| `DATETIME(n)` | Parse `ccyymmddhhmmss` as `DateTime`; reject malformed                                                            |
| `BOOLEAN(n)`  | Accept `T` or `F` only; coerce to `bool`                                                                          |
| `INT(n)`      | Parse as `int`; check length ≤ n                                                                                  |
| `±NUM(n,m)`   | Signed decimal; n total digits, m after the decimal point; allow leading +/-                                     |

MPAN/AMSID Core (J0003) is special: even though its logical format is `NUM(13)`, the runtime stores it as a 13-character string. Treating it as a number loses leading zeros and corrupts hashing/keying behaviour downstream.

---

## 8. Schema loading & dependency injection

The bundle is loaded once at startup. `ISchemaRegistry` holds parsed `FlowSchema` and `DataItem` dictionaries in memory; lookups are O(1).

```csharp
public interface ISchemaRegistry
{
    bool TryGet(string flowId, string flowVersion,
                out FlowSchema schema);
    IReadOnlyCollection<ManifestEntry> Manifest { get; }
}

public sealed class FileSchemaRegistry : ISchemaRegistry
{
    public FileSchemaRegistry(string bundleRoot);
    // Loads manifest.json eagerly, individual flow files lazily on first
    // TryGet call. Cached after first load.
}
```

### 8.1 DI registration

```csharp
services.AddDtcParser(opts =>
{
    opts.BundlePath = "schemas/";       // directory containing manifest.json
    opts.LazyLoadFlows = true;          // default: load flow files on demand
    opts.RegisterDefaultPredicates = true;
});

// or with custom predicate
services.AddDtcParser(opts => { opts.BundlePath = "schemas/"; });
services.AddSingleton<IPredicate, MyCustomMpanCheck>();
```

### 8.2 Concurrency

Once loaded, schemas are immutable. `DtcProcessor.Process` is thread-safe and re-entrant — register it as a singleton. The host can call `Process` from multiple threads concurrently with no synchronisation.

---

## 9. Findings — what the runtime returns

Every problem detected is emitted as a `Finding`. Findings are categorised by `RuleId` prefix:

| Prefix       | Category                                                                                  |
|--------------|--------------------------------------------------------------------------------------------|
| `PARSE-`     | Structural problems with the flat file (unknown group code, bad parent)                    |
| `SCHEMA-`    | Schema-driven structural validation (required field missing, type mismatch, cardinality violation) |
| `RULE-`      | Generic rule-pack failures                                                                 |
| `D{NNNN}-`   | Flow-specific rule (e.g. `D0010-001`) — defined in the rule pack                           |

### 9.1 Severity

Three levels: `Info`, `Warning`, `Error`. The host application decides what's blocking; the runtime never aborts on its own beyond fatal envelope or schema-loading failures.

### 9.2 Path encoding

The `Path` field locates the offending element in the parsed tree:

```
mpan_groups[0].mpan_core
mpan_groups[0].meters[1].register_readings[0].validation_result
mpan_groups[2].meters[0].reading_type
```

Index notation uses the group's logical name (lowercased, snake_case) and zero-based sibling index. This is consistent with the post-parse object model and survives JSON serialisation.

---

## 10. Testing strategy

### 10.1 Unit tests

`FieldCoercer`, `EnvelopeReader`, `FlatFileParser`, `RuleEngine`, and each built-in predicate get focused unit tests. Use the schema bundle as a test resource — load it once per test class with `[ClassInitialize]`/static fixture.

### 10.2 Round-trip fixtures

For each significant flow (start with D0010, D0086, D0149, D0150, D0036) commit at least three fixtures:

- `happy.txt` — a file that parses and validates with zero Findings
- `structural.txt` — at least one violation per `SCHEMA-` rule
- `rules.txt` — at least one violation per `Dxxxx-` rule

Tests assert exact `(RuleId, Path)` tuples in expected Findings. Don't assert message strings — they're allowed to evolve.

### 10.3 Schema-bundle smoke test

On every test run, load the entire bundle and assert manifest.json claims match disk: every entry's file exists; every flow file has at least one group with no parent; group code references in parent fields exist.

### 10.4 Performance baseline

Realistic D0010 files are typically 10-100 KB. The runtime should parse and validate a 1 MB file in well under one second on a modern dev box. Add a benchmark test that fails if a representative D0036 file (the largest typical flow) exceeds 2× its baseline.

---

## 11. Worked example — D0010 round-trip

Bring the pieces together with the canonical example. Input file:

```
ZHV|FILE-12345|D0010|002|NHHDA|UDMS|...
026|1234567890121|V|
028|S95A123456|R|
030|01|20260415000000|045231||||T|N|
032||T|
030|02|20260415000000|023987||||T|N|
032||T|
ZPT|6|
```

Phase 1 — envelope detection emits:

```
Envelope { FlowId = "D0010", FlowVersion = "002",
           Sender = "NHHDA", Recipient = "UDMS" }
```

Phase 2 — schema lookup yields `D0010_v002.json`. Parser produces a tree:

```
ROOT
└── 026 MPAN Cores [#1]
    └── 028 Meter/Reading Types [#1]
        ├── 030 Register Readings [#1]   (Day rate, 045231)
        │   └── 032 Validation Result [#1]
        └── 030 Register Readings [#2]   (Night rate, 023987)
            └── 032 Validation Result [#1]
```

Phase 3 — structural validation passes. Rule pack runs:

- `D0010-001` (suspect→reason code) — N/A: flag is T not F
- `D0010-006` (HHDC reading type) — N/A: sender role NHHDA not HHDC
- `D0010-007` (MPAN check digit) — runs against `1234567890121` and emits an error if the check digit is wrong

Final `ProcessingResult.Findings` is empty (or contains only `D0010-007` if the test MPAN's check digit fails — that's the expected case for hand-typed test data).

### 11.1 A deliberately broken file

```
026|1234567890121|X|
028|S95A123456|O|
030|01|20260415000000|045231||||F|N|
032||T|
```

Expected Findings:

- `SCHEMA-ENUM` at `026` J0022 — value `X` not in `[F, U, V]`
- `SCHEMA-ENUM` at `028` J0171 — value `O` is in the catalogue's master enum but flagged via a rule pack entry as not-permitted-in-D0010
- `D0010-001` at `030` — flag `F` but no reason code in `032`
- `D0010-007` at `026` — bad MPAN check digit (depends on the specific MPAN)

---

## 12. Acceptance criteria

The implementation is considered complete when all of the following hold.

### 12.1 Functional

- `DtcProcessor.Process` accepts an arbitrary `Stream` and returns `ProcessingResult` with no exceptions for any well-formed file claiming a flow defined in the bundle.
- Adding a new flow to the bundle (drop a JSON file into `flows/`, update `manifest.json`) is sufficient to enable parsing of that flow without code changes.
- All built-in predicates listed in §6.3 are implemented and registered by default.
- The runtime supports concurrent calls from multiple threads.

### 12.2 Test coverage

- Unit-test coverage of `DtcParser.Core` ≥ 80% line coverage.
- Each public method on `DtcProcessor` has both happy-path and failure-path tests.
- Round-trip fixtures exist for at least 5 flows: D0010, D0086, D0149, D0150, D0036.
- Smoke test loads the full bundle and verifies manifest integrity.

### 12.3 Performance

- A 1 MB D0010 file parses and validates in < 1 second on the build agent.
- A 10 MB D0036 file parses and validates in < 10 seconds on the build agent.

### 12.4 Documentation

- `README.md` at the solution root explains the architecture, the bundle's role, and how to consume the library from a host application.
- Public API has XML doc comments. `dotnet build` with `/p:GenerateDocumentationFile=true` produces no warnings.

### 12.5 Versioning

- The runtime tolerates old and new flow versions side-by-side in the bundle (e.g. `D0010_v001` and `D0010_v002` both load).
- `ProcessingResult.Envelope.FlowVersion` accurately reflects the file's claimed version, not the runtime's preference.

---

## Appendix A — Glossary

| Term         | Definition                                                                                          |
|--------------|------------------------------------------------------------------------------------------------------|
| DTC          | Data Transfer Catalogue — Elexon's specification of inter-participant data flows in the GB electricity market |
| Flow         | A named, structured message type (e.g. D0010 Meter Readings)                                         |
| MPAN         | Meter Point Administration Number — 13-digit national reference for an electricity metering point   |
| AMSID        | Asset Metering System Identifier — analogous to MPAN for asset metering                              |
| NHHDC        | Non Half Hourly Data Collector                                                                       |
| HHDC         | Half Hourly Data Collector                                                                           |
| MOP          | Meter Operator                                                                                       |
| BSC          | Balancing and Settlement Code                                                                        |
| MRA          | Master Registration Agreement                                                                        |
| Group        | A repeatable, hierarchical structural unit within a DTC flow (e.g. 026 MPAN Cores)                   |
| J-reference  | Catalogue reference for a Data Item (e.g. J0003 = MPAN Core)                                         |

## Appendix B — Related deliverables

- `dtc-schema-extractor` — the Python skill that produces the schema bundle this runtime consumes.
- Per-flow rule packs (`rules/Dxxxx_vNNN.rules.json`) — hand-curated. Initial set covers D0010, D0086, D0149, D0150, D0036.
- Sample fixtures under `DtcParser.Core.Tests/Fixtures/` — committed with the test suite.

## Appendix C — Open questions for design review

- Should validation findings include the original raw line text alongside parsed values, for human readability in error reports?
- Is there a concrete need to support streaming parse for files larger than 100 MB? If yes, `FlatFileParser` becomes a forward-only enumerator and the parsed tree is emitted incrementally.
- How is the schema bundle distributed in production — embedded in the assembly, packaged as a NuGet content package, or fetched from a central service at startup?
- Should the runtime emit the parsed tree as JSON (`System.Text.Json`) for downstream consumers that don't want to take a dependency on `DtcParser.Core` types?