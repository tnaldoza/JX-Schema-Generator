# JXSchemaGenerator

A .NET CLI tool that parses JXChange XSD schema files and generates JSON element mapping files.
These files are consumed by API client utilities (InquiryElements.js, etc.) to resolve which
extended elements (x_ prefixed) and their tags should be included in JXChange API requests.

---

## Requirements

- .NET 10 SDK
- JXChange XSD schema files in a local folder

---

## Usage

```
dotnet run --project .\src\JXSchemaGenerator.Cli -- --input <SchemaFolder> [--root <RootXsdFile>] [--out <OutputFolder>] [--strict true|false]
```

**Arguments**

| Argument  | Required | Default     | Description                                              |
|-----------|----------|-------------|----------------------------------------------------------|
| --input   | yes      |             | Folder containing all XSD files                          |
| --root    | no       |             | Single XSD file to process. Omit to process all masters  |
| --out     | no       | .\artifacts | Output folder for generated JSON                         |
| --strict  | no       | true        | Fail on schema compile errors                            |

**Process all master XSDs automatically**

```
dotnet run --project .\src\JXSchemaGenerator.Cli -- --input .\schemas --out .\artifacts
```

**Process a single XSD**

```
dotnet run --project .\src\JXSchemaGenerator.Cli -- --input .\schemas --root TPG_InquiryMaster.xsd --out .\artifacts
```

---

## Output

All output files share the same top-level structure and use `entityTypes` as the
uniform collection key regardless of domain.

| Root XSD                  | Output File(s)                                              |
|---------------------------|-------------------------------------------------------------|
| TPG_InquiryMaster.xsd     | inquiryElements.json                                        |
| TPG_CustomerMaster.xsd    | customerElements.json, customerModificationElements.json    |
| TPG_DepositMaster.xsd     | depositModificationElements.json                            |
| TPG_LoanMaster.xsd        | loanModificationElements.json                               |

```json
{
  "version": "1.0.0",
  "lastUpdated": "2026-02-21",
  "entityTypes": {
    "D": {
      "name": "Deposit/Checking/Savings",
      "aliases": ["S", "X"],
      "extendedElements": [
        {
          "name": "x_DepInfoRec",
          "description": null,
          "tags": ["AcctId", "AcctStat", "BrCode", "..."]
        }
      ]
    }
  }
}
```

---

## Supported Domains

### Inquiry (TPG_InquiryMaster.xsd)

| Key | Name                     | Aliases |
|-----|--------------------------|---------|
| D   | Deposit/Checking/Savings | S, X    |
| T   | Time Deposit/CD          |         |
| L   | Loan                     | O       |
| B   | Safe Deposit Box         |         |
| C   | Collection/Trust         |         |

### Customer (TPG_CustomerMaster.xsd)

| Key  | Name     | Aliases |
|------|----------|---------|
| Cust | Customer |         |

### Deposit Modification (TPG_DepositMaster.xsd)

| Key | Name                     | Aliases |
|-----|--------------------------|---------|
| D   | Deposit/Checking/Savings | S, X    |
| T   | Time Deposit/CD          |         |
| B   | Safe Deposit Box         |         |
| C   | Collection/Trust         |         |

### Loan Modification (TPG_LoanMaster.xsd)

| Key | Name | Aliases |
|-----|------|---------|
| L   | Loan | O       |

---

## Adding a New Domain

### Step 1 - Identify the XSD pattern

Run these three commands against the target XSD. The output determines which
config type to use and what values to supply.

**Command 1 - Find which complex types own x_ elements**

```powershell
Select-String -Path .\schemas\TPG_TargetMaster.xsd -Pattern '"x_[A-Za-z]' |
    ForEach-Object {
        $lines = Get-Content $_.Path
        $lines[0..($_.LineNumber-2)] | Select-String 'complexType name=' | Select-Object -Last 1
    } | Sort-Object -Unique
```

Look at the type names returned. They tell you the container shape.

**Command 2 - Find all top-level response and request container types**

```powershell
Select-String -Path .\schemas\TPG_TargetMaster.xsd -Pattern 'complexType name="[A-Za-z]+(Rq|Rs)_MType"' |
    Select-Object -First 20
```

Use this to confirm whether the file is inquiry-side (Rs), modification-side (Rq), or both.

**Command 3 - Inspect a specific container type in full**

```powershell
$typeName = "DepAcctModRq_MType"
Select-String -Path .\schemas\TPG_TargetMaster.xsd -Pattern "complexType name=""$typeName""" |
    ForEach-Object { Get-Content $_.Path | Select-Object -Skip ($_.LineNumber - 1) -First 50 }
```

Replace the type name with one found in Commands 1 or 2.

---

### Step 2 - Determine the config type

Use the table below to classify the XSD based on what Commands 1 and 2 returned.

