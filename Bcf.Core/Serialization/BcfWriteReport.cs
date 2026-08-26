using System;
using System.Collections.Generic;
using System.Globalization;

namespace Bcf.Core.Serialization
{
    /// <summary>
    /// What happened while the archive was written. It reaches the user: they
    /// have to know what an export to 2.1 costs them and where the data did not
    /// fit into the format.
    ///
    /// Что произошло при записи архива. Попадает к пользователю: он должен
    /// знать, чего стоит выгрузка в 2.1 и где данные не поместились в формат.
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

        /// <summary>
        /// Entries carried over from the archive being updated, unchanged.
        /// Записи, перенесённые из обновляемого архива без изменений.
        /// </summary>
        public int EntriesCopied { get; internal set; }

        /// <summary>
        /// Notes for the user, in the order they appeared.
        /// Замечания пользователю в порядке появления.
        /// </summary>
        public IReadOnlyList<string> Warnings
        {
            get { return _warnings; }
        }

        /// <summary>
        /// Fields dropped because of the limits of the format version. A set
        /// rather than a list: across five thousand topics the same message
        /// must not repeat five thousand times.
        ///
        /// Поля, отброшенные из-за ограничений версии формата. Множество,
        /// а не список: на пяти тысячах замечаний одно и то же сообщение
        /// не должно повториться пять тысяч раз.
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
                Warn("The field " + field + " is not carried over: " + reason);
            }
        }

        /// <inheritdoc />
        public override string ToString()
        {
            return string.Format(
                CultureInfo.InvariantCulture,
                "Topics: {0}, viewpoints: {1}, snapshots: {2}, warnings: {3}",
                TopicsWritten, ViewpointsWritten, SnapshotsWritten, _warnings.Count);
        }
    }
}
