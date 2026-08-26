using System;
using System.Collections.Generic;
using Bcf.Core.Model;

namespace Bcf.Core.Serialization
{
    /// <summary>
    /// The outcome of reading an archive: the topics, plus everything worth
    /// showing to the user.
    ///
    /// Итог чтения архива: замечания и всё, что стоит показать пользователю.
    /// </summary>
    public class BcfReadResult
    {
        private readonly List<string> _warnings = new List<string>();
        private readonly List<BcfExternalValue> _externalValues = new List<BcfExternalValue>();

        /// <summary>
        /// The version declared in bcf.version.
        /// Версия, объявленная в bcf.version.
        /// </summary>
        public BcfVersion Version { get; internal set; } = BcfVersion.Bcf30;

        /// <summary>
        /// The project from project.bcfp, when the archive carries one.
        /// Проект из project.bcfp, если архив его несёт.
        /// </summary>
        public BcfProject Project { get; internal set; }

        /// <summary>
        /// The topics read from the archive.
        /// Замечания, прочитанные из архива.
        /// </summary>
        public IList<BcfTopic> Topics { get; } = new List<BcfTopic>();

        /// <summary>
        /// What went wrong while reading without stopping the read.
        /// Что пошло не так при чтении, не остановив его.
        /// </summary>
        public IReadOnlyList<string> Warnings
        {
            get { return _warnings; }
        }

        /// <summary>
        /// Values that are not in the vocabulary. Not an error: the standard
        /// does not fix the vocabularies, and a file from BIMcollab, Revizto or
        /// Solibri legitimately arrives with statuses of its own. The values
        /// are kept as they are, and the user sees them as a list and can map
        /// them by hand.
        ///
        /// Значения, которых нет в справочнике. Не ошибка: стандарт словари
        /// не фиксирует, и файл из BIMcollab, Revizto или Solibri законно
        /// приходит со своими статусами. Значения сохраняются как есть,
        /// пользователь видит их списком и может сопоставить вручную.
        /// </summary>
        public IReadOnlyList<BcfExternalValue> ExternalValues
        {
            get { return _externalValues; }
        }

        internal void Warn(string message)
        {
            if (!string.IsNullOrEmpty(message) && !_warnings.Contains(message))
            {
                _warnings.Add(message);
            }
        }

        internal void AddExternalValue(string field, string value, Guid topicGuid)
        {
            foreach (BcfExternalValue existing in _externalValues)
            {
                if (existing.Field == field && existing.Value == value)
                {
                    existing.Count++;
                    return;
                }
            }

            _externalValues.Add(new BcfExternalValue
            {
                Field = field,
                Value = value,
                Count = 1,
                FirstTopic = topicGuid
            });
        }
    }

    /// <summary>
    /// What an archive holds, without parsing the topics. Enough to show the
    /// user the file they picked and to decide whether it can be appended to.
    ///
    /// Что лежит в архиве, без разбора замечаний. Хватает, чтобы показать
    /// пользователю выбранный файл и решить, можно ли в него дописывать.
    /// </summary>
    public class BcfArchiveSummary
    {
        /// <summary>
        /// The version from bcf.version.
        /// Версия из bcf.version.
        /// </summary>
        public BcfVersion Version { get; internal set; } = BcfVersion.Bcf30;

        /// <summary>
        /// Whether the archive carries a bcf.version file at all.
        /// Есть ли в архиве файл bcf.version вообще.
        /// </summary>
        public bool HasVersionFile { get; internal set; }

        /// <summary>
        /// How many topics the archive holds.
        /// Сколько в архиве замечаний.
        /// </summary>
        public int TopicCount { get; internal set; }
    }

    /// <summary>
    /// A vocabulary value met while reading that we do not know.
    /// Незнакомое значение справочника, встреченное при чтении.
    /// </summary>
    public class BcfExternalValue
    {
        /// <summary>
        /// The field: TopicStatus, TopicType, Priority, Stage or Label.
        /// Поле: TopicStatus, TopicType, Priority, Stage или Label.
        /// </summary>
        public string Field { get; internal set; }

        /// <summary>
        /// The value exactly as the file spells it.
        /// Значение ровно в том виде, в каком оно записано в файле.
        /// </summary>
        public string Value { get; internal set; }

        /// <summary>
        /// How many times it was met.
        /// Сколько раз оно встретилось.
        /// </summary>
        public int Count { get; internal set; }

        /// <summary>
        /// The first topic carrying it — so that there is somewhere to look.
        /// Первое замечание с этим значением — чтобы было куда посмотреть.
        /// </summary>
        public Guid FirstTopic { get; internal set; }

        /// <inheritdoc />
        public override string ToString()
        {
            return Field + " = '" + Value + "' (" + Count + ")";
        }
    }
}
