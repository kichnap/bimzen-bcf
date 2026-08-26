using System;
using Bcf.Core.Model;

namespace Bcf.Core.Serialization
{
    /// <summary>
    /// Settings for writing an archive: a plain object the caller fills in.
    /// It is deliberately not owned by any user interface — a headless agent
    /// receives the same decisions from a job file and has no dialog at all.
    ///
    /// Настройки записи архива: простой объект, который заполняет вызывающий.
    /// Он намеренно не принадлежит интерфейсу — агент без интерфейса получает
    /// те же решения файлом задания, и никакого диалога у него нет.
    /// </summary>
    public class BcfWriteOptions
    {
        /// <summary>
        /// The format version. 3.0 is the primary one, 2.1 is a switchable option.
        /// Версия формата. 3.0 — основная, 2.1 — переключаемая опция.
        /// </summary>
        public BcfVersion Version { get; set; } = BcfVersion.Bcf30;

        /// <summary>
        /// The project of this export — the project.bcfp file.
        /// Проект выгрузки — файл project.bcfp.
        /// </summary>
        public BcfProject Project { get; set; }

        /// <summary>
        /// The author of the export. Goes into the Users list of the
        /// vocabulary declaration together with the assignees encountered.
        ///
        /// Автор выгрузки. Попадает в список Users объявления справочников
        /// вместе со встреченными исполнителями.
        /// </summary>
        public string Author { get; set; }

        /// <summary>
        /// Whether snapshots are written. Capturing an image is the slowest
        /// operation of an export, and on large runs it is turned off knowingly.
        ///
        /// Писать ли снимки. Снятие изображения — самая медленная операция
        /// выгрузки, и на больших прогонах её отключают осознанно.
        /// </summary>
        public bool IncludeSnapshots { get; set; } = true;

        /// <summary>
        /// The timestamp of the archive entries. Not set — the current moment
        /// is used. It is set for reproducibility: reference archives must come
        /// out byte for byte identical when generated again.
        ///
        /// Метка времени записей архива. Не задана — берётся текущий момент.
        /// Задают её ради воспроизводимости: эталонные архивы должны
        /// получаться побайтово одинаковыми при повторной генерации.
        /// </summary>
        public DateTimeOffset? EntryTimestamp { get; set; }

        /// <summary>
        /// Strict validation of values against the vocabulary before writing.
        /// Turned off only when rewriting someone else's archive, where values
        /// of other tools legitimately occur.
        ///
        /// Строгая проверка значений по справочнику перед записью. Выключается
        /// только при перезаписи чужого архива, где законно встречаются
        /// значения сторонних инструментов.
        /// </summary>
        public bool ValidateVocabulary { get; set; } = true;
    }
}
