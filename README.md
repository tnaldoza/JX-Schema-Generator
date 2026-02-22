# JXSchemaGenerator

A .NET CLI tool that parses JXChange XSD schema files and generates JSON element mapping files.
These files are consumed by API client utilities (`EntitySchemaHelper.ts`, etc.) to resolve which
extended elements (`x_` prefixed) should be included in JXChange SOAP requests, and how to
build modification request bodies.

---

## Requirements

- .NET 10 SDK
- JXChange XSD schema files in a local folder

---

## Commands

### `generate`

Processes master XSD files and writes JSON element mapping files to the output folder.

```txt
dotnet run --project .\src\JXSchemaGenerator.Cli -- generate --input <SchemaFolder> [--root <XsdFile>] [--out <OutputFolder>] [--strict true|false]
```

### `detect`

Analyses a master XSD and prints a config recommendation report with exact tag counts.
Use this when adding a new XSD to determine what config entries are needed.

```txt
dotnet run --project .\src\JXSchemaGenerator.Cli -- detect --input <SchemaFolder> [--root <XsdFile>] [--strict true|false]
```

#### **Arguments**

| Argument  | Required | Default       | Description                                             |
|-----------|----------|---------------|---------------------------------------------------------|
| `--input` | yes      |               | Folder containing all XSD files                         |
| `--root`  | no       |               | Single XSD file to process. Omit to process all masters |
| `--out`   | no       | `.\artifacts` | Output folder for generated JSON (`generate` only)      |
| `--strict`| no       | `true`        | Fail on schema compile errors                           |

#### **Examples**

```txt
# Process all registered master XSDs
dotnet run --project .\src\JXSchemaGenerator.Cli -- generate --input .\schemas --out .\artifacts

# Process a single XSD
dotnet run --project .\src\JXSchemaGenerator.Cli -- generate --input .\schemas --root tpg_depositmaster.xsd --out .\artifacts

# Detect config needed for a new XSD
dotnet run --project .\src\JXSchemaGenerator.Cli -- detect --input .\schemas --root tpg_newmaster.xsd
```

---

## JXChange Operation Type Naming Convention

Every JXChange operation follows a strict naming convention based on the operation type:

| Suffix          | Meaning                         | Example                    |
| ------------    | --------------------------------| -------------------------- |
| `*InqRq_MType`  | Inquiry request envelope        | `AcctInqRq_MType`          |
| `*InqRs_MType`  | Inquiry response envelope       | `AcctInqRs_MType`          |
| `*SrchRq_MType` | Search request envelope         | `XferSrchRq_MType`         |
| `*SrchRs_MType` | Search response envelope        | `XferSrchRs_MType`         |
| `*SrchRec_CType`| Search result record            | `XferSrchRec_CType`        |
| `*ModRq_MType`  | Modification request envelope   | `AcctSweepModRq_MType`     |
| `*ModRs_MType`  | Modification response envelope  | `AcctSweepModRs_MType`     |

### Rq → Rs derivation

The response type for any operation is derived by swapping the `Rq` suffix to `Rs`:

```txt
AcctInqRq_MType   →  strip "Rq_MType"  →  "AcctInq"   →  append "Rs_MType"  →  AcctInqRs_MType
XferSrchRq_MType  →  strip "Rq_MType"  →  "XferSrch"  →  append "Rs_MType"  →  XferSrchRs_MType
```

The element name (the key used in output JSON) is the stripped middle portion:
`AcctInqRq_MType` → element name `AcctInq`.

### Search record derivation

Search response records follow a different pattern — they use `Rec_CType` not `Rs_MType`:

```txt
XferSrchRq_MType  →  strip "Rq_MType"  →  "XferSrch"  →  append "Rec_CType"  →  XferSrchRec_CType
```

---

## Four Operation Families

The generator handles three distinct operation families, each with its own generator and config type.

### Inquiry (`*InqRq_MType`)

**Auto-discovered** — no regex or manual type mapping needed.

