[English] · [Русский](README.ru.md)

# Official buildingSMART test cases

Archives taken unchanged from
[buildingSMART/BCF-XML](https://github.com/buildingSMART/BCF-XML), folder
`Test Cases`, commit `bc48611`. That repository is MIT-licensed, the same as
this one.

**Why they are here.** Every other fixture in `test-data/` is written by our
own exporter and read back by our own reader. A misunderstanding of the
format shared by both halves passes such a check unnoticed: the circle is
closed. These files were produced by other tools years before this library
existed, so reading them is the only outside check we have of whether we
understand BCF the way its authors do.

It found something on the first run: BCF 2.0 keeps components as a flat list
where selection and visibility are attributes, and the reader knew only the
2.1 layout. Archives in 2.0 used to read as topics with no components at
all — whole-looking and meaningless.

## What is here

| File | What it is worth checking for |
|---|---|
| `v2.1/MinimumInformation.bcf` | The smallest legal topic |
| `v2.1/MaximumInformation.bcf` | Two topics, three viewpoints, fifteen components, a foreign vocabulary, a snippet, a bitmap |
| `v2.1/ExternalBIMSnippet.bcf` | A snippet referenced outside the archive |
| `v2.1/InternalBIMSnippet.bcf` | A snippet stored inside the archive |
| `v2.1/RelatedTopics.bcf` | Two topics referring to each other |
| `v2.0/MinimumInformation.bcfzip` | The 2.0 minimum |
| `v2.0/SelectedComponent.bcfzip` | Selection through the flat 2.0 component list |
| `v2.0/SingleInvisibleWall.bcfzip` | Visibility as an attribute of a component |
| `v2.0/Clippingplane.bcfzip` | A camera and clipping planes written by another tool |
| `v2.0/ComponentColoring.bcfzip` | Colouring, which our model does not keep — it must not break the read |

`v2.1/RelatedTopics.bcf` is the file the source repository calls
`related topics with both topics in the same file/case.bcf`; only the name
was changed, the bytes were not.

## What is deliberately absent

**BCF 3.0.** The source repository has no test cases for it yet. Our 3.0
reading is therefore still checked against our own writer only — the very
closed circle described above. When official 3.0 cases appear, they belong
here.

**The rest of the 2.0 and 2.1 cases.** The full set is about five megabytes,
mostly IFC models embedded in the archives. The files above cover the
constructions that matter to a reader; the others repeat them with heavier
payloads.

The expectations for every file live in
`Bcf.Core.Tests/BuildingSmartTestCaseTests.cs`, and they were read out of the
archives themselves rather than out of our output.
