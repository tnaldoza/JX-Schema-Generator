# JX-Schema-Generator

.NET 10 CLI that compiles Jack Henry jXchange TPG XSDs and emits JSON element mappings for downstream SOAP clients (JXChange-CS, JXChange-TS).

## Commands

```sh
pnpm install                                   # Node tooling and lefthook hooks
dotnet tool restore                            # CSharpier, ReportGenerator
pnpm fetch:xsd                                 # newest TPG zip into ./schemas (gitignored)
dotnet build JXSchemaGenerator.slnx -c Release
dotnet test --solution JXSchemaGenerator.slnx  # xUnit v3 on Microsoft.Testing.Platform
pnpm generate                                  # ./schemas -> ./dist/schemas
pnpm format                                    # Prettier + CSharpier
pnpm lint                                      # Prettier check + markdownlint
```

## Layout

- `src/JXSchemaGenerator.Core` - schema loading (`Schema/`), the document model (`Model/`), builders (`Build/`), JSON output (`Emit/`)
- `src/JXSchemaGenerator.Cli` - the `generate` command only, assembly name `generate`
- `tests/JXSchemaGenerator.Tests` - xUnit v3 test executable; `Fixtures/` holds a hand-written sample Master and its golden output
- `scripts/fetch-xsd.js` - CommonJS fetcher using Node built-ins only

## Rules

- No StyleCop.Analyzers; analyzers are the SDK's CA rules at `latest-recommended` with `EnforceCodeStyleInBuild`
- No `packages.lock.json`; package versions live only in `Directory.Packages.props`, never as `Version=` on a `PackageReference`
- `global.json` is a floor with `rollForward: latestFeature`, never a hard pin
- pnpm only, no npm or yarn; no husky, hooks are lefthook
- Every GitHub Action is pinned to a full commit SHA with a `# vX.Y.Z` comment; workflows start with `permissions: {}` and pass inputs through `env:`
- CSharpier formats C# and MSBuild files, Prettier formats JSON, YAML and JS, markdownlint-cli2 owns Markdown
- Never commit Jack Henry XSDs, WSDLs, zips, PDFs or DOCX files, and never copy text from the confidential Contract Changes document into the repository
- Generated JSON key order uses ordinal comparers so output is stable across machines
- Operations, modify containers, x_ elements and search records come from XSD structure; no source file lists operation, container or Master names
- sample.golden.json changes only with a deliberate format change, and formatVersion goes up with any breaking one