- Scans for all `*InqRq_MType` complex types in the XSD
- Derives the element name by stripping `Rq_MType` (e.g. `AcctSweepInqRq_MType` → `AcctSweepInq`)
- Expands the request type (minus `MsgRqHdr`) into `requestSchema`
- Looks up the matching `*InqRs_MType` response type and expands it (minus `MsgRsHdr`) into `responseSchema`
- Patches `IncXtendElemArray.IncXtendElemInfo.XtendElem` in the request schema with the valid `x_*`
  element names collected from the response schema (see [Extended Elements](#extended-elements-x_))
- Output key: `inquiryOperations`

### Search (`*SrchRq_MType`)

**Auto-discovered** — no regex or manual type mapping needed.

- Scans for all `*SrchRq_MType` complex types in the XSD
- Derives the element name by stripping `Rq_MType` (e.g. `XferSrchRq_MType` → `XferSrch`)
- Expands the request type (minus `SrchMsgRqHdr`) into `requestSchema`
- Looks up the matching `*SrchRec_CType` response record type and expands it into `responseRecordSchema`
- Output key: `searchOperations`

### Add (`*AddRq_MType`)

**Auto-discovered** — no regex or manual type mapping needed.

- Scans for all `*AddRq_MType` complex types in the XSD
- Derives the element name by stripping `Rq_MType` (e.g. `CustAddRq_MType` → `CustAdd`)
- Expands the request type (minus `MsgRqHdr`) into `requestSchema`
- Looks up the matching `*AddRs_MType` response type and expands it (minus `MsgRsHdr`) into `responseSchema`
- No `IncXtendElemArray` patching — Add operations do not use the extended element mechanism
- Output key: `addOperations`

> **Note:** `*ValidateRq_MType` operations (e.g. `XferAddValidate`) wrap an Add request as a
> child element and are not separately generated — their field shape is identical to the Add counterpart.

### Modification (`*ModRq_MType`)

**Manually configured** — requires `ContainerTypeRegex`, `TypeToEntry`, and `OutputOrder` in
`ModificationDomainConfig.cs` because operations must be grouped into output files and entity
type keys (e.g. `D`, `L`) with aliases cannot be derived from the XSD alone.

- Scans for `*ModRq_MType` types matching the config regex
- Locates the `*Mod_CType` child (nested pattern) or uses direct children (flat pattern) as sections
- Expands each section into a hierarchical `schema` tree
- Output key: `entityTypes`

---

## Extended Elements (`x_*`)

JXChange uses `x_` prefixed elements as optional data bundles. You only receive the data for
bundles you explicitly request. The mechanism:

1. In the **request**, include `IncXtendElemArray.IncXtendElemInfo.XtendElem` listing the
   `x_*` element names you want
2. In the **response**, those `x_*` elements are populated with their fields

The inquiry generator automatically patches `XtendElem` in the generated `requestSchema` with
every valid `x_*` name found anywhere in the corresponding `responseSchema`. This gives consumers
a ready-made enumeration of what can be requested.

For account inquiry (`AcctInq`), the response schema groups `x_*` elements by account type record:

```txt
responseSchema
  DepAcctInqRec        ← deposit/checking/savings accounts
    x_DepInfoRec
    x_DepAcctInfo
    ...
  LnAcctInqRec         ← loan accounts
    x_LnInfoRec
    x_LnAcctInfo
    ...
  TimeDepAcctInqRec    ← time deposit / CD accounts
    ...
```

For non-account inquiry (`CustInq`), `x_*` elements appear directly in `responseSchema`:

```txt
responseSchema
  x_BusDetail
  x_RegDetail
  x_TaxDetail
  ...
```

---

## Output Files

Each registered XSD produces up to three files — one per generator family.

| Root XSD                      | Inquiry output                    | Add output                        | Search output                    | Modification output                       |
|-------------------------------|-----------------------------------|-----------------------------------|----------------------------------|-------------------------------------------|
| `tpg_inquirymaster.xsd`       | `inquiryElements.json`            | —                                 | `inquirySearchElements.json`     | —                                         |
| `tpg_customermaster.xsd`      | `customerInquiryElements.json`    | `customerAddElements.json`        | `customerSearchElements.json`    | `customerModificationElements.json`       |
| `tpg_depositmaster.xsd`       | `depositInquiryElements.json`     | `depositAddElements.json`         | `depositSearchElements.json`     | `depositModificationElements.json`        |
| `tpg_loanmaster.xsd`          | `loanInquiryElements.json`        | `loanAddElements.json`            | `loanSearchElements.json`        | `loanModificationElements.json`           |
| `tpg_achmaster.xsd`           | `achInquiryElements.json`         | `achAddElements.json`             | `achSearchElements.json`         | `achModificationElements.json`            |
| `tpg_billpaymaster.xsd`       | `billPayInquiryElements.json`     | `billPayAddElements.json`         | `billPaySearchElements.json`     | `billPayModificationElements.json`        |
| `tpg_crcardmaster.xsd`        | `crCardInquiryElements.json`      | `crCardAddElements.json`          | `crCardSearchElements.json`      | `crCardModificationElements.json`         |
| `tpg_ensmaster.xsd`           | `ensInquiryElements.json`         | `ensAddElements.json`             | `ensSearchElements.json`         | `ensModificationElements.json`            |
| `tpg_imagemaster.xsd`         | `imageInquiryElements.json`       | `imageAddElements.json`           | `imageSearchElements.json`       | `imageModificationElements.json`          |
| `tpg_imsmaster.xsd`           | `imsInquiryElements.json`         | `imsAddElements.json`             | `imsSearchElements.json`         | `imsModificationElements.json`            |
| `tpg_tellermaster.xsd`        | `tellerInquiryElements.json`      | `tellerAddElements.json`          | `tellerSearchElements.json`      | `tellerModificationElements.json`         |
| `tpg_wiremaster.xsd`          | `wireInquiryElements.json`        | `wireAddElements.json`            | `wireSearchElements.json`        | `wireModificationElements.json`           |
| `tpg_custommaster.xsd`        | `customInquiryElements.json`      | `customAddElements.json`          | `customSearchElements.json`      | `customModificationElements.json`         |
| `tpg_transactionmaster.xsd`   | —                                 | `transactionAddElements.json`     | —                                | `transactionModificationElements.json`    |
| `tpg_brdcstmaster.xsd`        | —                                 | —                                 | `brdCstSearchElements.json`      | —                                         |
| `tpg_workflowmaster.xsd`      | —                                 | —                                 | `workflowSearchElements.json`    | —                                         |

---

## Output Formats

### Inquiry file

```json
{
  "version": "1.0.0",
  "lastUpdated": "2026-02-22",
  "inquiryOperations": {
    "AcctInq": {
      "name": "AcctInq",
      "requestSchema": {
        "InAcctId": { "AcctId": null, "AcctType": null },
        "IncXtendElemArray": {
          "IncXtendElemInfo": {
            "XtendElem": ["x_DepAcctInfo", "x_DepInfoRec", "x_LnInfoRec", "..."]
          }
        }
      },
      "responseSchema": {
        "DepAcctInqRec": {
          "x_DepInfoRec": { "AcctId": null, "AcctStat": null, "..." : null },
          "x_DepAcctInfo": { "..." : null }
        },
        "LnAcctInqRec": {
          "x_LnInfoRec": { "AcctId": null, "..." : null }
        }
      }
    },
    "CustInq": {
      "name": "CustInq",
      "requestSchema": { "..." : null },
      "responseSchema": {
        "x_BusDetail": { "OffCode": null, "..." : null },
        "x_RegDetail": { "DoNotCallCode": null, "..." : null }
      }
    }
  }
}
```

### Search file

```json
{
  "version": "1.0.0",
  "lastUpdated": "2026-02-22",
  "searchOperations": {
    "XferSrch": {
      "name": "XferSrch",
      "requestSchema": {
        "AcctId": null,
        "AcctType": null,
        "SrchMsgRqHdr": null
      },
      "responseRecordSchema": {
        "XferSrchRec": {
          "AcctId": null,
          "XferAmt": null,
          "XferDt": null
        }
      }
    }
  }
}
```

### Modification file

```json
{
  "version": "1.0.0",
  "lastUpdated": "2026-02-22",
  "entityTypes": {
    "Cust": {
      "name": "Customer",
      "aliases": [],
      "modificationStructure": "",
      "modificationSections": [
        {
          "name": "CustDetail",
          "schema": {
            "EmailArray": {
              "isArray": true,
              "EmailInfo": {
                "EmailAddr": null,
                "EmailType": null
              }
            },
            "DoNotCallCode": null
          }
        }
      ]
    },
    "D": {
      "name": "Deposit/Checking/Savings",
      "aliases": ["S", "X"],
      "modificationStructure": "DepMod",
      "modificationSections": [
        {
          "name": "DepInfoRec",
          "schema": { "AcctTitle": null, "..." : null }
        }
      ]
    }
  }
}
```

The `isArray: true` marker on a node indicates that its parent element is an XML array wrapper
(e.g. `EmailArray`). `ModificationBuilder` uses this to correctly build repeated items.

---

## Config Files

### `OperationConfig.cs` — Inquiry and Search

Both inquiry and search generators are **fully auto-discovering**. The only config needed is
the output file name, registered in two static classes:

```csharp
// InquiryConfigs — maps XSD file name -> inquiry output file name
// SearchConfigs  — maps XSD file name -> search output file name
public static class InquiryConfigs
{
    public static OperationConfig? FromRootXsd(string rootXsd) =>
        Path.GetFileName(rootXsd).ToLowerInvariant() switch
        {
            "tpg_depositmaster.xsd" => new() { OutputFileName = "depositInquiryElements.json" },
            // ...
            _ => null,
        };
}
```

### `ModificationDomainConfig.cs` — Modification

Modification configs require three extra properties because entity type keys and grouping
cannot be derived from the XSD alone:

| Property            | Purpose                                                                                 |
|---------------------|-----------------------------------------------------------------------------------------|
| `OutputFileName`    | Output JSON file name                                                                   |
| `ContainerTypeRegex`| Regex applied to each complex type name; must capture named group `key`                 |
| `TypeToEntry`       | Maps the `key` capture to a display name, output key, and aliases                       |
| `OutputOrder`       | Controls the key order in the emitted JSON                                              |

```csharp
public static readonly ModificationDomainConfig Deposit = new()
{
    OutputFileName     = "depositModificationElements.json",
    ContainerTypeRegex = new Regex(
        @"^(?<key>[A-Za-z]+)AcctModRq_MType$|^(?<key>AcctSweep|TaxPln|...)ModRq_MType$",
        RegexOptions.Compiled),
    OutputOrder = ["D", "T", "B", "C", "AcctSweep", "TaxPln", "..."],
    TypeToEntry = key => key switch
    {
        "Dep"      => new("D", "Deposit/Checking/Savings", ["S", "X"]),
        "TimeDep"  => new("T", "Time Deposit/CD",          []),
        "SafeDep"  => new("B", "Safe Deposit Box",         []),
        "Trck"     => new("C", "Collection/Trust",         []),
        "AcctSweep"=> new("AcctSweep", "Account Sweep",    []),
        _          => null,  // unknown key = skip silently
    },
};
```

---

## Adding a New XSD

### Step 1 — Run `detect`

```txt
dotnet run --project .\src\JXSchemaGenerator.Cli -- detect --input .\schemas --root tpg_newmaster.xsd
```

The report shows:

- How many `*InqRs_MType` response types with `x_*` elements were found (inquiry)
- Whether those types are already registered in `InquiryConfigs`
- How many `*ModRq_MType` request types with modification sections were found
- Whether those types are already registered in `ModificationDomainConfigs`
- Exact tag counts per section from the real expansion pipeline
- A copy-paste config snippet for modification (if needed)

### Step 2 — Register inquiry and/or search (if the XSD has them)

Add an entry to `InquiryConfigs.FromRootXsd` and/or `SrchOpConfigs.FromRootXsd` in
`OperationConfig.cs`. Only the output file name is needed — operations are auto-discovered.

```csharp
"tpg_newmaster.xsd" => new() { OutputFileName = "newInquiryElements.json" },
```

### Step 3 — Add a modification config (if the XSD has `*ModRq_MType` types)

Use the config snippet printed by `detect` as a starting point. Add the static config entry
to `ModificationDomainConfigs` in `ModificationDomainConfig.cs`, then register it in
`ModificationDomainConfigs.FromRootXsd`.

### Step 4 — Run and verify

```txt
dotnet run --project .\src\JXSchemaGenerator.Cli -- generate --input .\schemas --root tpg_newmaster.xsd --out .\artifacts
```

The summary at the end reports any sections that resolved zero tags. If any are listed,
run the detect command again and inspect the specific type name — it usually indicates a
namespace mismatch or a type that is intentionally empty in the XSD.

---

## How Schema Expansion Works

For each element found in a container type, the tool attempts to resolve its complex type
using three strategies in order:

| Strategy | Description |
| ---------- | ------------- |
| 1 | `ElementSchemaType` — the .NET XSD compiler resolved the type reference directly on the element after compilation. |
| 2 | `SchemaTypeName` with namespace backfill — local type references in JXChange XSDs often have an empty namespace after compilation. The enclosing type's namespace is filled in and the index lookup is retried. |
| 3 | Naming convention — appends `_CType` then `_AType` to the element name and tries both. Example: `x_DepInfoRec` → `x_DepInfoRec_CType`. |

Once a type resolves, it is expanded recursively into a hierarchical `SchemaNode` tree.
Both container element names and their leaf children are included because the JXChange API
expects the full path from the envelope element down to the scalar field.

The `isArray: true` marker is set on any node whose XSD type is wrapped in a `maxOccurs="unbounded"`
sequence — this tells `ModificationBuilder` to treat those nodes as array containers.

---

## Project Structure

```txt
JXSchemaGenerator.slnx
src/
  JXSchemaGenerator.Cli/
    Program.cs                        CLI entry point; dispatches generate / detect commands
  JXSchemaGenerator.Core/
    Emit/
      JsonEmitter.cs                  JSON serialization helper
    Expand/
      ExpansionOptions.cs             Controls tag expansion behaviour (leaf-only, skip-any, etc.)
      TypeExpander.cs                 Recursively expands XSD complex types into schema trees
    Generate/
      OperationConfig.cs              InquiryConfigs + SrchOpConfigs registries (add new XSDs here)
      ModificationDomainConfig.cs     Modification domain configs (add new here)
      InquiryGenerator.cs             Auto-discovers *InqRq_MType, emits inquiryOperations
      AddGenerator.cs                 Auto-discovers *AddRq_MType, emits addOperations
      SearchGenerator.cs              Auto-discovers *SrchRq_MType, emits searchOperations
      ModificationGenerator.cs        Regex-driven modification section expander
      DomainDetector.cs               Analyses an XSD index and produces a DetectionReport
      DetectionReportPrinter.cs       Renders a DetectionReport to the console
      Models/
        InquiryModels.cs              Output model for inquiry files
        AddModels.cs                  Output model for add files
        SearchModels.cs               Output model for search files
        ModificationModel.cs          Output model for modification files
        DetectionModels.cs            Internal models for the detect command
    Schema/
      SchemaIndex.cs                  In-memory index of all compiled XSD complex types
      SchemaLoader.cs                 Loads and compiles XSD files into XmlSchemaSet
      LocalFolderXmlResolver.cs       Resolves XSD xs:include / xs:import from a local folder
tests/
  JXSchemaGenerator.Tests/
    UnitTest1.cs
```

---

## TODO

### Validate operations

`*ValidateRq_MType` operations (31 total across all XSDs, e.g. `XferAddValidateRq_MType`) are not
yet generated. Unlike Add operations, Validate requests wrap an Add request as a child element
rather than containing raw fields directly. A `ValidateGenerator` would need to handle this
nesting and produce a `validateOperations` output section.
