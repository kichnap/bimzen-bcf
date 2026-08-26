[English](README.md) · **Русский** · [Deutsch](README.de.md) · [Nederlands](README.nl.md) · [Suomi](README.fi.md)

# bimzen-bcf

[![Build](https://github.com/kichnap/bimzen-bcf/actions/workflows/ci.yml/badge.svg)](https://github.com/kichnap/bimzen-bcf/actions/workflows/ci.yml)
[![License: MIT](https://img.shields.io/badge/license-MIT-green.svg)](LICENSE)
[![Target](https://img.shields.io/badge/target-netstandard2.0-blue.svg)](Bcf.Core/Bcf.Core.csproj)
[![BCF](https://img.shields.io/badge/BCF-3.0%20%7C%202.1%20%7C%202.0%20read-blue.svg)](https://github.com/buildingSMART/BCF-XML)

**Библиотека .NET для формата buildingSMART Collaboration Format (BCF)
и единственный источник правды для справочников, которые к нему прилагаются.**

`Bcf.Core` собирается под `netstandard2.0`, не имеет зависимостей времени
выполнения и ничего не знает о приложении-хосте. Всё, что связано с хостом,
живёт за узкими портами, которые реализует встраивающий. Результат
проверяется по официальным XSD buildingSMART на каждой сборке.

## Что умеет

| Область | Что даёт |
|---|---|
| Запись | BCF 3.0 и BCF 2.1 — два независимых сериализатора, а не один с параметром |
| Чтение | BCF 3.0, 2.1 и 2.0 (только чтение). Терпимое по замыслу: незнакомые статусы и типы из чужих инструментов сохраняются, а не отвергаются |
| Обновление | Дописать в существующий архив, не потеряв того, что положил туда приёмник |
| Камера | Перспектива и ортогональ, кватернион в направление и вектор верха, ограничения версий |
| Единицы | Любые единицы хоста в метры, которых требует BCF |
| Идентификаторы | IFC GUID в обе стороны и Revit `UniqueId` в IFC GUID по алгоритму самого экспортёра |
| Справочники | Типы, статусы, приоритеты, метки и стадии из одного файла, с генерацией констант |
| Идемпотентность | Устойчивый ключ замечания, переживающий повторный прогон: выгрузка не плодит дубли |

Чего библиотека намеренно **не** делает: не открывает модели, не рисует
снимки, не проверяет лицензии, не показывает окон и не ходит в сеть.
Всё это — забота хоста.

## Быстрый старт

```csharp
var settings = new BcfExportSettings
{
    Author = "coordinator@example.com",
    ProjectName = "Северный квартал",
    Version = BcfVersion.Bcf30
};

using (var file = File.Create(@"C:\exports\clashes.bcfzip"))
{
    BcfExportResult result = new BcfClashExporter(source).Export(file, settings);

    if (!result.Succeeded) { /* result.Error, result.Warnings */ }
}
```

`source` — ваша реализация `IClashSource`, единственного обязательного
порта. Полный договор, включая необязательные порты, —
в [`docs/integration.ru.md`](docs/integration.ru.md).

## Подключение

Пакет NuGet (`BimZen.Bcf.Core`) готовится. Пока он не опубликован,
подключайте проект напрямую:

```
git clone https://github.com/kichnap/bimzen-bcf.git
dotnet add <ваш-проект> reference bimzen-bcf/Bcf.Core/Bcf.Core.csproj
```

Потребителям не на .NET может пригодиться один только файл справочника
[`bcf-vocabularies/bcf-extensions.json`](bcf-vocabularies/bcf-extensions.json) —
это обычный JSON, кода в нём нет.

## Состав репозитория

```
Bcf.Core/            модель BCF, конвертеры и сериализаторы (netstandard2.0)
Bcf.Core.Tests/      xUnit, net48 + net8.0
bcf-vocabularies/    канонический справочник — ЕДИНСТВЕННЫЙ источник правды
schemas/3.0/         XSD из buildingSMART/BCF-XML, ветка release_3_0
schemas/2.1/         XSD из buildingSMART/BCF-XML, ветка release_2_1
schemas/api/         машиночитаемое описание настроек выгрузки
docs/integration.md  договор на встраивание библиотеки в свой инструмент
docs/releasing.ru.md как версия попадает на nuget.org и что настроить один раз
test-data/           эталонные архивы .bcfzip для тестов импорта
```

## Правила, которые легко нарушить не заметив

- **`Bcf.Core` ничего не знает о хосте.** Ни одной ссылки на BIM-приложение:
  библиотека собирается и тестируется на машине, где ничего такого
  не установлено. Данные приходят через узкий порт `IClashSource`.
- **Ноль зависимостей NuGet в `Bcf.Core`.** Библиотека может оказаться
  в одном процессе с другой надстройкой, несущей её же. Каждая зависимость
  удваивает риск `TypeLoadException` при расхождении версий. По той же
  причине сборка не подписывается строгим именем.
- **Модель строится по спецификации, а не по структуре zip-архива.**
  BCF описывает одни и те же сущности дважды — XML в файле и JSON по HTTP.
  Модель общая, сериализация сменная.
- **Значения справочника не хардкодятся.** Константы генерируются
  из `bcf-vocabularies/bcf-extensions.json`; `extensions.xml` (3.0)
  и `extensions.xsd` (2.1) генерируются оттуда же, а не лежат готовыми.
- **Проверка асимметрична: строгая на запись, терпимая на чтение.** Файл
  из BIMcollab или Revizto законно содержит статусы, которых вы никогда
  не видели. Отвергнуть его — самый быстрый способ прослыть инструментом,
  который «не понимает openBIM».

## Генерация констант справочника

Значения справочника попадают в код только через генератор — руками
их не пишут:

```
dotnet run --project Bcf.Vocabulary.Generator            # перезаписать Bcf.Core/Vocabulary/BcfVocabulary.g.cs
dotnet run --project Bcf.Vocabulary.Generator -- --check # проверить, что файл актуален
```

Забыть про перегенерацию не получится: `VocabularyDriftTests` строит
константы заново из `bcf-extensions.json` и сверяет с закоммиченным файлом,
а `NoHardcodedVocabularyTests` следит, чтобы значения вроде `"In Progress"`
не появлялись в коде строками.

Файлы справочников для архива тоже собираются из констант, а не хранятся
готовыми: `ExtensionsWriter.Write30` даёт `extensions.xml` (BCF 3.0),
`ExtensionsWriter.Write21` — `extensions.xsd` (BCF 2.1). Второй
переопределяет типы из `markup.xsd`, поэтому та схема обязана ехать
в архиве рядом с ним.

## Эталонные архивы

```
dotnet run --project Bcf.TestData.Generator
```

Собирает фикстуры в `test-data/` настоящим экспортёром и побайтово
воспроизводимо. Подробности — в [`test-data/README.md`](test-data/README.md).

Рядом, в [`test-data/buildingsmart/`](test-data/buildingsmart/README.ru.md),
лежат официальные эталонные архивы из репозитория buildingSMART. Их писали
чужие инструменты, поэтому их чтение — единственная внешняя проверка того,
понимает ли библиотека формат так же, как его авторы: всё остальное
в `test-data/` мы и пишем, и читаем сами.

## Схемы

XSD взяты из репозитория buildingSMART `BCF-XML` (ветки `release_3_0`
и `release_2_1`) и лежат здесь без изменений. В BCF 2.1 файла
`extensions.xsd` среди схем нет: там справочники объявляются файлом внутри
каждого архива, и его генерирует `Bcf.Core`. Эталон для сверки —
`schemas/2.1/extensions.reference.xsd`.

## Сборка и тесты

```
dotnet test Bcf.Core.Tests/Bcf.Core.Tests.csproj
```

Тесты идут на двух целевых фреймворках: `net48` — среда настольных
BIM-приложений, и `net8.0` — для сервисов и фоновых агентов.

## Участие в разработке

Соглашения репозитория — язык кода и документации, формат двуязычной
XML-документации, что можно и чего нельзя хардкодить — в
[`AGENTS.md`](AGENTS.md).

## Лицензия

MIT — см. [`LICENSE`](LICENSE).
