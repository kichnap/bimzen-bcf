using System;
using System.Collections.Generic;
using System.Globalization;
using Bcf.Core.Serialization;

namespace Bcf.Core.Clash
{
    /// <summary>
    /// Итог экспорта. Ошибка возвращается результатом, а не всплывает диалогом:
    /// в неинтерактивном контексте на диалог некому ответить, и сессия просто
    /// повиснет до сторожа.
    /// </summary>
    public class BcfExportResult
    {
        private readonly List<string> _warnings = new List<string>();

        public bool Succeeded { get; internal set; }

        /// <summary>Экспорт остановлен пользователем.</summary>
        public bool Cancelled { get; internal set; }

        /// <summary>Ошибка, из-за которой экспорт не состоялся.</summary>
        public Exception Error { get; internal set; }

        public int TopicsCreated { get; internal set; }

        /// <summary>Замечания, созданные из сохранённых видов, а не из коллизий.</summary>
        public int ViewpointTopicsCreated { get; internal set; }

        /// <summary>
        /// Замечания, которым достался ранее выданный идентификатор.
        /// Ненулевое значение означает, что повторная выгрузка легла
        /// в существующие топики, а не создала дубли.
        /// </summary>
        public int TopicsReused { get; internal set; }

        /// <summary>
        /// Существующие замечания, переписанные данными из Navisworks.
        /// Входят в <see cref="TopicsCreated"/>: в файл их записали мы.
        /// </summary>
        public int TopicsUpdated { get; internal set; }

        /// <summary>
        /// Замечания, перенесённые из обновляемого файла как есть — вместе
        /// с чужими статусами, комментариями и вложениями.
        /// </summary>
        public int TopicsKept { get; internal set; }

        public int ClashesProcessed { get; internal set; }

        /// <summary>Пропущено по фильтру статусов.</summary>
        public int ClashesSkippedByStatus { get; internal set; }

        /// <summary>Пропущено из-за ошибки на конкретной коллизии.</summary>
        public int ClashesSkippedByError { get; internal set; }

        /// <summary>
        /// Элементы, для которых не нашлось идентификатора. Замечание для них
        /// создаётся, но выделить элемент приёмник не сможет.
        /// </summary>
        public int ElementsWithoutGuid { get; internal set; }

        public int SnapshotsCaptured { get; internal set; }

        /// <summary>
        /// Снимки, на которых почти нет геометрии. Считаются отдельно:
        /// «снято 51» при 51 пустом кадре — это не отчёт, а дезинформация.
        /// </summary>
        public int SnapshotsEmpty { get; internal set; }

        /// <summary>Отчёт сериализатора: что не поместилось в версию формата.</summary>
        public BcfWriteReport WriteReport { get; internal set; }

        public IReadOnlyList<string> Warnings
        {
            get { return _warnings; }
        }

        /// <summary>
        /// Добавляет предупреждение в отчёт. Публичный: часть замечаний
        /// рождается в адаптере хоста — например, что идентификаторы
        /// элементов пришлось брать из внутренних данных Navisworks.
        /// </summary>
        public void AddWarning(string message)
        {
            if (!string.IsNullOrEmpty(message) && !_warnings.Contains(message))
            {
                _warnings.Add(message);
            }
        }

        /// <summary>Результат сорвавшегося экспорта.</summary>
        public static BcfExportResult Failed(Exception error)
        {
            return new BcfExportResult { Succeeded = false, Error = error };
        }

        internal void Warn(string message)
        {
            AddWarning(message);
        }

        public override string ToString()
        {
            return string.Format(
                CultureInfo.InvariantCulture,
                "Замечаний: {0}, обработано коллизий: {1}, пропущено по статусу: {2}, без идентификатора элементов: {3}",
                TopicsCreated, ClashesProcessed, ClashesSkippedByStatus, ElementsWithoutGuid);
        }
    }

    /// <summary>
    /// Ход экспорта. Передаётся через IProgress: экспортёр не показывает
    /// ни одного окна и ничего не спрашивает.
    /// </summary>
    public class BcfExportProgress
    {
        public string CurrentTest { get; internal set; }

        public int ProcessedClashes { get; internal set; }

        public int TotalClashes { get; internal set; }

        public int TopicsWritten { get; internal set; }

        /// <summary>Доля выполненного, 0..1. Ноль, если общее число неизвестно.</summary>
        public double Fraction
        {
            get { return TotalClashes <= 0 ? 0 : Math.Min(1.0, (double)ProcessedClashes / TotalClashes); }
        }
    }
}
