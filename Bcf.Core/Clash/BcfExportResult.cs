using System;
using System.Collections.Generic;
using System.Globalization;
using Bcf.Core.Serialization;

namespace Bcf.Core.Clash
{
    /// <summary>
    /// The outcome of an export. A failure comes back as a result rather than
    /// popping up a dialog: in a non-interactive context there is nobody to
    /// answer a dialog, and the session would simply hang until a watchdog
    /// notices.
    ///
    /// Итог выгрузки. Ошибка возвращается результатом, а не всплывает
    /// диалогом: в неинтерактивном окружении на диалог некому ответить,
    /// и сессия просто повиснет до сторожа.
    /// </summary>
    public class BcfExportResult
    {
        private readonly List<string> _warnings = new List<string>();

        /// <summary>
        /// Whether the archive was written completely.
        /// Записан ли архив полностью.
        /// </summary>
        public bool Succeeded { get; internal set; }

        /// <summary>
        /// The export was stopped by the user.
        /// Выгрузку остановил пользователь.
        /// </summary>
        public bool Cancelled { get; internal set; }

        /// <summary>
        /// The failure that stopped the export.
        /// Ошибка, из-за которой выгрузка не состоялась.
        /// </summary>
        public Exception Error { get; internal set; }

        /// <summary>
        /// The topics this export wrote, updated ones included.
        /// Замечания, которые записала эта выгрузка, включая обновлённые.
        /// </summary>
        public int TopicsCreated { get; internal set; }

        /// <summary>
        /// Topics created from saved viewpoints rather than from clashes.
        /// Замечания, созданные из сохранённых видов, а не из коллизий.
        /// </summary>
        public int ViewpointTopicsCreated { get; internal set; }

        /// <summary>
        /// Topics that received a previously issued identifier. A non-zero
        /// value means the repeated export landed in existing topics instead of
        /// creating duplicates.
        ///
        /// Замечания, которым достался ранее выданный идентификатор. Ненулевое
        /// значение означает, что повторная выгрузка легла в существующие
        /// замечания, а не создала дубли.
        /// </summary>
        public int TopicsReused { get; internal set; }

        /// <summary>
        /// Existing topics rewritten with fresh host data. They are part of
        /// <see cref="TopicsCreated"/>: it was this export that wrote them.
        ///
        /// Существующие замечания, переписанные свежими данными хоста. Входят
        /// в <see cref="TopicsCreated"/>: в файл их записала эта выгрузка.
        /// </summary>
        public int TopicsUpdated { get; internal set; }

        /// <summary>
        /// Topics carried over from the archive being updated exactly as they
        /// were — together with statuses, comments and attachments added
        /// elsewhere.
        ///
        /// Замечания, перенесённые из обновляемого архива как есть — вместе
        /// со статусами, комментариями и вложениями, добавленными в другом
        /// инструменте.
        /// </summary>
        public int TopicsKept { get; internal set; }

        /// <summary>
        /// How many clashes were looked at.
        /// Сколько коллизий было рассмотрено.
        /// </summary>
        public int ClashesProcessed { get; internal set; }

        /// <summary>
        /// Skipped by the status filter.
        /// Пропущено по фильтру статусов.
        /// </summary>
        public int ClashesSkippedByStatus { get; internal set; }

        /// <summary>
        /// Skipped because of a failure on one particular clash.
        /// Пропущено из-за ошибки на конкретной коллизии.
        /// </summary>
        public int ClashesSkippedByError { get; internal set; }

        /// <summary>
        /// Elements for which no identifier was found. A topic is still
        /// created, but a receiving tool will not be able to highlight them.
        ///
        /// Элементы, для которых не нашлось идентификатора. Замечание всё
        /// равно создаётся, но выделить их приёмник не сможет.
        /// </summary>
        public int ElementsWithoutGuid { get; internal set; }

        /// <summary>
        /// Where the numeric element identifiers came from, and how many times.
        /// The key is the property and the tree level, the value is a count.
        ///
        /// This counter shows that on a new model the property walk went
        /// differently than before — before a consumer of the export notices it
        /// on their side.
        ///
        /// Откуда брались числовые идентификаторы элементов и сколько раз.
        /// Ключ — свойство и уровень дерева, значение — число элементов.
        ///
        /// По этому счётчику видно, что на новой модели обход свойств пошёл
        /// иначе, чем прежде, — до того, как это заметит потребитель выгрузки
        /// на своей стороне.
        /// </summary>
        public IDictionary<string, int> ElementIdSources { get; } = new Dictionary<string, int>(StringComparer.Ordinal);