| What you see in the output       | Config type to use        | Class to add entry to      |
|----------------------------------|---------------------------|----------------------------|
| `*AcctInqRec_CType` containers   | Inquiry / XElement config | `DomainConfigs`            |
| Single `*InqRs_MType` container  | Inquiry / XElement config | `DomainConfigs`            |
| `*AcctModRq_MType` containers    | Modification config       | `ModificationDomainConfigs`|
| Single `*ModRq_MType` container  | Modification config       | `ModificationDomainConfigs`|
| Only Add/Ext/Ren/Pmt types       | No config needed          | n/a                        |
| Mix of Rq and Rs with x_         | Both config types         | Both classes               |

---

### Step 3 - Determine the regex pattern

Use the table below to choose the right regex based on the container type names found.

| Container type name pattern                  | Regex to use                                    | Notes                          |
|----------------------------------------------|-------------------------------------------------|--------------------------------|
| `DepAcctInqRec_CType`, `LnAcctInqRec_CType`  | `^(?<key>[A-Za-z]+)AcctInqRec_CType# JXSchemaGenerator

A .NET CLI tool that parses JXChange XSD schema files and generates JSON element mapping files.
These files are consumed by API client utilities (InquiryElements.js, etc.) to resolve which
extended elements (x_ prefixed) and their tags should be included in JXChange API requests.

---

## Requirements

- .NET 10 SDK
- JXChange XSD schema files in a local folder

---

## Usage

```
dotnet run --project .\src\JXSchemaGenerator.Cli -- --input <SchemaFolder> [--root <RootXsdFile>] [--out <OutputFolder>] [--strict true|false]
```

**Arguments**

| Argument  | Required | Default     | Description                                              |
|-----------|----------|-------------|----------------------------------------------------------|
| --input   | yes      |             | Folder containing all XSD files                          |
| --root    | no       |             | Single XSD file to process. Omit to process all masters  |
| --out     | no       | .\artifacts | Output folder for generated JSON                         |
| --strict  | no       | true        | Fail on schema compile errors                            |

**Process all master XSDs automatically**

```
dotnet run --project .\src\JXSchemaGenerator.Cli -- --input .\schemas --out .\artifacts
```

**Process a single XSD**

```
dotnet run --project .\src\JXSchemaGenerator.Cli -- --input .\schemas --root TPG_InquiryMaster.xsd --out .\artifacts
```

---

## Output

All output files share the same top-level structure and use `entityTypes` as the
uniform collection key regardless of domain.

| Root XSD                  | Output File(s)                                              |
|---------------------------|-------------------------------------------------------------|
| TPG_InquiryMaster.xsd     | inquiryElements.json                                        |
| TPG_CustomerMaster.xsd    | customerElements.json, customerModificationElements.json    |
| TPG_DepositMaster.xsd     | depositModificationElements.json                            |
| TPG_LoanMaster.xsd        | loanModificationElements.json                               |

```json
{
  "version": "1.0.0",
  "lastUpdated": "2026-02-21",
  "entityTypes": {
    "D": {
      "name": "Deposit/Checking/Savings",
      "aliases": ["S", "X"],
      "extendedElements": [
        {
          "name": "x_DepInfoRec",
          "description": null,
          "tags": ["AcctId", "AcctStat", "BrCode", "..."]
        }
      ]
    }
  }
}
```

---

## Supported Domains

### Inquiry (TPG_InquiryMaster.xsd)

| Key | Name                     | Aliases |
|-----|--------------------------|---------|
| D   | Deposit/Checking/Savings | S, X    |
| T   | Time Deposit/CD          |         |
| L   | Loan                     | O       |
| B   | Safe Deposit Box         |         |
| C   | Collection/Trust         |         |

### Customer (TPG_CustomerMaster.xsd)

| Key  | Name     | Aliases |
|------|----------|---------|
| Cust | Customer |         |

### Deposit Modification (TPG_DepositMaster.xsd)

| Key | Name                     | Aliases |
|-----|--------------------------|---------|
| D   | Deposit/Checking/Savings | S, X    |
| T   | Time Deposit/CD          |         |
| B   | Safe Deposit Box         |         |
| C   | Collection/Trust         |         |

### Loan Modification (TPG_LoanMaster.xsd)

| Key | Name | Aliases |
|-----|------|---------|
| L   | Loan | O       |

---

## Adding a New Domain

### Step 1 - Identify the XSD pattern

Run these three commands against the target XSD. The output determines which
config type to use and what values to supply.

**Command 1 - Find which complex types own x_ elements**

```powershell
Select-String -Path .\schemas\TPG_TargetMaster.xsd -Pattern '"x_[A-Za-z]' |
    ForEach-Object {
        $lines = Get-Content $_.Path
        $lines[0..($_.LineNumber-2)] | Select-String 'complexType name=' | Select-Object -Last 1
    } | Sort-Object -Unique
