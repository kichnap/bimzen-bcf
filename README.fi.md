[English](README.md) · [Русский](README.ru.md) · [Deutsch](README.de.md) · [Nederlands](README.nl.md) · **Suomi**

# bimzen-bcf

[![Build](https://github.com/kichnap/bimzen-bcf/actions/workflows/ci.yml/badge.svg)](https://github.com/kichnap/bimzen-bcf/actions/workflows/ci.yml)
[![License: MIT](https://img.shields.io/badge/license-MIT-green.svg)](LICENSE)
[![Target](https://img.shields.io/badge/target-netstandard2.0-blue.svg)](Bcf.Core/Bcf.Core.csproj)
[![BCF](https://img.shields.io/badge/BCF-3.0%20%7C%202.1%20%7C%202.0%20read-blue.svg)](https://github.com/buildingSMART/BCF-XML)

**.NET-kirjasto buildingSMART Collaboration Format -muodolle (BCF) ja samalla
ainoa totuuden lähde siihen kuuluville sanastoille.**

`Bcf.Core` kääntyy `netstandard2.0`-kohteelle, sillä ei ole
ajonaikaisia riippuvuuksia eikä se tiedä mitään isäntäsovelluksesta. Kaikki
isäntäkohtainen on kapeiden porttien takana, jotka upottava työkalu toteuttaa.
Tulos tarkistetaan buildingSMARTin virallisia XSD-skeemoja vasten jokaisessa
käännöksessä.

## Mitä kirjasto tekee

| Alue | Mitä saat |
|---|---|
| Kirjoitus | BCF 3.0 ja BCF 2.1 — kaksi erillistä sarjallistajaa, ei yhtä valitsimella |
| Luku | BCF 3.0, 2.1 ja 2.0 (vain luku). Tarkoituksella salliva: muiden työkalujen tuntemattomat tilat ja tyypit säilyvät eikä niitä koskaan hylätä |
| Päivitys | Lisää olemassa olevaan arkistoon menettämättä sitä, mitä vastaanottava työkalu on sinne jättänyt |
| Kamera | Perspektiivi ja ortogonaali, kvaternio suunnaksi ja ylösvektoriksi, versiokohtaiset rajat |
| Yksiköt | Mikä tahansa isännän yksikkö BCF:n vaatimiksi metreiksi |
| Tunnisteet | IFC GUID molempiin suuntiin ja Revitin `UniqueId` IFC GUID:ksi viejän omalla algoritmilla |
| Sanastot | Tyypit, tilat, prioriteetit, tunnisteet ja vaiheet yhdestä tiedostosta, vakiot generoiden |
| Idempotenssi | Vakaa aiheavain, joka kestää uuden ajon: vienti ei tuota kaksoiskappaleita |

Mitä kirjasto tarkoituksella **ei** tee: ei avaa malleja, ei piirrä
tilannekuvia, ei tarkista lisenssejä, ei näytä ikkunoita eikä ota yhteyttä
verkkoon. Kaikki tämä kuuluu isännälle.

## Pikaopas

```csharp
var settings = new BcfExportSettings
{
    Author = "coordinator@example.com",
    ProjectName = "Pohjoinen kortteli",
    Version = BcfVersion.Bcf30
};

using (var file = File.Create(@"C:\exports\clashes.bcfzip"))
{
    BcfExportResult result = new BcfClashExporter(source).Export(file, settings);

    if (!result.Succeeded) { /* result.Error, result.Warnings */ }
}
```

`source` on oma toteutuksesi `IClashSource`-portista — ainoa pakollinen
portti. Koko sopimus valinnaisine portteineen on tiedostossa
[`docs/integration.md`](docs/integration.md).

## Käyttöönotto

NuGet-paketti (`BimZen.Bcf.Core`) on valmisteilla. Kunnes se on julkaistu,
viittaa projektiin suoraan:

```
git clone https://github.com/kichnap/bimzen-bcf.git
dotnet add <oma-projekti> reference bimzen-bcf/Bcf.Core/Bcf.Core.csproj
```

Muut kuin .NET-käyttäjät voivat hyödyntää pelkkää sanastotiedostoa
[`bcf-vocabularies/bcf-extensions.json`](bcf-vocabularies/bcf-extensions.json) —
se on tavallista JSONia eikä sisällä koodia.

## Repositorion rakenne

```
Bcf.Core/            BCF-malli, muuntimet ja sarjallistajat (netstandard2.0)
Bcf.Core.Tests/      xUnit, net48 + net8.0
bcf-vocabularies/    kanoninen sanasto — AINOA totuuden lähde
schemas/3.0/         XSD:t lähteestä buildingSMART/BCF-XML, haara release_3_0
schemas/2.1/         XSD:t lähteestä buildingSMART/BCF-XML, haara release_2_1
schemas/api/         koneluettava kuvaus vienti­asetuksista
docs/integration.md  sopimus kirjaston upottamisesta omaan työkaluun
test-data/           referenssiarkistot .bcfzip tuontitesteihin
```

## Säännöt, jotka rikkoutuvat helposti huomaamatta

- **`Bcf.Core` ei tiedä isännästä mitään.** Ei yhtäkään viittausta
  BIM-sovellukseen: kirjasto kääntyy ja testit ajetaan koneella, jolle ei ole
  asennettu yhtäkään. Tiedot tulevat kapean `IClashSource`-portin kautta.
- **Nolla NuGet-riippuvuutta `Bcf.Core`ssa.** Kirjasto voi päätyä samaan
  prosessiin toisen lisäosan kanssa, joka tuo mukanaan saman kirjaston. Jokainen
  riippuvuus kaksinkertaistaa `TypeLoadException`-riskin versioiden erotessa.
  Samasta syystä kokoonpanoa ei allekirjoiteta vahvalla nimellä.
- **Malli seuraa määrittelyä, ei zip-arkiston rakennetta.** BCF kuvaa samat
  oliot kahdesti — XML tiedostossa ja JSON HTTP:n yli. Malli on yhteinen,
  sarjallistus vaihdettavissa.
- **Sanaston arvoja ei koskaan kovakoodata.** Vakiot generoidaan tiedostosta
  `bcf-vocabularies/bcf-extensions.json`; samoin `extensions.xml` (3.0) ja
  `extensions.xsd` (2.1) sen sijaan, että ne kulkisivat valmiina tiedostoina.
- **Tarkistus on epäsymmetrinen: tiukka kirjoitettaessa, salliva luettaessa.**
  BIMcollabista tai Reviztosta tullut tiedosto sisältää täysin oikeutetusti
  tiloja, joita et ole ennen nähnyt. Sen hylkääminen on nopein tapa saada maine
  työkaluna, joka "ei ymmärrä openBIMiä".

## Sanastovakioiden generointi

Sanaston arvot päätyvät koodiin vain generaattorin kautta — käsin niitä ei
kirjoiteta:

```
dotnet run --project Bcf.Vocabulary.Generator            # kirjoita Bcf.Core/Vocabulary/BcfVocabulary.g.cs uudelleen
dotnet run --project Bcf.Vocabulary.Generator -- --check # tarkista, että tiedosto on ajan tasalla
```

Generoinnin unohtaminen ei ole mahdollista: `VocabularyDriftTests` rakentaa
vakiot uudelleen tiedostosta `bcf-extensions.json` ja vertaa niitä
versionhallintaan tallennettuun tiedostoon, ja `NoHardcodedVocabularyTests`
valvoo, ettei arvoja kuten `"In Progress"` esiinny koodissa merkkijonoina.

Myös arkistoon menevät sanastotiedostot kootaan samoista vakioista sen sijaan,
että ne säilytettäisiin valmiina: `ExtensionsWriter.Write30` tuottaa
`extensions.xml` (BCF 3.0) ja `ExtensionsWriter.Write21` tuottaa
`extensions.xsd` (BCF 2.1). Jälkimmäinen määrittelee uudelleen `markup.xsd`:n
tyyppejä, joten sen skeeman on matkustettava arkistossa vierellä.

## Referenssiarkistot

```
dotnet run --project Bcf.TestData.Generator
```

Rakentaa `test-data/`-hakemiston fixtuurit oikealla viejällä, tavulleen
toistettavasti. Yksityiskohdat: [`test-data/README.md`](test-data/README.md).

Niiden vieressä hakemistossa [`test-data/buildingsmart/`](test-data/buildingsmart/README.md)
ovat buildingSMARTin viralliset testitapaukset. Ne on kirjoitettu muilla
työkaluilla, joten niiden lukeminen on ainoa ulkopuolinen tarkistus siitä,
ymmärtääkö tämä kirjasto muodon samoin kuin sen tekijät.

## Skeemat

XSD:t ovat peräisin buildingSMARTin `BCF-XML`-repositoriosta (haarat
`release_3_0` ja `release_2_1`) ja ovat täällä muuttamattomina. BCF 2.1:ssä ei
ole `extensions.xsd`-tiedostoa skeemojen joukossa: siellä sanastot esitellään
jokaisen arkiston sisällä olevalla tiedostolla, jonka `Bcf.Core` generoi.
Vertailun referenssikopio on `schemas/2.1/extensions.reference.xsd`.

## Kääntäminen ja testaus

```
dotnet test Bcf.Core.Tests/Bcf.Core.Tests.csproj
```

Testit ajetaan kahdella kohdekehyksellä: `net48`, työpöydän BIM-sovellusten
ajoympäristö, ja `net8.0` palveluille ja taustaagenteille.

## Osallistuminen

Repositorion käytännöt — koodin ja dokumentaation kieli, kaksikielinen
XML-dokumentaatiomuoto, mitä saa ja mitä ei saa kovakoodata — ovat tiedostossa
[`AGENTS.md`](AGENTS.md).

## Lisenssi

MIT — katso [`LICENSE`](LICENSE).