        /// <summary>
        /// How the elements were identified, and how many times: the key is a
        /// value of <see cref="ElementIdOrigin"/>, the value is a count.
        ///
        /// The difference between "read from an IFC property" and "computed
        /// from a Revit UniqueId" matters to anyone comparing the export with
        /// the IFC of the same model: the first always matches the IFC file,
        /// the second matches everywhere the IFC exporter did not split the
        /// element or generate the entity itself.
        ///
        /// Чем опознаны элементы и сколько раз: ключ — значение
        /// <see cref="ElementIdOrigin"/>, значение — число элементов.
        ///
        /// Разница между «прочитан из свойства IFC» и «вычислен из Revit
        /// UniqueId» принципиальна для того, кто сверяет выгрузку с IFC той же
        /// модели: первый совпадёт с файлом IFC всегда, второй — только там,
        /// где экспортёр IFC не разрезал элемент и не породил сущность сам.
        /// </summary>
        public IDictionary<string, int> ElementIdOrigins { get; } = new Dictionary<string, int>(StringComparer.Ordinal);

        /// <summary>
        /// How many snapshots were captured.
        /// Сколько снимков было снято.
        /// </summary>
        public int SnapshotsCaptured { get; internal set; }

        /// <summary>
        /// Snapshots that carry almost no geometry. Counted separately:
        /// "51 captured" with 51 empty frames is not a report, it is
        /// misinformation.
        ///
        /// Снимки, на которых почти нет геометрии. Считаются отдельно:
        /// «снято 51» при 51 пустом кадре — это не отчёт, а дезинформация.
        /// </summary>
        public int SnapshotsEmpty { get; internal set; }

        /// <summary>
        /// The serializer report: what did not fit into the format version.
        /// Отчёт сериализатора: что не поместилось в версию формата.
        /// </summary>
        public BcfWriteReport WriteReport { get; internal set; }

        /// <summary>
        /// Everything worth telling the user about, in the order it happened.
        /// Всё, о чём стоит рассказать пользователю, в порядке появления.
        /// </summary>
        public IReadOnlyList<string> Warnings
        {
            get { return _warnings; }
        }

        /// <summary>
        /// Adds a warning to the report. Public because some warnings are born
        /// in the host adapter — for instance, that element identifiers had to
        /// be taken from the host's internal data.
        ///
        /// Добавляет предупреждение в отчёт. Публичный, потому что часть
        /// предупреждений рождается в адаптере хоста — например, что
        /// идентификаторы элементов пришлось брать из его внутренних данных.
        /// </summary>
        /// <param name="message">The warning text; duplicates are ignored.</param>
        public void AddWarning(string message)
        {
            if (!string.IsNullOrEmpty(message) && !_warnings.Contains(message))
            {
                _warnings.Add(message);
            }
        }

        /// <summary>
        /// The result of an export that failed outright.
        /// Результат выгрузки, сорвавшейся целиком.
        /// </summary>
        /// <param name="error">The failure to report.</param>
        public static BcfExportResult Failed(Exception error)
        {
            return new BcfExportResult { Succeeded = false, Error = error };
        }

        internal void Warn(string message)
        {
            AddWarning(message);
        }

        /// <inheritdoc />
        public override string ToString()
        {
            return string.Format(
                CultureInfo.InvariantCulture,
                "Topics: {0}, clashes processed: {1}, skipped by status: {2}, elements without an identifier: {3}",
                TopicsCreated, ClashesProcessed, ClashesSkippedByStatus, ElementsWithoutGuid);
        }
    }

    /// <summary>
    /// The progress of an export. Delivered through IProgress: the exporter
    /// shows no window and asks nothing.
    ///
    /// Ход выгрузки. Передаётся через IProgress: экспортёр не показывает
    /// ни одного окна и ничего не спрашивает.
    /// </summary>
    public class BcfExportProgress
    {
        /// <summary>
        /// The test or the saved view being processed right now.
        /// Проверка или сохранённый вид, который обрабатывается сейчас.
        /// </summary>
        public string CurrentTest { get; internal set; }

        /// <summary>
        /// How many clashes and views have been passed.
        /// Сколько коллизий и видов пройдено.
        /// </summary>
        public int ProcessedClashes { get; internal set; }

        /// <summary>
        /// The total to be passed, saved views included.
        /// Сколько всего предстоит пройти, вместе с сохранёнными видами.
        /// </summary>
        public int TotalClashes { get; internal set; }

        /// <summary>
        /// How many topics have reached the archive.
        /// Сколько замечаний уже попало в архив.
        /// </summary>
        public int TopicsWritten { get; internal set; }

        /// <summary>
        /// The fraction done, 0..1. Zero when the total is unknown.
        /// Доля выполненного, 0..1. Ноль, если общее число неизвестно.
        /// </summary>
        public double Fraction
        {
            get { return TotalClashes <= 0 ? 0 : Math.Min(1.0, (double)ProcessedClashes / TotalClashes); }
        }
    }
}