```

Look at the type names returned. They tell you the container shape.

**Command 2 - Find all top-level response and request container types**

```powershell
Select-String -Path .\schemas\TPG_TargetMaster.xsd -Pattern 'complexType name="[A-Za-z]+(Rq|Rs)_MType"' |
    Select-Object -First 20
```

Use this to confirm whether the file is inquiry-side (Rs), modification-side (Rq), or both.

**Command 3 - Inspect a specific container type in full**

```powershell
$typeName = "DepAcctModRq_MType"
Select-String -Path .\schemas\TPG_TargetMaster.xsd -Pattern "complexType name=""$typeName""" |
    ForEach-Object { Get-Content $_.Path | Select-Object -Skip ($_.LineNumber - 1) -First 50 }
```

Replace the type name with one found in Commands 1 or 2.

---

### Step 2 - Determine the config type

Use the table below to classify the XSD based on what Commands 1 and 2 returned.

| What you see in the output       | Config type to use        | Class to add entry to      |
|----------------------------------|---------------------------|----------------------------|
| `*AcctInqRec_CType` containers   | Inquiry / XElement config | `DomainConfigs`            |
| Single `*InqRs_MType` container  | Inquiry / XElement config | `DomainConfigs`            |
| `*AcctModRq_MType` containers    | Modification config       | `ModificationDomainConfigs`|
| Single `*ModRq_MType` container  | Modification config       | `ModificationDomainConfigs`|
| Only Add/Ext/Ren/Pmt types       | No config needed          | n/a                        |
| Mix of Rq and Rs with x_         | Both config types         | Both classes               |

---

          | Multiple prefixes, one regex   |
| `CustInqRs_MType`                            | `^(?<key>Cust)InqRs_MType# JXSchemaGenerator

A .NET CLI tool that parses JXChange XSD schema files and generates JSON element mapping files.
These files are consumed by API client utilities (InquiryElements.js, etc.) to resolve which
extended elements (x_ prefixed) and their tags should be included in JXChange API requests.

---

## Requirements

- .NET 10 SDK
- JXChange XSD schema files in a local folder

---

## Usage

```
dotnet run --project .\src\JXSchemaGenerator.Cli -- --input <SchemaFolder> [--root <RootXsdFile>] [--out <OutputFolder>] [--strict true|false]
```

**Arguments**

| Argument  | Required | Default     | Description                                              |
|-----------|----------|-------------|----------------------------------------------------------|
| --input   | yes      |             | Folder containing all XSD files                          |
| --root    | no       |             | Single XSD file to process. Omit to process all masters  |
| --out     | no       | .\artifacts | Output folder for generated JSON                         |
| --strict  | no       | true        | Fail on schema compile errors                            |

**Process all master XSDs automatically**

```
dotnet run --project .\src\JXSchemaGenerator.Cli -- --input .\schemas --out .\artifacts
```

**Process a single XSD**

```
dotnet run --project .\src\JXSchemaGenerator.Cli -- --input .\schemas --root TPG_InquiryMaster.xsd --out .\artifacts
```

---

## Output

All output files share the same top-level structure and use `entityTypes` as the
uniform collection key regardless of domain.

| Root XSD                  | Output File(s)                                              |
|---------------------------|-------------------------------------------------------------|
| TPG_InquiryMaster.xsd     | inquiryElements.json                                        |
| TPG_CustomerMaster.xsd    | customerElements.json, customerModificationElements.json    |
| TPG_DepositMaster.xsd     | depositModificationElements.json                            |
| TPG_LoanMaster.xsd        | loanModificationElements.json                               |

```json
{
  "version": "1.0.0",
  "lastUpdated": "2026-02-21",
  "entityTypes": {
    "D": {
      "name": "Deposit/Checking/Savings",
      "aliases": ["S", "X"],
      "extendedElements": [
        {
          "name": "x_DepInfoRec",
          "description": null,
          "tags": ["AcctId", "AcctStat", "BrCode", "..."]
        }
      ]
    }
  }
}
```

---

## Supported Domains

### Inquiry (TPG_InquiryMaster.xsd)

| Key | Name                     | Aliases |
|-----|--------------------------|---------|
| D   | Deposit/Checking/Savings | S, X    |
| T   | Time Deposit/CD          |         |
| L   | Loan                     | O       |
| B   | Safe Deposit Box         |         |
| C   | Collection/Trust         |         |

### Customer (TPG_CustomerMaster.xsd)

| Key  | Name     | Aliases |
|------|----------|---------|
| Cust | Customer |         |

### Deposit Modification (TPG_DepositMaster.xsd)

