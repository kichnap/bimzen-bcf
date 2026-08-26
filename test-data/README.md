[English] · [Русский](README.ru.md)

# Reference archives

Fixtures for import tests. They are produced by the real exporter
(`BcfClashExporter`) over a synthetic clash source — the same code that writes
production files, not markup hand-written for a test.

Rebuild them with:

```
dotnet run --project Bcf.TestData.Generator
```

Generation is **reproducible byte for byte**: the export time and the archive
entry timestamps are fixed, and identifiers are derived from stable keys.
A second run with no code changes touches no file, so the repository history
stays free of noise.

## Contents

| File | What is inside |
|---|---|
| `small-3-topics-bcf30.bcfzip` | 3 topics, one topic per clash group, snapshots everywhere |
| `small-3-topics-bcf21.bcfzip` | the same in BCF 2.1 |
| `large-500-topics-bcf30.bcfzip` | 500 topics, one topic per clash |
| `large-500-topics-bcf21.bcfzip` | the same in BCF 2.1 |
| `foreign-values-bcf30.bcfzip` | an archive carrying another tool's vocabulary |

In the large archives only the first fifty topics have snapshots: five hundred
images would inflate the file in the repository into megabytes, and an
importer benefits more from seeing both cases in one file — a topic with a
snapshot and a topic without one.

The snapshots are real PNGs (320×240, a gradient) rather than a placeholder
carrying the format signature: an importer must be able to decode them.

## `foreign-values-bcf30.bcfzip`

Statuses are replaced with `Открыто`, types with `Пересечение`. Neither exists
in the vocabulary, and our exporter cannot produce such a file: validation on
write is strict. The fixture was built by substituting values inside a
finished archive — exactly as BIMcollab, Revizto or Solibri would send it with
their own vocabularies.

This is legitimate: the BCF standard does not fix the vocabularies, it only
describes the mechanism for declaring them. **An importer must accept such a
file**, keep the values as they are and show them marked as external, rather
than reject the file or silently replace the values with "correct" ones. The
test `TestDataFixturesTests.ForeignValues_AreReadWithoutError_AndReported`
checks exactly that.

## What these fixtures are good for

- parsing `markup.bcf`, `extensions.xml` (3.0) and `extensions.xsd` (2.1);
- cameras: perspective, clipping planes, `AspectRatio` present in 3.0 and
  absent in 2.1, and the field of view clamped to [45; 60] in 2.1;
- non-ASCII text in content while archive entry names stay ASCII;
- numbers with a dot as the decimal separator and ISO 8601 dates with an
  explicit offset;
- behaviour on 500 topics: parsing time and memory use.
