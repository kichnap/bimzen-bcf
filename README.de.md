[English](README.md) · [Русский](README.ru.md) · **Deutsch** · [Nederlands](README.nl.md) · [Suomi](README.fi.md)

# bimzen-bcf

[![Build](https://github.com/kichnap/bimzen-bcf/actions/workflows/ci.yml/badge.svg)](https://github.com/kichnap/bimzen-bcf/actions/workflows/ci.yml)
[![License: MIT](https://img.shields.io/badge/license-MIT-green.svg)](LICENSE)
[![Target](https://img.shields.io/badge/target-netstandard2.0-blue.svg)](Bcf.Core/Bcf.Core.csproj)
[![BCF](https://img.shields.io/badge/BCF-3.0%20%7C%202.1%20%7C%202.0%20read-blue.svg)](https://github.com/buildingSMART/BCF-XML)

**Eine .NET-Bibliothek für das buildingSMART Collaboration Format (BCF) –
und die einzige verbindliche Quelle für die zugehörigen Wertelisten.**

`Bcf.Core` zielt auf `netstandard2.0`, hat keine Laufzeitabhängigkeiten und
weiß nichts über die Anwendung, die es einbettet. Alles Hostspezifische liegt
hinter schmalen Ports, die das einbettende Werkzeug implementiert. Das
Ergebnis wird bei jedem Build gegen die offiziellen buildingSMART-XSDs
geprüft.

## Was die Bibliothek leistet

| Bereich | Was Sie bekommen |
|---|---|
| Schreiben | BCF 3.0 und BCF 2.1 – zwei eigenständige Serialisierer, kein parametrierter |
| Lesen | BCF 3.0, 2.1 und 2.0 (nur lesend). Bewusst tolerant: unbekannte Status und Typen fremder Werkzeuge bleiben erhalten und werden nie verworfen |
| Aktualisieren | An ein bestehendes Archiv anfügen, ohne zu verlieren, was ein Empfängerwerkzeug dort hinterlassen hat |
| Kamera | Perspektivisch und orthogonal, Quaternion zu Richtung und Up-Vektor, versionsabhängige Grenzen |
| Einheiten | Beliebige Host-Einheiten in die von BCF geforderten Meter |
| Bezeichner | IFC-GUID in beide Richtungen und Revit-`UniqueId` zu IFC-GUID nach dem Algorithmus des Exporters |
| Wertelisten | Typen, Status, Prioritäten, Labels und Phasen aus einer Datei, mit generierten Konstanten |
| Idempotenz | Ein stabiler Themenschlüssel, der einen erneuten Lauf übersteht: der Export erzeugt keine Dubletten |

Was die Bibliothek bewusst **nicht** tut: Modelle öffnen, Schnappschüsse
rendern, Lizenzen prüfen, Fenster anzeigen oder ins Netz gehen. Das alles
gehört dem Host.

## Schnellstart

```csharp
var settings = new BcfExportSettings
{
    Author = "coordinator@example.com",
    ProjectName = "Nordviertel",
    Version = BcfVersion.Bcf30
};

using (var file = File.Create(@"C:\exports\clashes.bcfzip"))
{
    BcfExportResult result = new BcfClashExporter(source).Export(file, settings);

    if (!result.Succeeded) { /* result.Error, result.Warnings */ }
}
```

`source` ist Ihre Implementierung von `IClashSource` – dem einzigen Port, der
zwingend ist. Der vollständige Vertrag samt der optionalen Ports steht in
[`docs/integration.md`](docs/integration.md).

## Einbindung

Ein NuGet-Paket (`BimZen.Bcf.Core`) ist in Vorbereitung. Solange es nicht
veröffentlicht ist, binden Sie das Projekt direkt ein:

```
git clone https://github.com/kichnap/bimzen-bcf.git
dotnet add <ihr-projekt> reference bimzen-bcf/Bcf.Core/Bcf.Core.csproj
```

Nicht-.NET-Anwender können allein die Wertelistendatei
[`bcf-vocabularies/bcf-extensions.json`](bcf-vocabularies/bcf-extensions.json)
verwenden – reines JSON, ohne Code.

## Aufbau des Repositorys

```
Bcf.Core/            BCF-Modell, Konverter und Serialisierer (netstandard2.0)
Bcf.Core.Tests/      xUnit, net48 + net8.0
bcf-vocabularies/    kanonische Werteliste – die EINZIGE verbindliche Quelle
schemas/3.0/         XSDs aus buildingSMART/BCF-XML, Branch release_3_0
schemas/2.1/         XSDs aus buildingSMART/BCF-XML, Branch release_2_1
schemas/api/         maschinenlesbare Beschreibung der Exporteinstellungen
docs/integration.md  Vertrag für die Einbettung in ein eigenes Werkzeug
test-data/           Referenzarchive .bcfzip für Importtests
```

## Regeln, die man leicht unbemerkt bricht

- **`Bcf.Core` weiß nichts über den Host.** Keine einzige Referenz auf eine
  BIM-Anwendung: Die Bibliothek baut und testet auf einem Rechner, auf dem
  keine installiert ist. Daten kommen über den schmalen Port `IClashSource`.
- **Null NuGet-Abhängigkeiten in `Bcf.Core`.** Die Bibliothek kann im selben
  Prozess wie ein anderes Add-in landen, das dieselbe Bibliothek mitbringt.
  Jede Abhängigkeit verdoppelt das Risiko einer `TypeLoadException` bei
  abweichenden Versionen. Aus demselben Grund ist die Assembly nicht
  strong-named.
- **Das Modell folgt der Spezifikation, nicht der Struktur des ZIP-Archivs.**
  BCF beschreibt dieselben Entitäten zweimal – XML in einer Datei und JSON
  über HTTP. Das Modell ist gemeinsam, die Serialisierung austauschbar.
- **Wertelisten werden nie fest verdrahtet.** Konstanten werden aus
  `bcf-vocabularies/bcf-extensions.json` generiert; `extensions.xml` (3.0)
  und `extensions.xsd` (2.1) ebenso, statt als fertige Dateien mitzureisen.
- **Die Prüfung ist asymmetrisch: streng beim Schreiben, tolerant beim
  Lesen.** Eine Datei aus BIMcollab oder Revizto enthält legitim Status, die
  Sie nie gesehen haben. Sie abzulehnen ist der schnellste Weg, als Werkzeug
  zu gelten, das „openBIM nicht versteht".

## Erzeugen der Wertelisten-Konstanten

Wertelisten gelangen ausschließlich über den Generator in den Code – von Hand
schreibt sie niemand:

```
dotnet run --project Bcf.Vocabulary.Generator            # Bcf.Core/Vocabulary/BcfVocabulary.g.cs neu schreiben
dotnet run --project Bcf.Vocabulary.Generator -- --check # prüfen, ob die Datei aktuell ist
```

Das Neuerzeugen zu vergessen ist nicht möglich: `VocabularyDriftTests` baut
die Konstanten erneut aus `bcf-extensions.json` und vergleicht sie mit der
eingecheckten Datei, und `NoHardcodedVocabularyTests` stellt sicher, dass
Werte wie `"In Progress"` nie als Zeichenketten im Code auftauchen.

Auch die Wertelistendateien für ein Archiv werden aus denselben Konstanten
zusammengesetzt statt fertig abgelegt: `ExtensionsWriter.Write30` erzeugt
`extensions.xml` (BCF 3.0), `ExtensionsWriter.Write21` erzeugt
`extensions.xsd` (BCF 2.1). Letzteres definiert Typen aus `markup.xsd` neu,
weshalb dieses Schema im Archiv daneben liegen muss.

## Referenzarchive

```
dotnet run --project Bcf.TestData.Generator
```

Erzeugt die Fixtures in `test-data/` mit dem echten Exporter, Byte für Byte
reproduzierbar. Einzelheiten in [`test-data/README.md`](test-data/README.md).

Daneben liegen in [`test-data/buildingsmart/`](test-data/buildingsmart/README.md)
die offiziellen Testfälle aus dem buildingSMART-Repository. Sie stammen von
anderen Werkzeugen; sie zu lesen ist die einzige Prüfung von außen, ob diese
Bibliothek das Format so versteht wie seine Autoren.

## Schemata

Die XSDs stammen aus dem buildingSMART-Repository `BCF-XML` (Branches
`release_3_0` und `release_2_1`) und liegen hier unverändert. BCF 2.1 hat
keine `extensions.xsd` unter seinen Schemata: Dort werden Wertelisten durch
eine Datei innerhalb jedes Archivs erklärt, und `Bcf.Core` erzeugt sie. Die
Referenzkopie zum Abgleich ist `schemas/2.1/extensions.reference.xsd`.

## Bauen und testen

```
dotnet test Bcf.Core.Tests/Bcf.Core.Tests.csproj
```

Die Tests laufen auf zwei Zielframeworks: `net48`, der Laufzeit von
Desktop-BIM-Anwendungen, und `net8.0` für Dienste und Hintergrundagenten.

## Mitwirken

Die Konventionen des Repositorys – Sprache von Code und Dokumentation, das
zweisprachige XML-Doc-Format, was fest verdrahtet werden darf und was nicht –
stehen in [`AGENTS.md`](AGENTS.md).

## Lizenz

MIT – siehe [`LICENSE`](LICENSE).