| Key | Name                     | Aliases |
|-----|--------------------------|---------|
| D   | Deposit/Checking/Savings | S, X    |
| T   | Time Deposit/CD          |         |
| B   | Safe Deposit Box         |         |
| C   | Collection/Trust         |         |

### Loan Modification (TPG_LoanMaster.xsd)

| Key | Name | Aliases |
|-----|------|---------|
| L   | Loan | O       |

---

## Adding a New Domain

### Step 1 - Identify the XSD pattern

Run these three commands against the target XSD. The output determines which
config type to use and what values to supply.

**Command 1 - Find which complex types own x_ elements**

```powershell
Select-String -Path .\schemas\TPG_TargetMaster.xsd -Pattern '"x_[A-Za-z]' |
    ForEach-Object {
        $lines = Get-Content $_.Path
        $lines[0..($_.LineNumber-2)] | Select-String 'complexType name=' | Select-Object -Last 1
    } | Sort-Object -Unique
```

Look at the type names returned. They tell you the container shape.

**Command 2 - Find all top-level response and request container types**

```powershell
Select-String -Path .\schemas\TPG_TargetMaster.xsd -Pattern 'complexType name="[A-Za-z]+(Rq|Rs)_MType"' |
    Select-Object -First 20
```

Use this to confirm whether the file is inquiry-side (Rs), modification-side (Rq), or both.

**Command 3 - Inspect a specific container type in full**

```powershell
$typeName = "DepAcctModRq_MType"
Select-String -Path .\schemas\TPG_TargetMaster.xsd -Pattern "complexType name=""$typeName""" |
    ForEach-Object { Get-Content $_.Path | Select-Object -Skip ($_.LineNumber - 1) -First 50 }
```

Replace the type name with one found in Commands 1 or 2.

---

### Step 2 - Determine the config type

Use the table below to classify the XSD based on what Commands 1 and 2 returned.

| What you see in the output       | Config type to use        | Class to add entry to      |
|----------------------------------|---------------------------|----------------------------|
| `*AcctInqRec_CType` containers   | Inquiry / XElement config | `DomainConfigs`            |
| Single `*InqRs_MType` container  | Inquiry / XElement config | `DomainConfigs`            |
| `*AcctModRq_MType` containers    | Modification config       | `ModificationDomainConfigs`|
| Single `*ModRq_MType` container  | Modification config       | `ModificationDomainConfigs`|
| Only Add/Ext/Ren/Pmt types       | No config needed          | n/a                        |
| Mix of Rq and Rs with x_         | Both config types         | Both classes               |

---

                    | Single flat container          |
| `WireInqRs_MType`                            | `^(?<key>Wire)InqRs_MType# JXSchemaGenerator

A .NET CLI tool that parses JXChange XSD schema files and generates JSON element mapping files.
These files are consumed by API client utilities (InquiryElements.js, etc.) to resolve which
extended elements (x_ prefixed) and their tags should be included in JXChange API requests.

---

## Requirements

- .NET 10 SDK
- JXChange XSD schema files in a local folder

---

## Usage

```
dotnet run --project .\src\JXSchemaGenerator.Cli -- --input <SchemaFolder> [--root <RootXsdFile>] [--out <OutputFolder>] [--strict true|false]
```

**Arguments**

| Argument  | Required | Default     | Description                                              |
|-----------|----------|-------------|----------------------------------------------------------|
| --input   | yes      |             | Folder containing all XSD files                          |
| --root    | no       |             | Single XSD file to process. Omit to process all masters  |
| --out     | no       | .\artifacts | Output folder for generated JSON                         |
| --strict  | no       | true        | Fail on schema compile errors                            |

**Process all master XSDs automatically**

```
dotnet run --project .\src\JXSchemaGenerator.Cli -- --input .\schemas --out .\artifacts
```

**Process a single XSD**

```
dotnet run --project .\src\JXSchemaGenerator.Cli -- --input .\schemas --root TPG_InquiryMaster.xsd --out .\artifacts
```

---

## Output

All output files share the same top-level structure and use `entityTypes` as the
uniform collection key regardless of domain.

| Root XSD                  | Output File(s)                                              |
|---------------------------|-------------------------------------------------------------|
| TPG_InquiryMaster.xsd     | inquiryElements.json                                        |
| TPG_CustomerMaster.xsd    | customerElements.json, customerModificationElements.json    |
| TPG_DepositMaster.xsd     | depositModificationElements.json                            |
| TPG_LoanMaster.xsd        | loanModificationElements.json                               |

```json
{
  "version": "1.0.0",
  "lastUpdated": "2026-02-21",
  "entityTypes": {
    "D": {
      "name": "Deposit/Checking/Savings",
      "aliases": ["S", "X"],
      "extendedElements": [
        {
          "name": "x_DepInfoRec",
          "description": null,
          "tags": ["AcctId", "AcctStat", "BrCode", "..."]
        }
      ]
    }
  }
}
```

---

## Supported Domains

### Inquiry (TPG_InquiryMaster.xsd)

| Key | Name                     | Aliases |
|-----|--------------------------|---------|
| D   | Deposit/Checking/Savings | S, X    |
| T   | Time Deposit/CD          |         |
| L   | Loan                     | O       |
| B   | Safe Deposit Box         |         |
| C   | Collection/Trust         |         |

### Customer (TPG_CustomerMaster.xsd)

| Key  | Name     | Aliases |
|------|----------|---------|
| Cust | Customer |         |

### Deposit Modification (TPG_DepositMaster.xsd)

| Key | Name                     | Aliases |
|-----|--------------------------|---------|
| D   | Deposit/Checking/Savings | S, X    |
| T   | Time Deposit/CD          |         |
| B   | Safe Deposit Box         |         |
| C   | Collection/Trust         |         |

### Loan Modification (TPG_LoanMaster.xsd)

| Key | Name | Aliases |
|-----|------|---------|
| L   | Loan | O       |

---

## Adding a New Domain

### Step 1 - Identify the XSD pattern

Run these three commands against the target XSD. The output determines which
config type to use and what values to supply.

**Command 1 - Find which complex types own x_ elements**

```powershell
Select-String -Path .\schemas\TPG_TargetMaster.xsd -Pattern '"x_[A-Za-z]' |
    ForEach-Object {
        $lines = Get-Content $_.Path
        $lines[0..($_.LineNumber-2)] | Select-String 'complexType name=' | Select-Object -Last 1
    } | Sort-Object -Unique
