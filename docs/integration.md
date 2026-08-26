[English] · [Русский](integration.ru.md)

# Embedding Bcf.Core in your own tool

`Bcf.Core` is a BCF library, not a part of any application. It targets
`netstandard2.0`, has no external dependencies and knows nothing about the
host: not the modelling application, not the user interface. Everything tied
to a host lives behind ports — interfaces that the embedding code implements.

The library already works in two roles: inside a desktop add-in that exports
clash results, and inside a headless agent that produces the same archives on
a schedule with no window anywhere.

## What the library takes care of

| | |
|---|---|
| The BCF model | Topics, comments, viewpoints, cameras — following the buildingSMART specification, not the shape of the zip |
| Writing | BCF 3.0 and BCF 2.1 as two independent serializers; the result is validated against the official XSDs in tests |
| Reading | Lenient: other tools' statuses and types are preserved as-is and reported |
| Updating | Append topics to an existing archive without losing what a receiving tool added to it |
| Camera | Perspective and orthogonal, quaternion to direction and up vector, per-version limits |
| Units | Any host unit to the metres BCF requires |
| Identifiers | IFC GUID (buildingSMART base64 compression), Revit `UniqueId` to IFC GUID |
| Vocabularies | Types, statuses, priorities, labels, stages from one file, with generated constants |
| Idempotency | A stable topic key: a repeated export does not create duplicates |

What the library does **not** do: open models, render snapshots, know about
licences, show windows, or use the network.

## The ports the host implements

### `IClashSource` — required

Where the clashes come from.

```csharp
public interface IClashSource
{
    ClashDocumentInfo GetDocument();
    IReadOnlyList<ClashTestInfo> GetTests();
    IEnumerable<ClashItem> EnumerateClashes(ClashTestInfo test, CancellationToken cancellationToken);
    ClashViewpointData CreateViewpoint(ClashItem clash, SnapshotRequest snapshot, CancellationToken cancellationToken);
}
```

`EnumerateClashes` returns an `IEnumerable` rather than a list on purpose:
five thousand clashes with snapshots must not sit in memory at once.

`CreateViewpoint` is the only place where the host draws. It receives a
`SnapshotRequest` carrying the frame size, the capture mode, the isolation
method and a time budget, and returns a camera and, if asked, a PNG. If it
cannot, return `null` or fill in `Warning`: one frame must not fail the whole
export.

### `ISavedViewpointSource` — optional

Issues that clash logic cannot express: a device rotated the wrong way, a
pipe crossing the middle of a room, an assembly built off-design. A person
records those as a saved view, and this port turns them into topics of the
same archive. Not supplied — only clashes are exported.

### `ITopicGuidStore` — optional

The map of topic identifiers already handed out. Without it an identifier is
derived from the stable key, which is enough until a server starts issuing
them. A ready file-backed implementation is `TopicGuidMap`.

## A minimal run

```csharp
var settings = new BcfExportSettings
{
    Author = "coordinator@example.com",
    ProjectName = "Northern Quarter",
    Version = BcfVersion.Bcf30
};

using (var file = File.Create(@"C:\exports\clashes.bcfzip"))
{
    BcfExportResult result = new BcfClashExporter(source, topicGuids, viewpoints)
        .Export(file, settings, progress, cancellationToken);

    if (!result.Succeeded) { /* result.Error, result.Warnings */ }
}
```

Updating an existing archive is the same call with a second stream:

```csharp
using (var existing = File.OpenRead(path))
using (var destination = File.Create(path + ".tmp"))
{
    settings.UpdateMode = BcfUpdateMode.AppendNew;

    var result = exporter.Export(destination, existing, settings);
}
```

Never write over the source file: an interrupted write leaves the user with
neither version. Replace the target only after success.

## Settings

The full description is
[`schemas/api/bcf-export-settings.schema.json`](../schemas/api/bcf-export-settings.schema.json).
The schema is compared with the `BcfExportSettings` class by a test on every
build: a field added to the code and forgotten in the schema fails the build.

Vocabulary values are deliberately not enumerated in the schema — their only
source is
[`bcf-vocabularies/bcf-extensions.json`](../bcf-vocabularies/bcf-extensions.json),
from which the `BcfVocabulary` constants are generated. A second list of
values would inevitably drift from the first.

## Rules the library relies on

**Strict on write, lenient on read.** A value outside the vocabulary is an
exception while the topic is being built. The same value read from someone
else's archive is preserved as-is: the standard defines the mechanism for
vocabularies but does not fix their contents, and a file from BIMcollab or
Revizto legitimately arrives with its own statuses.

**Invariant culture everywhere.** Numbers and dates are written through
`CultureInfo.InvariantCulture`. The tests run under three locales, Turkish
among them.

**No `String.GetHashCode`.** It is randomised across processes, and the
export would stop being reproducible. Identity uses SHA-256
(`StableTopicKey`); internal hashing uses FNV-1a.

**Nothing goes to a UI or to the network.** Progress travels through
`IProgress<T>`, cancellation through a `CancellationToken`, and a failure
comes back as a result rather than an exception thrown at the caller.

## Building and referencing

```
git clone https://github.com/kichnap/bimzen-bcf.git
```

Then reference the project `Bcf.Core/Bcf.Core.csproj`. A NuGet package
(`BimZen.Bcf.Core`) is being prepared and will become the recommended way.

The `Bcf.Core.Tests` suite builds for `net48` and `net8.0` and needs nothing
but xUnit — no modelling application is required to run it.
