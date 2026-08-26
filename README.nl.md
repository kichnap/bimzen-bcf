[English](README.md) · [Русский](README.ru.md) · [Deutsch](README.de.md) · **Nederlands** · [Suomi](README.fi.md)

# bimzen-bcf

[![Build](https://github.com/kichnap/bimzen-bcf/actions/workflows/ci.yml/badge.svg)](https://github.com/kichnap/bimzen-bcf/actions/workflows/ci.yml)
[![License: MIT](https://img.shields.io/badge/license-MIT-green.svg)](LICENSE)
[![Target](https://img.shields.io/badge/target-netstandard2.0-blue.svg)](Bcf.Core/Bcf.Core.csproj)
[![BCF](https://img.shields.io/badge/BCF-3.0%20%7C%202.1-blue.svg)](https://github.com/buildingSMART/BCF-XML)

**Een .NET-bibliotheek voor het buildingSMART Collaboration Format (BCF), en
tegelijk de enige bron van waarheid voor de bijbehorende waardelijsten.**

`Bcf.Core` richt zich op `netstandard2.0`, heeft geen runtime-afhankelijkheden
en weet niets van de toepassing die haar insluit. Alles wat hostspecifiek is,
zit achter smalle poorten die het insluitende gereedschap implementeert. De
uitvoer wordt bij elke build getoetst aan de officiële XSD's van
buildingSMART.

## Wat de bibliotheek doet

| Gebied | Wat je krijgt |
|---|---|
| Schrijven | BCF 3.0 en BCF 2.1 — twee onafhankelijke serializers, niet één met een schakelaar |
| Lezen | Bewust tolerant: onbekende statussen en typen uit andere gereedschappen blijven behouden en worden nooit geweigerd |
| Bijwerken | Toevoegen aan een bestaand archief zonder te verliezen wat een ontvangend gereedschap erin heeft gezet |
| Camera | Perspectief en orthogonaal, quaternion naar richting en up-vector, grenzen per versie |
| Eenheden | Elke eenheid van de host naar de meters die BCF vereist |
| Identificatoren | IFC-GUID in beide richtingen en Revit-`UniqueId` naar IFC-GUID volgens het algoritme van de exporteur |
| Waardelijsten | Typen, statussen, prioriteiten, labels en fasen uit één bestand, met gegenereerde constanten |
| Idempotentie | Een stabiele onderwerpsleutel die een herhaalde run overleeft: de export maakt geen dubbelen |

Wat de bibliotheek bewust **niet** doet: modellen openen, snapshots renderen,
licenties controleren, vensters tonen of het netwerk op gaan. Dat is allemaal
het werk van de host.

## Snel aan de slag

```csharp
var settings = new BcfExportSettings
{
    Author = "coordinator@example.com",
    ProjectName = "Noorderkwartier",
    Version = BcfVersion.Bcf30
};

using (var file = File.Create(@"C:\exports\clashes.bcfzip"))
{
    BcfExportResult result = new BcfClashExporter(source).Export(file, settings);

    if (!result.Succeeded) { /* result.Error, result.Warnings */ }
}
```

`source` is jouw implementatie van `IClashSource` — de enige poort die
verplicht is. Het volledige contract, inclusief de optionele poorten, staat in
[`docs/integration.md`](docs/integration.md).

## Installatie

Er wordt een NuGet-pakket (`BimZen.Bcf.Core`) voorbereid. Zolang dat niet
gepubliceerd is, verwijs je rechtstreeks naar het project:

```
git clone https://github.com/kichnap/bimzen-bcf.git
dotnet add <jouw-project> reference bimzen-bcf/Bcf.Core/Bcf.Core.csproj
```

Gebruikers buiten .NET kunnen alleen het waardelijstbestand
[`bcf-vocabularies/bcf-extensions.json`](bcf-vocabularies/bcf-extensions.json)
gebruiken — gewone JSON, zonder code.

## Indeling van de repository

```
Bcf.Core/            het BCF-model, converters en serializers (netstandard2.0)
Bcf.Core.Tests/      xUnit, net48 + net8.0
bcf-vocabularies/    de canonieke waardelijst — de ENIGE bron van waarheid
schemas/3.0/         XSD's uit buildingSMART/BCF-XML, branch release_3_0
schemas/2.1/         XSD's uit buildingSMART/BCF-XML, branch release_2_1
schemas/api/         machineleesbare beschrijving van de exportinstellingen
docs/integration.md  het contract om de bibliotheek in eigen gereedschap in te bouwen
test-data/           referentiearchieven .bcfzip voor importtests
```

## Regels die je makkelijk ongemerkt breekt

- **`Bcf.Core` weet niets van de host.** Geen enkele verwijzing naar een
  BIM-toepassing: de bibliotheek bouwt en test op een machine waar er geen
  geïnstalleerd is. Gegevens komen binnen via de smalle poort `IClashSource`.
- **Nul NuGet-afhankelijkheden in `Bcf.Core`.** De bibliotheek kan in
  hetzelfde proces belanden als een andere add-in die haar ook meebrengt.
  Elke afhankelijkheid verdubbelt de kans op een `TypeLoadException` bij
  verschillende versies. Om dezelfde reden is de assembly niet strong-named.
- **Het model volgt de specificatie, niet de vorm van het zip-archief.** BCF
  beschrijft dezelfde entiteiten twee keer — XML in een bestand en JSON over
  HTTP. Het model is gedeeld, de serialisatie verwisselbaar.
- **Waarden uit de waardelijst staan nooit hard in de code.** Constanten
  worden gegenereerd uit `bcf-vocabularies/bcf-extensions.json`;
  `extensions.xml` (3.0) en `extensions.xsd` (2.1) eveneens, in plaats van als
  kant-en-klare bestanden mee te reizen.
- **De controle is asymmetrisch: streng bij schrijven, tolerant bij lezen.**
  Een bestand uit BIMcollab of Revizto bevat terecht statussen die je nooit
  hebt gezien. Het weigeren is de snelste manier om bekend te staan als het
  gereedschap dat "openBIM niet begrijpt".

## Constanten uit de waardelijst genereren

Waarden uit de waardelijst komen uitsluitend via de generator in de code —
niemand typt ze met de hand:

```
dotnet run --project Bcf.Vocabulary.Generator            # Bcf.Core/Vocabulary/BcfVocabulary.g.cs opnieuw schrijven
dotnet run --project Bcf.Vocabulary.Generator -- --check # controleren of het bestand actueel is
```

Vergeten opnieuw te genereren kan niet: `VocabularyDriftTests` bouwt de
constanten opnieuw uit `bcf-extensions.json` en vergelijkt ze met het
ingecheckte bestand, en `NoHardcodedVocabularyTests` bewaakt dat waarden als
`"In Progress"` nooit als tekst in de code verschijnen.

Ook de waardelijstbestanden voor een archief worden uit dezelfde constanten
samengesteld in plaats van kant-en-klaar bewaard: `ExtensionsWriter.Write30`
levert `extensions.xml` (BCF 3.0), `ExtensionsWriter.Write21` levert
`extensions.xsd` (BCF 2.1). De laatste herdefinieert typen uit `markup.xsd`,
dus dat schema moet ernaast in het archief meereizen.

## Referentiearchieven

```
dotnet run --project Bcf.TestData.Generator
```

Bouwt de fixtures in `test-data/` met de echte exporteur, byte voor byte
reproduceerbaar. Details in [`test-data/README.md`](test-data/README.md).

## Schema's

De XSD's komen uit de buildingSMART-repository `BCF-XML` (branches
`release_3_0` en `release_2_1`) en liggen hier ongewijzigd. BCF 2.1 heeft geen
`extensions.xsd` tussen de schema's: daar worden waardelijsten verklaard door
een bestand binnen elk archief, en `Bcf.Core` genereert dat. De referentiekopie
om mee te vergelijken is `schemas/2.1/extensions.reference.xsd`.

## Bouwen en testen

```
dotnet test Bcf.Core.Tests/Bcf.Core.Tests.csproj
```

De tests draaien op twee doelframeworks: `net48`, de runtime van
desktop-BIM-toepassingen, en `net8.0` voor diensten en achtergrondagenten.

## Bijdragen

De afspraken van de repository — de taal van code en documentatie, het
tweetalige XML-doc-formaat, wat wel en niet hard in de code mag — staan in
[`AGENTS.md`](AGENTS.md).

## Licentie

MIT — zie [`LICENSE`](LICENSE).