```

Look at the type names returned. They tell you the container shape.

**Command 2 - Find all top-level response and request container types**

```powershell
Select-String -Path .\schemas\TPG_TargetMaster.xsd -Pattern 'complexType name="[A-Za-z]+(Rq|Rs)_MType"' |
    Select-Object -First 20
```

Use this to confirm whether the file is inquiry-side (Rs), modification-side (Rq), or both.

**Command 3 - Inspect a specific container type in full**

```powershell
$typeName = "DepAcctModRq_MType"
Select-String -Path .\schemas\TPG_TargetMaster.xsd -Pattern "complexType name=""$typeName""" |
    ForEach-Object { Get-Content $_.Path | Select-Object -Skip ($_.LineNumber - 1) -First 50 }
```

Replace the type name with one found in Commands 1 or 2.

---

### Step 2 - Determine the config type

Use the table below to classify the XSD based on what Commands 1 and 2 returned.

| What you see in the output       | Config type to use        | Class to add entry to      |
|----------------------------------|---------------------------|----------------------------|
| `*AcctInqRec_CType` containers   | Inquiry / XElement config | `DomainConfigs`            |
| Single `*InqRs_MType` container  | Inquiry / XElement config | `DomainConfigs`            |
| `*AcctModRq_MType` containers    | Modification config       | `ModificationDomainConfigs`|
| Single `*ModRq_MType` container  | Modification config       | `ModificationDomainConfigs`|
| Only Add/Ext/Ren/Pmt types       | No config needed          | n/a                        |
| Mix of Rq and Rs with x_         | Both config types         | Both classes               |

---

                    | Single flat container          |
| `DepAcctModRq_MType`, `LnAcctModRq_MType`    | `^(?<key>[A-Za-z]+)AcctModRq_MType# JXSchemaGenerator

A .NET CLI tool that parses JXChange XSD schema files and generates JSON element mapping files.
These files are consumed by API client utilities (InquiryElements.js, etc.) to resolve which
extended elements (x_ prefixed) and their tags should be included in JXChange API requests.

---

## Requirements

- .NET 10 SDK
- JXChange XSD schema files in a local folder

---

## Usage

```
dotnet run --project .\src\JXSchemaGenerator.Cli -- --input <SchemaFolder> [--root <RootXsdFile>] [--out <OutputFolder>] [--strict true|false]
```

**Arguments**

| Argument  | Required | Default     | Description                                              |
|-----------|----------|-------------|----------------------------------------------------------|
| --input   | yes      |             | Folder containing all XSD files                          |
| --root    | no       |             | Single XSD file to process. Omit to process all masters  |
| --out     | no       | .\artifacts | Output folder for generated JSON                         |
| --strict  | no       | true        | Fail on schema compile errors                            |

**Process all master XSDs automatically**

```
dotnet run --project .\src\JXSchemaGenerator.Cli -- --input .\schemas --out .\artifacts
```

**Process a single XSD**

```
dotnet run --project .\src\JXSchemaGenerator.Cli -- --input .\schemas --root TPG_InquiryMaster.xsd --out .\artifacts
```

---

## Output

