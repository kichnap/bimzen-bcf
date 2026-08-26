[English] · [Русский](AGENTS.ru.md)

# Repository conventions

Rules that apply to every change here, whether it is made by a person or by
an assistant. They are short on purpose: each one exists because breaking it
has cost something.

## Languages

| Where | Language |
|---|---|
| Code: identifiers, `//` comments, exception messages | English |
| XML documentation (`/// <summary>`) | English **and** Russian |
| `README` | English, Russian, German, Dutch, Finnish |
| Other documents | English and Russian |
| Commit messages | Russian |
| Conversation with the repository owner | Russian only |

**The bilingual XML-doc format.** English paragraph first, a blank line,
then the Russian one. The English half states the contract; the Russian half
usually carries the reasoning — why this decision and not the obvious one.
Both halves are maintained together: changing one and not the other is worse
than having only one.

```csharp
/// <summary>
/// Strict on write, lenient on read: a value outside the vocabulary is an
/// error when writing and is preserved as-is when reading someone else's
/// archive.
///
/// Строго на запись, терпимо на чтение: значение вне справочника при записи —
/// ошибка, а при чтении чужого архива оно сохраняется как есть.
/// </summary>
```

A short summary that fits on one line each way skips the blank line:

```csharp
/// <summary>
/// The topic title.
/// Заголовок замечания.
/// </summary>
```

A summary of two or three words — an enum member, mostly — fits on a single
line, the halves separated by a slash:

```csharp
/// <summary>Millimetres. / Миллиметры.</summary>
```

Inline `//` comments inside method bodies are English only. They are short,
they sit next to the code they explain, and doubling them makes the code
harder to read rather than easier.

**Where the bilingual rule applies.** To `Bcf.Core` and to the two generator
projects — the code a consumer reads, through IntelliSense or while working
out how the vocabulary file becomes constants. `Bcf.Core.Tests` is English
only, XML docs included: no consumer sees those, and the tests are read by
whoever is changing them. `BcfVocabulary.g.cs` is bilingual as well, but it
is never edited by hand — the pairs of labels come from `labels.en` and
`labels.ru` in `bcf-vocabularies/bcf-extensions.json`.

**Messages are English, topic text stays as it is.** Exception messages,
warnings in `BcfWriteReport` and `BcfExportResult`, and the console output of
the generators are English: they are read by whoever embeds the library. The
text the export puts *into* topics — titles, descriptions — is a different
matter: it reaches the coordinator who opens the file, and it is not the
library's business to change the language of the deliverable. It is Russian
today because that is what the first host needed; making it translatable is
an open question, not an oversight.

**Where the bilingual rule applies.** To `Bcf.Core` and to the two generator
projects — the code a consumer reads, through IntelliSense or while working
out how the vocabulary file becomes constants. `Bcf.Core.Tests` is English
only, XML docs included: no consumer sees those, and the tests are read by
whoever is changing them. `BcfVocabulary.g.cs` is bilingual as well, but it
is never edited by hand — the pairs of labels come from `labels.en` and
`labels.ru` in `bcf-vocabularies/bcf-extensions.json`.

**Messages are English, topic text stays as it is.** Exception messages,
warnings in `BcfWriteReport` and `BcfExportResult`, and the console output of
the generators are English: they are read by whoever embeds the library. The
text the export puts *into* topics — titles, descriptions — is a different
matter: it reaches the coordinator who opens the file, and it is not the
library's business to change the language of the deliverable. It is Russian
today because that is what the first host needed; making it translatable is
an open question, not an oversight.

**Documents come in pairs.** `name.md` in English and `name.ru.md` in
Russian, with a language line at the top of each. The `README` additionally
has `README.de.md`, `README.nl.md` and `README.fi.md`.

## What must not appear here

**Names of private projects.** This repository is public and independent.
Do not name the closed products that embed it, their repositories, their
file layout, or their internal paths. Write about *a host*, *a consumer*,
*an embedding tool*.

Names of third-party products are a different matter and stay: Navisworks,
Revit, Solibri, BIMcollab, IFC, buildingSMART. Without them the
documentation loses its meaning — "clash identifiers are regenerated when
the test is reset" is a fact about the domain, not a reference to anyone's
private code.

**Vocabulary values as string literals.** Every type, status, priority,
label and stage comes from `bcf-vocabularies/bcf-extensions.json` through
the generated `BcfVocabulary`. `NoHardcodedVocabularyTests` enforces this.

**Runtime dependencies in `Bcf.Core`.** The library may be loaded into a
process that already carries another copy of it. Build-time-only packages
with `PrivateAssets="all"` are acceptable; anything the consumer would have
to resolve at runtime is not.

**References to any host application.** No `Autodesk.*`, no add-in APIs.
The library builds and tests on a machine where no BIM application is
installed, and that is checked by CI, not by good intentions.

## Rules the code relies on

- **Invariant culture everywhere.** Numbers and dates are formatted with
  `CultureInfo.InvariantCulture`. Tests run under three locales, Turkish
  among them.
- **No `String.GetHashCode` for anything stable.** It is randomised per
  process, and an export would stop being reproducible. Use SHA-256
  (`StableTopicKey`) for identity and FNV-1a for internal hashing.
- **No UI and no network.** Progress goes through `IProgress<T>`,
  cancellation through `CancellationToken`, failure comes back as a result
  rather than an exception thrown at the caller.
- **The output is validated against the official XSDs in tests.** A change
  that makes a valid archive invalid must fail the build, not a user's day.

## Before you commit

```
dotnet test Bcf.Core.Tests/Bcf.Core.Tests.csproj
```

Both target frameworks must pass. If you touched the vocabulary, run the
generator; if you touched the export settings, the schema in `schemas/api/`
is checked against the class by a test and will tell you.
