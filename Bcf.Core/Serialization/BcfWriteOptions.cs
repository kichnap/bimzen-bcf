using System;
using Bcf.Core.Model;

namespace Bcf.Core.Serialization
{
    /// <summary>
    /// Настройки записи архива. Простой объект, который заполняет вызывающий:
    /// у второго потребителя библиотеки — агента, выгружающего коллизии по
    /// расписанию, — никакого диалога нет, те же решения приходят файлом задания.
    /// </summary>
    public class BcfWriteOptions
    {
        /// <summary>Версия формата. 3.0 — основная, 2.1 — переключаемая опция.</summary>
        public BcfVersion Version { get; set; } = BcfVersion.Bcf30;

        /// <summary>Проект выгрузки (файл project.bcfp).</summary>
        public BcfProject Project { get; set; }

        /// <summary>
        /// Автор выгрузки. Попадает в список Users справочника вместе
        /// со встреченными исполнителями.
        /// </summary>
        public string Author { get; set; }

        /// <summary>
        /// Писать ли снимки. Снятие изображения — самая медленная операция
        /// экспорта, и на больших выгрузках её отключают осознанно.
        /// </summary>
        public bool IncludeSnapshots { get; set; } = true;

        /// <summary>
        /// Метка времени записей архива. Не задана — берётся текущий момент.
        /// Задают её ради воспроизводимости: эталонные архивы должны получаться
        /// побайтово одинаковыми при повторной генерации.
        /// </summary>
        public DateTimeOffset? EntryTimestamp { get; set; }

        /// <summary>
        /// Строгая проверка значений по справочнику перед записью.
        /// Выключается только при перезаписи чужого архива, где законно
        /// встречаются значения сторонних инструментов.
        /// </summary>
        public bool ValidateVocabulary { get; set; } = true;
    }
}