All output files share the same top-level structure and use `entityTypes` as the
uniform collection key regardless of domain.

| Root XSD                  | Output File(s)                                              |
|---------------------------|-------------------------------------------------------------|
| TPG_InquiryMaster.xsd     | inquiryElements.json                                        |
| TPG_CustomerMaster.xsd    | customerElements.json, customerModificationElements.json    |
| TPG_DepositMaster.xsd     | depositModificationElements.json                            |
| TPG_LoanMaster.xsd        | loanModificationElements.json                               |

```json
{
  "version": "1.0.0",
  "lastUpdated": "2026-02-21",
  "entityTypes": {
    "D": {
      "name": "Deposit/Checking/Savings",
      "aliases": ["S", "X"],
      "extendedElements": [
        {
          "name": "x_DepInfoRec",
          "description": null,
          "tags": ["AcctId", "AcctStat", "BrCode", "..."]
        }
      ]
    }
  }
}
```

---

## Supported Domains

### Inquiry (TPG_InquiryMaster.xsd)

| Key | Name                     | Aliases |
|-----|--------------------------|---------|
| D   | Deposit/Checking/Savings | S, X    |
| T   | Time Deposit/CD          |         |
| L   | Loan                     | O       |
| B   | Safe Deposit Box         |         |
| C   | Collection/Trust         |         |

### Customer (TPG_CustomerMaster.xsd)

| Key  | Name     | Aliases |
|------|----------|---------|
| Cust | Customer |         |

### Deposit Modification (TPG_DepositMaster.xsd)

| Key | Name                     | Aliases |
|-----|--------------------------|---------|
| D   | Deposit/Checking/Savings | S, X    |
| T   | Time Deposit/CD          |         |
| B   | Safe Deposit Box         |         |
| C   | Collection/Trust         |         |

### Loan Modification (TPG_LoanMaster.xsd)

| Key | Name | Aliases |
|-----|------|---------|
| L   | Loan | O       |

---

## Adding a New Domain

### Step 1 - Identify the XSD pattern

Run these three commands against the target XSD. The output determines which
config type to use and what values to supply.

**Command 1 - Find which complex types own x_ elements**

```powershell
Select-String -Path .\schemas\TPG_TargetMaster.xsd -Pattern '"x_[A-Za-z]' |
    ForEach-Object {
        $lines = Get-Content $_.Path
        $lines[0..($_.LineNumber-2)] | Select-String 'complexType name=' | Select-Object -Last 1
    } | Sort-Object -Unique
```

Look at the type names returned. They tell you the container shape.

**Command 2 - Find all top-level response and request container types**

```powershell
Select-String -Path .\schemas\TPG_TargetMaster.xsd -Pattern 'complexType name="[A-Za-z]+(Rq|Rs)_MType"' |
    Select-Object -First 20
```

Use this to confirm whether the file is inquiry-side (Rs), modification-side (Rq), or both.

**Command 3 - Inspect a specific container type in full**

```powershell
$typeName = "DepAcctModRq_MType"
Select-String -Path .\schemas\TPG_TargetMaster.xsd -Pattern "complexType name=""$typeName""" |
    ForEach-Object { Get-Content $_.Path | Select-Object -Skip ($_.LineNumber - 1) -First 50 }
```

Replace the type name with one found in Commands 1 or 2.

---

### Step 2 - Determine the config type

Use the table below to classify the XSD based on what Commands 1 and 2 returned.

| What you see in the output       | Config type to use        | Class to add entry to      |
|----------------------------------|---------------------------|----------------------------|
| `*AcctInqRec_CType` containers   | Inquiry / XElement config | `DomainConfigs`            |
| Single `*InqRs_MType` container  | Inquiry / XElement config | `DomainConfigs`            |
| `*AcctModRq_MType` containers    | Modification config       | `ModificationDomainConfigs`|
| Single `*ModRq_MType` container  | Modification config       | `ModificationDomainConfigs`|
| Only Add/Ext/Ren/Pmt types       | No config needed          | n/a                        |
| Mix of Rq and Rs with x_         | Both config types         | Both classes               |

---

           | Multiple prefixes, one regex   |
