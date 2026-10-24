# JXSchemaGenerator

A .NET CLI tool that compiles the Jack Henry jXchange TPG XSDs and writes one JSON file per Master.
The files describe every operation, its request and response element trees and the named types they use.
They are consumed by SOAP client libraries that build jXchange requests and generate types.

---

## Requirements

- .NET 10 SDK 10.0.100 or newer, since `global.json` rolls forward to the newest installed 10.0 feature band
- pnpm 12 for the Node tooling, which downloads the pnpm and Node versions declared in `package.json` `devEngines` when the installed ones do not match

## Development

```sh
pnpm install          # installs the Node tooling and the lefthook git hooks
dotnet tool restore   # installs CSharpier and ReportGenerator from .config/dotnet-tools.json
pnpm fetch:xsd        # downloads the newest TPG XSD zip into ./schemas
pnpm generate         # writes the JSON mappings into ./dist/schemas
pnpm test             # runs the xUnit v3 tests on Microsoft.Testing.Platform
```

| Script                      | What it does                                                        |
| --------------------------- | ------------------------------------------------------------------- |
| `pnpm fetch:xsd`            | Downloads the newest TPG release listed on the Jack Henry Downloads page |
| `pnpm fetch:xsd:all`        | Downloads every listed TPG release into `./schemas/R<version>/`, which `generate` reads with `--input schemas/R<version>` |
| `pnpm fetch:xsd:version X`  | Downloads one release, for example `2026.1.00`                      |
| `pnpm generate`             | Runs the `generate` command over `./schemas`                        |
| `pnpm format`               | Formats with Prettier and CSharpier                                 |
| `pnpm format:check`         | Checks Prettier and CSharpier formatting                            |
| `pnpm lint`                 | Checks Prettier formatting and runs markdownlint                    |

Tooling:

- **Formatting:** CSharpier owns C# and MSBuild files, Prettier owns JSON, YAML and JavaScript, and markdownlint-cli2 owns Markdown
- **Analyzers:** the SDK's built-in CA rules at `latest-recommended`, with code style enforced on build and every warning except advisories treated as an error in CI
- **Packages:** versions live only in `Directory.Packages.props` (Central Package Management), and CI rejects any `packages.lock.json`
- **Git hooks:** lefthook formats staged files on commit, runs commitlint on the message, and builds before push
- **Commits:** Conventional Commits with a sentence-case subject, checked by commitlint locally and on PR titles in CI

Jack Henry lists only the two newest TPG releases on <https://jackhenry.dev/jxchange-soap/documentation/> and deletes superseded zips, so `./schemas` is gitignored and always downloaded.

## Releases

The **Release** workflow is started by hand from the Actions tab, with an optional `tpg-version` input.
It fetches the XSDs, generates the mappings and publishes `jxchange-schemas.zip` with a `SHA256SUMS` file and a build provenance attestation.
Tags take the form `tpg-<tpg version>-<short sha>`, for example `tpg-2026.1.00-1a2b3c4`.
The **TPG watch** workflow checks the Downloads page daily and opens an issue when a TPG release has not been published here yet.

Verify a downloaded bundle with:

```sh
gh attestation verify jxchange-schemas.zip -R tnaldoza/JX-Schema-Generator
```

jXchange is a trademark of Jack Henry & Associates, Inc.
This project is not affiliated with or endorsed by Jack Henry.
Releases contain generated mappings only, never the vendor XSD, WSDL or zip files.

---

## Commands

### `generate`

Compiles every top-level `*Master.xsd` in the input folder, or one Master, and writes one JSON file per Master plus the format's JSON Schema.

```txt
dotnet run --project src/JXSchemaGenerator.Cli -- generate --input <SchemaFolder> [--root <XsdFile>] [--out <OutputFolder>]
```

| Argument  | Required | Default     | Description                                                                         |
| --------- | -------- | ----------- | ----------------------------------------------------------------------------------- |
| `--input` | yes      |             | Folder containing all XSD files                                                     |
| `--root`  | no       |             | One Master, as a bare file name inside `--input`. Omit to process every Master      |
| `--out`   | no       | `artifacts` | Output folder for the generated JSON                                                |

Exit codes: 0 when every Master was written, 1 when a Master failed or none was found, 2 for a usage error.
A Master that fails does not stop the others.
The run never deletes files and warns about each JSON file in the output folder that it did not write.

```txt
# Process every Master
dotnet run --project src/JXSchemaGenerator.Cli -- generate --input schemas --out artifacts

# Process one Master
dotnet run --project src/JXSchemaGenerator.Cli -- generate --input schemas --root TPG_DepositMaster.xsd --out artifacts
```

---

## Output

Each Master produces one file, for example `deposit.json` for `TPG_DepositMaster.xsd`, next to `jxchange-schemas.schema.json`, which describes the format.

- [docs/format.md](docs/format.md) is the format 2 reference
- [docs/migrating-to-format-2.md](docs/migrating-to-format-2.md) maps the old per-verb files to format 2

Operations, modify containers, `x_` elements and search records come from the XSD structure.
No configuration lists them.

---

## Project Structure

```txt
JXSchemaGenerator.slnx
src/
  JXSchemaGenerator.Cli/
    Program.cs                        CLI entry point for the generate command
  JXSchemaGenerator.Core/
    Schema/                           Loads and compiles XSD files, indexes their types
    Model/                            The document model one Master builds into
    Build/                            Operation discovery, element trees, type table, helpers, folder runner
    Emit/                             Format 2 JSON writer, output file names, embedded JSON Schema
tests/
  JXSchemaGenerator.Tests/
    Fixtures/                         Hand-written sample Master and its golden output
```
