using System;
using System.Collections.Generic;
using System.Globalization;

namespace Bcf.Core.Serialization
{
    /// <summary>
    /// Что произошло при записи архива. Отдаётся пользователю: он должен знать,
    /// что теряет при выгрузке в 2.1 и где данные не поместились в формат.
    /// </summary>
    public class BcfWriteReport
    {
        private readonly List<string> _warnings = new List<string>();
        private readonly HashSet<string> _droppedFields = new HashSet<string>(StringComparer.Ordinal);

        /// <summary>
        /// How many topics reached the archive.
        /// Сколько замечаний попало в архив.
        /// </summary>
        public int TopicsWritten { get; internal set; }

        /// <summary>
        /// How many viewpoints reached the archive.
        /// Сколько точек зрения попало в архив.
        /// </summary>
        public int ViewpointsWritten { get; internal set; }

        /// <summary>
        /// How many snapshots reached the archive.
        /// Сколько снимков попало в архив.
        /// </summary>
        public int SnapshotsWritten { get; internal set; }

        /// <summary>Записей, перенесённых из обновляемого архива как есть.</summary>
        public int EntriesCopied { get; internal set; }

        /// <summary>Замечания пользователю в порядке появления.</summary>
        public IReadOnlyList<string> Warnings
        {
            get { return _warnings; }
        }

        /// <summary>
        /// Поля, отброшенные из-за ограничений версии формата. Множество,
        /// а не список: на пяти тысячах топиков одно и то же сообщение
        /// не должно повторяться пять тысяч раз.
        /// </summary>
        public IReadOnlyCollection<string> DroppedFields
        {
            get { return _droppedFields; }
        }

        internal void Warn(string message)
        {
            if (!string.IsNullOrEmpty(message) && !_warnings.Contains(message))
            {
                _warnings.Add(message);
            }
        }

        internal void Drop(string field, string reason)
        {
            if (_droppedFields.Add(field))
            {
                Warn("Поле " + field + " не переносится: " + reason);
            }
        }

        /// <inheritdoc />
        public override string ToString()
        {
            return string.Format(
                CultureInfo.InvariantCulture,
                "Топиков: {0}, точек зрения: {1}, снимков: {2}, предупреждений: {3}",
                TopicsWritten, ViewpointsWritten, SnapshotsWritten, _warnings.Count);
        }
    }
}