| `CustModRq_MType`                            | `^(?<key>Cust)ModRq_MType# JXSchemaGenerator

A .NET CLI tool that parses JXChange XSD schema files and generates JSON element mapping files.
These files are consumed by API client utilities (InquiryElements.js, etc.) to resolve which
extended elements (x_ prefixed) and their tags should be included in JXChange API requests.

---

## Requirements

- .NET 10 SDK
- JXChange XSD schema files in a local folder

---

## Usage

```
dotnet run --project .\src\JXSchemaGenerator.Cli -- --input <SchemaFolder> [--root <RootXsdFile>] [--out <OutputFolder>] [--strict true|false]
```

**Arguments**

| Argument  | Required | Default     | Description                                              |
|-----------|----------|-------------|----------------------------------------------------------|
| --input   | yes      |             | Folder containing all XSD files                          |
| --root    | no       |             | Single XSD file to process. Omit to process all masters  |
| --out     | no       | .\artifacts | Output folder for generated JSON                         |
| --strict  | no       | true        | Fail on schema compile errors                            |

**Process all master XSDs automatically**

```
dotnet run --project .\src\JXSchemaGenerator.Cli -- --input .\schemas --out .\artifacts
```

**Process a single XSD**

```
dotnet run --project .\src\JXSchemaGenerator.Cli -- --input .\schemas --root TPG_InquiryMaster.xsd --out .\artifacts
```

---

## Output

All output files share the same top-level structure and use `entityTypes` as the
uniform collection key regardless of domain.

| Root XSD                  | Output File(s)                                              |
|---------------------------|-------------------------------------------------------------|
| TPG_InquiryMaster.xsd     | inquiryElements.json                                        |
| TPG_CustomerMaster.xsd    | customerElements.json, customerModificationElements.json    |
| TPG_DepositMaster.xsd     | depositModificationElements.json                            |
| TPG_LoanMaster.xsd        | loanModificationElements.json                               |

```json
{
  "version": "1.0.0",
  "lastUpdated": "2026-02-21",
  "entityTypes": {
    "D": {
      "name": "Deposit/Checking/Savings",
      "aliases": ["S", "X"],
      "extendedElements": [
        {
          "name": "x_DepInfoRec",
          "description": null,
          "tags": ["AcctId", "AcctStat", "BrCode", "..."]
        }
      ]
    }
  }
}
```

---

## Supported Domains

### Inquiry (TPG_InquiryMaster.xsd)

| Key | Name                     | Aliases |
|-----|--------------------------|---------|
| D   | Deposit/Checking/Savings | S, X    |
| T   | Time Deposit/CD          |         |
| L   | Loan                     | O       |
| B   | Safe Deposit Box         |         |
| C   | Collection/Trust         |         |

### Customer (TPG_CustomerMaster.xsd)

| Key  | Name     | Aliases |
|------|----------|---------|
| Cust | Customer |         |

### Deposit Modification (TPG_DepositMaster.xsd)

| Key | Name                     | Aliases |
|-----|--------------------------|---------|
| D   | Deposit/Checking/Savings | S, X    |
| T   | Time Deposit/CD          |         |
| B   | Safe Deposit Box         |         |
| C   | Collection/Trust         |         |

### Loan Modification (TPG_LoanMaster.xsd)

| Key | Name | Aliases |
|-----|------|---------|
| L   | Loan | O       |

---

## Adding a New Domain

### Step 1 - Identify the XSD pattern

Run these three commands against the target XSD. The output determines which
config type to use and what values to supply.

**Command 1 - Find which complex types own x_ elements**

```powershell
Select-String -Path .\schemas\TPG_TargetMaster.xsd -Pattern '"x_[A-Za-z]' |
    ForEach-Object {
        $lines = Get-Content $_.Path
        $lines[0..($_.LineNumber-2)] | Select-String 'complexType name=' | Select-Object -Last 1
    } | Sort-Object -Unique
```

Look at the type names returned. They tell you the container shape.

**Command 2 - Find all top-level response and request container types**

```powershell
Select-String -Path .\schemas\TPG_TargetMaster.xsd -Pattern 'complexType name="[A-Za-z]+(Rq|Rs)_MType"' |
    Select-Object -First 20
```

Use this to confirm whether the file is inquiry-side (Rs), modification-side (Rq), or both.

**Command 3 - Inspect a specific container type in full**

```powershell
$typeName = "DepAcctModRq_MType"
Select-String -Path .\schemas\TPG_TargetMaster.xsd -Pattern "complexType name=""$typeName""" |
    ForEach-Object { Get-Content $_.Path | Select-Object -Skip ($_.LineNumber - 1) -First 50 }
