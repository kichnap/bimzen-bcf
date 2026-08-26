**English** · [Русский](README.ru.md) · [Deutsch](README.de.md) · [Nederlands](README.nl.md) · [Suomi](README.fi.md)

# bimzen-bcf

[![Build](https://github.com/kichnap/bimzen-bcf/actions/workflows/ci.yml/badge.svg)](https://github.com/kichnap/bimzen-bcf/actions/workflows/ci.yml)
[![License: MIT](https://img.shields.io/badge/license-MIT-green.svg)](LICENSE)
[![Target](https://img.shields.io/badge/target-netstandard2.0-blue.svg)](Bcf.Core/Bcf.Core.csproj)
[![BCF](https://img.shields.io/badge/BCF-3.0%20%7C%202.1%20%7C%202.0%20read-blue.svg)](https://github.com/buildingSMART/BCF-XML)

**A .NET library for the buildingSMART Collaboration Format (BCF), plus the
single source of truth for the vocabularies that go with it.**

`Bcf.Core` targets `netstandard2.0`, has zero runtime dependencies, and knows
nothing about any host application. Everything host-specific lives behind
narrow ports that the embedding tool implements. The output is validated
against the official buildingSMART XSDs on every build.

## What it does

| Area | What you get |
|---|---|
| Write | BCF 3.0 and BCF 2.1 — two independent serializers, not one parameterised writer |
| Read | BCF 3.0, 2.1 and 2.0 (read-only). Lenient by design: unknown statuses and types from other tools are preserved, never rejected |
| Update | Append to an existing archive without losing what a receiving tool put there |
| Camera | Perspective and orthogonal, quaternion to direction and up vector, per-version limits |
| Units | Any host unit to the metres BCF requires |
| Identifiers | IFC GUID both ways, and Revit `UniqueId` to IFC GUID with the exporter's own algorithm |
| Vocabularies | Types, statuses, priorities, labels and stages from one file, with generated constants |
| Idempotency | A stable topic key that survives a re-run, so a repeated export does not create duplicates |

What it deliberately does **not** do: open models, render snapshots, check
licences, show windows, or touch the network. All of that belongs to the host.

## Quick start

```csharp
var settings = new BcfExportSettings
{
    Author = "coordinator@example.com",
    ProjectName = "Northern Quarter",
    Version = BcfVersion.Bcf30
};

using (var file = File.Create(@"C:\exports\clashes.bcfzip"))
{
    BcfExportResult result = new BcfClashExporter(source).Export(file, settings);

    if (!result.Succeeded) { /* result.Error, result.Warnings */ }
}
```

`source` is your implementation of `IClashSource` — the one port that is
mandatory. The full contract, including the optional ports, is in
[`docs/integration.md`](docs/integration.md).

## Installation

A NuGet package (`BimZen.Bcf.Core`) is being prepared. Until it is published,
reference the project directly:

```
git clone https://github.com/kichnap/bimzen-bcf.git
dotnet add <your-project> reference bimzen-bcf/Bcf.Core/Bcf.Core.csproj
```

Non-.NET consumers can use the vocabulary file
[`bcf-vocabularies/bcf-extensions.json`](bcf-vocabularies/bcf-extensions.json)
on its own — it is plain JSON and carries no code.

## Repository layout

```
Bcf.Core/            the BCF model, converters and serializers (netstandard2.0)
Bcf.Core.Tests/      xUnit, net48 + net8.0
bcf-vocabularies/    the canonical vocabulary — the ONLY source of truth
schemas/3.0/         XSDs from buildingSMART/BCF-XML, branch release_3_0
schemas/2.1/         XSDs from buildingSMART/BCF-XML, branch release_2_1
schemas/api/         machine-readable description of the export settings
docs/integration.md  the contract for embedding the library in your own tool
test-data/           reference .bcfzip fixtures for import tests
```

## Design rules that are easy to break without noticing

- **`Bcf.Core` knows nothing about any host.** No reference to any BIM
  application: the library builds and its tests run on a machine where none
  is installed. Data arrives through a narrow port, `IClashSource`.
- **Zero NuGet dependencies in `Bcf.Core`.** The library may end up in the
  same process as another add-in carrying the same library. Every dependency
  doubles the chance of a `TypeLoadException` on a version mismatch. For the
  same reason the assembly is not strong-named.
- **The model follows the specification, not the shape of the zip archive.**
  BCF describes the same entities twice — XML in a file and JSON over HTTP.
  The model is shared; the serialization is swappable.
- **Vocabulary values are never hard-coded.** Constants are generated from
  `bcf-vocabularies/bcf-extensions.json`; `extensions.xml` (3.0) and
  `extensions.xsd` (2.1) are generated from it too, not shipped as ready files.
- **Validation is asymmetric: strict on write, lenient on read.** A file
  produced by BIMcollab or Revizto legitimately contains statuses you have
  never seen. Rejecting it is the fastest way to be known as the tool that
  "does not understand openBIM".

## Generating the vocabulary constants

Vocabulary values reach the code through the generator only — nobody types
them by hand:

```
dotnet run --project Bcf.Vocabulary.Generator            # rewrite Bcf.Core/Vocabulary/BcfVocabulary.g.cs
dotnet run --project Bcf.Vocabulary.Generator -- --check # verify the file is up to date
```

Forgetting to regenerate is not possible: `VocabularyDriftTests` builds the
constants again from `bcf-extensions.json` and compares them with the
committed file, and `NoHardcodedVocabularyTests` makes sure values such as
`"In Progress"` never appear as string literals in the code.

The vocabulary files that go into an archive are assembled from the same
constants rather than stored ready-made: `ExtensionsWriter.Write30` produces
`extensions.xml` (BCF 3.0), `ExtensionsWriter.Write21` produces
`extensions.xsd` (BCF 2.1). The latter redefines types from `markup.xsd`, so
that schema has to travel inside the archive next to it.

## Reference archives

```
dotnet run --project Bcf.TestData.Generator
```

Builds the fixtures in `test-data/` with the real exporter, byte-for-byte
reproducibly. Details in [`test-data/README.md`](test-data/README.md).

Next to them, in [`test-data/buildingsmart/`](test-data/buildingsmart/README.md),
lie the official test cases from the buildingSMART repository. They were
written by other tools, so reading them is the only outside check of whether
this library understands the format the way its authors do — everything else
in `test-data/` we both write and read ourselves.

## Schemas

The XSDs come from the buildingSMART `BCF-XML` repository (branches
`release_3_0` and `release_2_1`) and are stored here unchanged. BCF 2.1 has
no `extensions.xsd` among its schemas: there, vocabularies are declared by a
file inside each archive, and `Bcf.Core` generates it. The reference copy for
comparison is `schemas/2.1/extensions.reference.xsd`.

## Building and testing

```
dotnet test Bcf.Core.Tests/Bcf.Core.Tests.csproj
```

Tests run on two target frameworks: `net48`, the runtime of desktop BIM
applications, and `net8.0` for services and background agents.

## Contributing

Repository conventions — language of code and documentation, the bilingual
XML-doc format, what may and may not be hard-coded — are in
[`AGENTS.md`](AGENTS.md).

## License

MIT — see [`LICENSE`](LICENSE).
