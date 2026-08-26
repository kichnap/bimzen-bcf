[English] · [Русский](CHANGELOG.ru.md)

# Changelog

All notable changes to this library. The format follows
[Keep a Changelog](https://keepachangelog.com/en/1.1.0/), the versioning
follows [SemVer](https://semver.org/).

The library is versioned independently of anything that embeds it: a
breaking change here is a major version of the package, never a silent
update.

## [Unreleased]

Preparing the first public release as a standalone component.

### Added

- Reading BCF 2.0 archives. They still turn up, and until now they were read
  as topics with no components at all: 2.0 keeps components in a flat list
  where selection and visibility are attributes. Writing 2.0 is refused with
  a clear message — reading old files is a courtesy, producing them is not.
- The official buildingSMART test cases as fixtures. Every other fixture here
  is written and read by this library alone; these were written by other
  tools, and they are the only outside check of the reader.
- MIT licence, English as the language of the repository, `README` in five
  languages and `AGENTS.md` with the conventions.
- Bilingual XML documentation across the public API: English states the
  contract, Russian carries the reasoning.
- NuGet package metadata for `BimZen.Bcf.Core`.

### Changed

- The library no longer describes itself in terms of the closed products
  that embed it. Hosts are referred to as hosts.

## What the library already does

The feature set below predates this changelog: it grew inside a closed
product and moved here as a whole. It is listed once, so that the first
public release is not an empty page.

- **Writing** BCF 3.0 and BCF 2.1 through two independent serializers;
  the output is validated against the official buildingSMART XSDs in tests.
- **Reading** other tools' archives leniently: unknown vocabulary values are
  preserved and reported rather than rejected.
- **Updating** an existing archive: topics the export did not touch are
  carried over byte for byte, together with statuses, comments and
  attachments added by a receiving tool.
- **Clash export** through the `IClashSource` port, with saved viewpoints as
  a second, optional source of topics.
- **Grouping** into topics per clash, per clash group, or per level, with a
  viewpoint for every pair inside a group so that pairing is not lost.
- **Identity that survives a re-run**: `StableTopicKey` and an optional
  `TopicGuidMap`, so a repeated export lands in the same topics instead of
  creating duplicates.
- **Converters**: IFC GUID both ways, Revit `UniqueId` to IFC GUID, host
  units to metres, camera orientation from a quaternion.
- **Vocabularies** generated from a single JSON file, with tests that fail
  the build if the generated constants drift from it.