```

Replace the type name with one found in Commands 1 or 2.

---

### Step 2 - Determine the config type

Use the table below to classify the XSD based on what Commands 1 and 2 returned.

| What you see in the output       | Config type to use        | Class to add entry to      |
|----------------------------------|---------------------------|----------------------------|
| `*AcctInqRec_CType` containers   | Inquiry / XElement config | `DomainConfigs`            |
| Single `*InqRs_MType` container  | Inquiry / XElement config | `DomainConfigs`            |
| `*AcctModRq_MType` containers    | Modification config       | `ModificationDomainConfigs`|
| Single `*ModRq_MType` container  | Modification config       | `ModificationDomainConfigs`|
| Only Add/Ext/Ren/Pmt types       | No config needed          | n/a                        |
| Mix of Rq and Rs with x_         | Both config types         | Both classes               |

---

                    | Single flat container          |

The named capture group must be called "key". It is passed to the TypeToEntry
switch expression to produce the output entity key, display name, and aliases.

---

### Step 4 - Build the config entry

**Inquiry / XElement config (DomainConfig.cs)**

```csharp
public static readonly DomainConfig Wire = new()
{
    OutputFileName     = "wireElements.json",
    CollectionKeyName  = "entityTypes",           // always entityTypes
    ContainerTypeRegex = new Regex(@"^(?<key>Wire)InqRs_MType$", RegexOptions.Compiled),
    OutputOrder        = ["Wire"],
    TypeToEntry        = key => key switch
    {
        "Wire" => new("Wire", "Wire Transfer", []),
        _      => null,                           // null = skip this container
    },
};
```

**Modification config (ModificationDomainConfig.cs)**

```csharp
public static readonly ModificationDomainConfig Wire = new()
{
    OutputFileName     = "wireModificationElements.json",
    ContainerTypeRegex = new Regex(@"^(?<key>Wire)ModRq_MType$", RegexOptions.Compiled),
    OutputOrder        = ["Wire"],
    TypeToEntry        = key => key switch
    {
        "Wire" => new("Wire", "Wire Transfer", []),
        _      => null,
    },
};
```

**Multi-entity example (multiple prefixes, one regex)**

```csharp
TypeToEntry = key => key switch
{
    "Dep"     => new("D", "Deposit/Checking/Savings", ["S", "X"]),
    "TimeDep" => new("T", "Time Deposit/CD",          []),
    "Ln"      => new("L", "Loan",                     ["O"]),
    _         => null,   // unknown prefix = skip silently
},
```

---

### Step 5 - Register the config

Add a line to the appropriate FromRootXsd switch in DomainConfig.cs or
ModificationDomainConfig.cs.

```csharp
public static DomainConfig? FromRootXsd(string rootXsd) =>
    Path.GetFileName(rootXsd).ToLowerInvariant() switch
    {
        "tpg_inquirymaster.xsd"  => Inquiry,
        "tpg_customermaster.xsd" => Customer,
        "tpg_wiremaster.xsd"     => Wire,      // add this line
        _                        => null,
    };
```

---

### Step 6 - Run and verify

```txt
dotnet run --project .\src\JXSchemaGenerator.Cli -- --input .\schemas --root TPG_WireMaster.xsd --out .\artifacts
```

The summary line at the end of the run reports how many sections resolved zero tags.
If any are listed, run Command 3 from Step 1 against those specific type names to
diagnose why the type lookup missed.

---

## Project Structure

```txt
JXSchemaGenerator.slnx
src/
  JXSchemaGenerator.Cli/
    Program.cs                              - CLI entry point, loops over all master XSDs
  JXSchemaGenerator.Core/
    Emit/
      JsonEmitter.cs                        - JSON serialization helper
    Expand/
      ExpansionOptions.cs                   - Controls tag expansion depth and filtering
      TypeExpander.cs                       - Recursively expands XSD complex types into tag lists
    Generate/
      DomainConfig.cs                       - Inquiry/XElement domain configs  (add new here)
      ModificationDomainConfig.cs           - Modification domain configs      (add new here)
      XElementGenerator.cs                  - Generic inquiry element generator
      ModificationGenerator.cs              - Generic modification element generator
      Models/
        InquiryModels.cs                    - Output model for inquiry/element files
        ModificationModels.cs               - Output model for modification files
    Schema/
      SchemaIndex.cs                        - In-memory index of compiled XSD types and elements
      SchemaLoader.cs                       - Loads and compiles XSD files into XmlSchemaSet
      LocalFolderXmlResolver.cs             - Resolves XSD imports from a local folder
tests/
  JXSchemaGenerator.Tests/
    UnitTest1.cs
```

---

## How Tag Expansion Works

For each element found in a container type, the tool attempts to resolve its full
tag list using three strategies in order.

| Strategy | Description                                                                                                                                                             |
|----------|-------------------------------------------------------------------------------------------------------------------------------------------------------------------------|
| 1        | ElementSchemaType -- the .NET XSD compiler resolved the type reference directly on the element object after compilation.                                                |
| 2        | SchemaTypeName with namespace backfill -- local element type references in JXChange XSDs often have an empty namespace after compilation. The enclosing type's namespace is filled in and the index lookup is retried. |
| 3        | Naming convention fallback -- strips any x_prefix and appends_CType or _AType to construct the expected type name. Example: x_DepInfoRec resolves to x_DepInfoRec_CType. |

Once a type is resolved it is expanded recursively. All element names at every
depth are included as tags, not just leaf nodes, because the JXChange API expects
both container element names and their children in the request.
